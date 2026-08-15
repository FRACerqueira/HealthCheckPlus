// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace HealthCheckPlusTests
{
    // Integration tests covering the AddCheckLinkTo mechanism, which adopts an existing
    // registration via the public Options pipeline instead of reaching into internal ASP.NET Core
    // types.
    public class AddCheckLinkToTests
    {
        private sealed class AlwaysHealthyCheck : IHealthCheck
        {
            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(HealthCheckResult.Healthy());
            }
        }

        [Fact]
        public void AddCheckLinkTo_ShouldReplaceOriginalRegistration_WithTheWrappedName()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var ihb = services.AddHealthChecksPlus(["Adopted"]);
            ihb.Add(new HealthCheckRegistration("Original", _ => new AlwaysHealthyCheck(), null, null));
            ihb.AddCheckLinkTo("Adopted", "Original");

            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

            Assert.Contains(options.Registrations, r => r.Name == "Adopted");
            Assert.DoesNotContain(options.Registrations, r => r.Name == "Original");
        }

        [Fact]
        public void AddCheckLinkTo_ShouldThrowClearException_WhenNamedCheckWasNeverRegistered()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var ihb = services.AddHealthChecksPlus(["Adopted"]);
            ihb.AddCheckLinkTo("Adopted", "DoesNotExist");

            using var provider = services.BuildServiceProvider();

            var ex = Assert.Throws<InvalidOperationException>(
                () => provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value);
            Assert.Contains("DoesNotExist", ex.Message, StringComparison.Ordinal);
        }

        private sealed class CountingFactoryCheck : IHealthCheck
        {
            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(HealthCheckResult.Healthy());
            }
        }

        // Regression test: the adopted check's factory caches the constructed
        // WrapperBaseHealthCheckPlus via ConcurrentDictionary.GetOrAdd(key, valueFactory) — but
        // GetOrAdd's valueFactory has no once-only guarantee under contention. Two overlapping
        // calls to registration.Factory (e.g. an HTTP request and a
        // background cycle both finding the check "due" for its first run at the same time) could
        // each construct a real underlying check instance, with the loser silently discarded and
        // never disposed. This test drives many concurrent factory calls at once, before the check
        // has been cached, to reproduce that race.
        [Fact]
        public async Task AddCheckLinkTo_ShouldConstructTheAdoptedCheckOnlyOnce_UnderConcurrentFactoryInvocations()
        {
            var constructionCount = 0;

            var services = new ServiceCollection();
            services.AddLogging();
            var ihb = services.AddHealthChecksPlus(["Adopted"]);
            ihb.Add(new HealthCheckRegistration("Original", _ =>
            {
                Interlocked.Increment(ref constructionCount);
                return new CountingFactoryCheck();
            }, null, null));
            ihb.AddCheckLinkTo("Adopted", "Original");

            using var provider = services.BuildServiceProvider();
            var registration = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
                .Single(r => r.Name == "Adopted");

            const int concurrency = 20;
            using var barrier = new Barrier(concurrency);
            var tasks = Enumerable.Range(0, concurrency)
                .Select(_ => Task.Run(() =>
                {
                    barrier.SignalAndWait(TestContext.Current.CancellationToken);
                    return registration.Factory(provider);
                }))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            Assert.Equal(1, constructionCount);
            Assert.All(results, r => Assert.Same(results[0], r));
        }
    }
}
