// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Abstractions;
using HealthCheckPlus.Internal;
using HealthCheckPlus.Internal.WrapperMicrosoft;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace HealthCheckPlusTests
{
    // Regression test: registration-time state (the "AddHealthChecksPlus was called" flag and
    // adopted external check instances) must be scoped per IServiceCollection, not process-wide —
    // a process-wide static field would leak state between hosts built in the same process
    // (WebApplicationFactory, .NET Aspire, parallel tests). This test proves two independent hosts
    // don't share HealthChecksPlusRegistrationState.
    public class HealthChecksPlusRegistrationStateTests
    {
        private sealed class AlwaysHealthyCheck : IHealthCheck
        {
            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(HealthCheckResult.Healthy());
            }
        }

        private sealed class DisposableTrackingCheck : IHealthCheck, IDisposable
        {
            public bool Disposed { get; private set; }

            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(HealthCheckResult.Healthy());
            }

            public void Dispose()
            {
                Disposed = true;
            }
        }

        private sealed class ThrowingOnDisposeCheck : IHealthCheck, IDisposable
        {
            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(HealthCheckResult.Healthy());
            }

            public void Dispose()
            {
                throw new InvalidOperationException("simulated Dispose failure");
            }
        }

        private static ServiceProvider BuildHostWithAdoptedCheck(string linkName)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var ihb = services.AddHealthChecksPlus();
            ihb.Add(new HealthCheckRegistration(linkName, _ => new AlwaysHealthyCheck(), null, null));
            ihb.AddCheckLinkTo("MyCheck", linkName);
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task TwoHostsInSameProcess_ShouldNotShareRegistrationStateOrAdoptedCheckInstances()
        {
            using var provider1 = BuildHostWithAdoptedCheck("Shared");
            using var provider2 = BuildHostWithAdoptedCheck("Shared");

            var service1 = (DefaultHealthCheckServicePlus)provider1.GetRequiredService<HealthCheckService>();
            var service2 = (DefaultHealthCheckServicePlus)provider2.GetRequiredService<HealthCheckService>();

            await service1.CheckHealthPlusAsync(null, null, HealthCheckTrigger.UrlRequest, CancellationToken.None);
            await service2.CheckHealthPlusAsync(null, null, HealthCheckTrigger.UrlRequest, CancellationToken.None);

            var state1 = provider1.GetRequiredService<HealthChecksPlusRegistrationState>();
            var state2 = provider2.GetRequiredService<HealthChecksPlusRegistrationState>();

            Assert.NotSame(state1, state2);
            Assert.True(state1.ExternalCheck.ContainsKey("MyCheck"));
            Assert.True(state2.ExternalCheck.ContainsKey("MyCheck"));
            Assert.NotSame(state1.ExternalCheck["MyCheck"].Value, state2.ExternalCheck["MyCheck"].Value);
        }

        // Regression test: disposing the container must dispose adopted external check instances
        // cached in HealthChecksPlusRegistrationState.ExternalCheck. DefaultHealthCheckServicePlus
        // — a factory-registered, container-managed singleton, unlike
        // HealthChecksPlusRegistrationState itself — does this in its own Dispose().
        [Fact]
        public async Task DisposingTheContainer_ShouldDisposeAdoptedExternalCheckInstances()
        {
            var check = new DisposableTrackingCheck();

            var services = new ServiceCollection();
            services.AddLogging();
            var ihb = services.AddHealthChecksPlus();
            ihb.Add(new HealthCheckRegistration("Original", _ => check, null, null));
            ihb.AddCheckLinkTo("MyCheck", "Original");

            var provider = services.BuildServiceProvider();
            var service = (DefaultHealthCheckServicePlus)provider.GetRequiredService<HealthCheckService>();
            await service.CheckHealthPlusAsync(null, null, HealthCheckTrigger.UrlRequest, CancellationToken.None);

            Assert.False(check.Disposed);

            provider.Dispose();

            Assert.True(check.Disposed);
        }

        // Regression test: DefaultHealthCheckServicePlus.Dispose() must not let one adopted check's
        // Dispose() throwing abort the loop and silently leak every remaining adopted check. A
        // consumer-supplied IDisposable.Dispose() throwing is a real scenario (e.g. a connection
        // multiplexer failing because its socket was already force-closed).
        [Fact]
        public async Task Dispose_ShouldIsolateFaults_WhenOneAdoptedCheckThrowsOnDispose()
        {
            using var capture = new MetricsCapture();
            var throwing = new ThrowingOnDisposeCheck();
            var healthy = new DisposableTrackingCheck();
            var loggerProvider = new CapturingLoggerProvider();

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddProvider(loggerProvider));
            var ihb = services.AddHealthChecksPlus();
            ihb.Add(new HealthCheckRegistration("Original1", _ => throwing, null, null));
            ihb.Add(new HealthCheckRegistration("Original2", _ => healthy, null, null));
            ihb.AddCheckLinkTo("Check1", "Original1");
            ihb.AddCheckLinkTo("Check2", "Original2");

            using var provider = services.BuildServiceProvider();
            var service = (DefaultHealthCheckServicePlus)provider.GetRequiredService<HealthCheckService>();
            await service.CheckHealthPlusAsync(null, null, HealthCheckTrigger.UrlRequest, TestContext.Current.CancellationToken);

            var exception = Record.Exception(() => service.Dispose());

            Assert.Null(exception);
            Assert.True(healthy.Disposed, "The second adopted check must still be disposed despite the first one throwing.");
            Assert.Contains(loggerProvider.Entries, e => e.Level == LogLevel.Warning && e.EventId.Name == "HealthCheckDisposeError");
            Assert.Contains(capture.Measurements, m =>
                m.InstrumentName == "healthcheckplus.anomalies" && (string?)m.Tags["healthcheckplus.anomaly.reason"] == "adopted_check_dispose_failed");
        }
    }
}
