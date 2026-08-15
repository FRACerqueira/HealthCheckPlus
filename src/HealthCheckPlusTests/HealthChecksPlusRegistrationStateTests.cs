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
    // Regression test for the action plan (doc/plano-acao-healthcheckplus.md), step P1.2.
    // High finding from the audit (doc/healthcheckplus-audit.html), previously "Inferido"
    // (architectural risk, not reproduced at runtime): a process-wide static field shared the
    // "AddHealthChecksPlus was called" flag and adopted external check instances across every
    // IServiceCollection in the process, which would leak state between hosts built in the same
    // process (WebApplicationFactory, .NET Aspire, parallel tests). This is now "Verificado":
    // step P1.1 replaced the static fields with a HealthChecksPlusRegistrationState instance
    // scoped to each IServiceCollection, and this test proves two independent hosts no longer
    // share it.
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
            var ihb = services.AddHealthChecksPlus(["MyCheck"]);
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

        // Regression test for the R3 follow-up (doc/progresso-plano-acao.md): disposing the
        // container must dispose adopted external check instances cached in
        // HealthChecksPlusRegistrationState.ExternalCheck. DefaultHealthCheckServicePlus — a
        // factory-registered, container-managed singleton, unlike HealthChecksPlusRegistrationState
        // itself — now does this in its own Dispose().
        [Fact]
        public async Task DisposingTheContainer_ShouldDisposeAdoptedExternalCheckInstances()
        {
            var check = new DisposableTrackingCheck();

            var services = new ServiceCollection();
            services.AddLogging();
            var ihb = services.AddHealthChecksPlus(["MyCheck"]);
            ihb.Add(new HealthCheckRegistration("Original", _ => check, null, null));
            ihb.AddCheckLinkTo("MyCheck", "Original");

            var provider = services.BuildServiceProvider();
            var service = (DefaultHealthCheckServicePlus)provider.GetRequiredService<HealthCheckService>();
            await service.CheckHealthPlusAsync(null, null, HealthCheckTrigger.UrlRequest, CancellationToken.None);

            Assert.False(check.Disposed);

            provider.Dispose();

            Assert.True(check.Disposed);
        }

        // Gap found during the advisor re-validation pass after the publisher-cycle-error fix
        // (doc/progresso-plano-acao.md, Fase 4 session log): DefaultHealthCheckServicePlus.Dispose()
        // looped over every adopted external check with no fault isolation and no operator signal
        // — the same class of mistake just caught in the background service's publish dispatch,
        // just in the disposal path. A consumer-supplied IDisposable.Dispose() throwing (a real
        // scenario, e.g. a connection multiplexer failing because its socket was already force-
        // closed) aborted the loop, silently leaking every remaining adopted check for the rest of
        // process shutdown.
        [Fact]
        public async Task Dispose_ShouldIsolateFaults_WhenOneAdoptedCheckThrowsOnDispose()
        {
            using var capture = new MetricsCapture();
            var throwing = new ThrowingOnDisposeCheck();
            var healthy = new DisposableTrackingCheck();
            var loggerProvider = new CapturingLoggerProvider();

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddProvider(loggerProvider));
            var ihb = services.AddHealthChecksPlus(["Check1", "Check2"]);
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
