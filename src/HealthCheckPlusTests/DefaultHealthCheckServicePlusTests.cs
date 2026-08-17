// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Abstractions;
using HealthCheckPlus.Internal;
using HealthCheckPlus.Internal.Policies;
using HealthCheckPlus.Internal.WrapperMicrosoft;
using HealthCheckPlus.options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HealthCheckPlusTests
{
    // Regression tests covering DefaultHealthCheckServicePlus's policy resolution and scheduling.
    public class DefaultHealthCheckServicePlusTests
    {
        private sealed class AlwaysHealthyCheck : IHealthCheck
        {
            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(HealthCheckResult.Healthy());
            }
        }

        private sealed class SlowCountingCheck : IHealthCheck
        {
            public int CallCount;

            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref CallCount);
                Thread.Sleep(50); // widen the race window so overlapping callers actually overlap
                return Task.FromResult(HealthCheckResult.Healthy());
            }
        }

        // Blocks on the ambient cancellationToken passed in by RunCheckAsync (not a per-check
        // timeout token) until externally cancelled, so a test can control exactly when the
        // ambient token fires mid-execution.
        private sealed class CancelableCheck : IHealthCheck
        {
            public readonly ManualResetEventSlim Started = new(false);

            public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                Started.Set();
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return HealthCheckResult.Healthy();
            }
        }

        private static DefaultHealthCheckServicePlus BuildService(
            CacheHealthCheckPlus cache,
            HealthCheckServiceOptions hcOptions,
            params IHealthCheckPlusPolicyStatus[] policies)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IStateHealthChecksPlus>(cache);
            foreach (var policy in policies)
            {
                services.AddSingleton(policy);
            }
            var provider = services.BuildServiceProvider();

            return new DefaultHealthCheckServicePlus(
                provider.GetRequiredService<IServiceScopeFactory>(),
                provider,
                NullLogger<HealthCheckService>.Instance,
                Options.Create(hcOptions),
                new HealthChecksPlusRegistrationState());
        }

        // The Degraded status must resolve to its own policy on the HTTP request path, not the
        // Healthy policy. Setup: check "Test1" has been Degraded for 10s. The Healthy policy has a
        // 1000s period (must NOT trigger a rerun on its own). The Degraded policy has a 3s period
        // (must trigger). No Unhealthy policy is registered, ruling out a Degraded-falls-through-to-
        // Unhealthy-lookup regression too.
        [Fact]
        public async Task CheckHealthPlusAsync_ShouldUseDegradedPolicy_WhenLastStatusIsDegraded()
        {
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test1"]);
            cache.Running("Test1", true);
            cache.Update("Test1", HealthCheckTrigger.Background, new HealthCheckResult(HealthStatus.Degraded), DateTime.UtcNow.AddSeconds(-10), TimeSpan.Zero);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test1", _ => new AlwaysHealthyCheck(), null, null));

            var healthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1000), "Test1");
            var degradedPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Degraded, TimeSpan.Zero, TimeSpan.FromSeconds(3), "Test1");

            var service = BuildService(cache, hcOptions, healthyPolicy, degradedPolicy);

            await service.CheckHealthPlusAsync(null, null, HealthCheckTrigger.UrlRequest, CancellationToken.None);

            var status = cache.FullStatus("Test1");
            Assert.Equal(HealthCheckTrigger.UrlRequest, status.Origin);
            Assert.Equal(HealthStatus.Healthy, status.LastResult.Status);
        }

        // A health check registered without a matching Healthy policy must fail fast and clearly at
        // service construction time, instead of throwing a NullReferenceException later, at
        // runtime, when health is evaluated. Setup: "NativeCheck" is registered in
        // HealthCheckServiceOptions (as would happen via the native IHealthChecksBuilder) but no
        // Healthy policy was registered for it (as would happen if a developer forgot
        // AddCheckPlus/AddCheckLinkTo).
        [Fact]
        public void Constructor_ShouldThrowClearException_WhenRegistrationHasNoHealthyPolicy()
        {
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["NativeCheck"]);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("NativeCheck", _ => new AlwaysHealthyCheck(), null, null));

            var ex = Assert.Throws<InvalidOperationException>(() => BuildService(cache, hcOptions));
            Assert.Contains("NativeCheck", ex.Message, StringComparison.Ordinal);
        }

        // A health check registered with a Healthy policy (as AddCheckPlus/AddCheckLinkTo always
        // do) but whose name was left out of the `names` list passed to AddHealthChecksPlus has no
        // entry in the cache - ValidateHealthyPolicies alone doesn't catch this, since it only
        // checks the opposite direction (a name without a matching policy). Left unchecked, this
        // reaches CacheHealthCheckPlus.FullStatus's raw dictionary indexer on every later
        // request/cycle and crashes with an unhandled KeyNotFoundException - exactly the kind of
        // failure the constructor-time fail-fast for the original NRE finding was meant to
        // eliminate, just reachable from the other direction. Setup: "Kafka" has a Healthy policy
        // but only "OtherCheck" was passed to AddHealthChecksPlus's names list (simulated here via
        // cache.InitCache).
        [Fact]
        public void Constructor_ShouldThrowClearException_WhenRegistrationNameIsMissingFromCache()
        {
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["OtherCheck"]);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Kafka", _ => new AlwaysHealthyCheck(), null, null));

            var healthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(30), "Kafka");

            var ex = Assert.Throws<InvalidOperationException>(() => BuildService(cache, hcOptions, healthyPolicy));
            Assert.Contains("Kafka", ex.Message, StringComparison.Ordinal);
        }

        // Regression test: the cache's per-check Tags (used by CacheHealthCheckPlus.CreateReport,
        // which a callback registered via AddStatusName consumes through IStateHealthChecksPlus.
        // Status) must be populated from the real registrations at construction time - InitCache
        // alone has no access to them (only the plain `names` list passed to AddHealthChecksPlus).
        [Fact]
        public void Constructor_ShouldPopulateCacheTags_FromRegistrations()
        {
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test1"]);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test1", _ => new AlwaysHealthyCheck(), null, ["tag-a", "tag-b"]));

            var healthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(30), "Test1");

            BuildService(cache, hcOptions, healthyPolicy);

            Assert.Equal(["tag-a", "tag-b"], cache.CreateReport().Entries["Test1"].Tags);
        }

        // Characterization test confirming the Unhealthy branch of the foreground/HTTP path behaves
        // correctly through ResolveForegroundPolicy.
        [Fact]
        public async Task CheckHealthPlusAsync_ShouldUseUnhealthyPolicy_WhenLastStatusIsUnhealthy()
        {
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test1"]);
            cache.Running("Test1", true);
            cache.Update("Test1", HealthCheckTrigger.Background, new HealthCheckResult(HealthStatus.Unhealthy), DateTime.UtcNow.AddSeconds(-10), TimeSpan.Zero);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test1", _ => new AlwaysHealthyCheck(), null, null));

            var healthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1000), "Test1");
            var unhealthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Unhealthy, TimeSpan.Zero, TimeSpan.FromSeconds(3), "Test1");

            var service = BuildService(cache, hcOptions, healthyPolicy, unhealthyPolicy);

            await service.CheckHealthPlusAsync(null, null, HealthCheckTrigger.UrlRequest, CancellationToken.None);

            var status = cache.FullStatus("Test1");
            Assert.Equal(HealthCheckTrigger.UrlRequest, status.Origin);
            Assert.Equal(HealthStatus.Healthy, status.LastResult.Status);
        }

        // Characterization test: unlike the foreground path, the background path
        // (ResolveBackgroundPolicy) falls back to the background service's own per-status defaults
        // (HealthCheckPlusBackGroundOptions), not to the check's Healthy policy, when no explicit
        // policy is registered for the current status. This is an intentional difference between
        // the two paths, not a bug — see ResolveForegroundPolicy/ResolveBackgroundPolicy.
        [Fact]
        public async Task BackGroudCheckHealthPlusAsync_ShouldFallBackToBackgroundOptionsDefault_WhenNoExplicitPolicy()
        {
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test2"]);
            cache.Running("Test2", true);
            cache.Update("Test2", HealthCheckTrigger.Background, new HealthCheckResult(HealthStatus.Degraded), DateTime.UtcNow.AddSeconds(-10), TimeSpan.Zero);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test2", _ => new AlwaysHealthyCheck(), null, null));

            // Only a Healthy policy is registered — no Degraded policy — so the fallback must come
            // from backgroundOptions.DegradedPeriod, not from the (much longer) Healthy period.
            var healthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1000), "Test2");

            var service = BuildService(cache, hcOptions, healthyPolicy);

            var backgroundOptions = new HealthCheckPlusBackGroundOptions
            {
                DegradedPeriod = TimeSpan.FromSeconds(3)
            };

            await service.BackGroudCheckHealthPlusAsync(backgroundOptions, CancellationToken.None);

            var status = cache.FullStatus("Test2");
            Assert.Equal(HealthStatus.Healthy, status.LastResult.Status);
        }

        // Regression test for a scheduling race: reading "due" and marking Running as two separate
        // steps would let concurrent callers deciding the same check is due at the same time (e.g.
        // an HTTP request racing a background cycle) both schedule and run it. This drives many
        // concurrent CheckHealthPlusAsync calls at the exact same instant (via Barrier) while the
        // check is due, and expects the underlying IHealthCheck to run once.
        [Fact]
        public async Task CheckHealthPlusAsync_ShouldRunTheCheckOnlyOnce_WhenCalledConcurrentlyWhileDue()
        {
            var check = new SlowCountingCheck();
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test1"]);
            cache.Running("Test1", true);
            cache.Update("Test1", HealthCheckTrigger.Background, new HealthCheckResult(HealthStatus.Healthy), DateTime.UtcNow.AddSeconds(-10), TimeSpan.Zero);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test1", _ => check, null, null));

            var healthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1), "Test1");

            var service = BuildService(cache, hcOptions, healthyPolicy);

            const int concurrency = 20;
            using var barrier = new Barrier(concurrency);
            var tasks = Enumerable.Range(0, concurrency).Select(_ => Task.Run(async () =>
            {
                barrier.SignalAndWait();
                await service.CheckHealthPlusAsync(null, null, HealthCheckTrigger.UrlRequest, CancellationToken.None);
            })).ToArray();

            await Task.WhenAll(tasks);

            Assert.Equal(1, check.CallCount);
        }

        // Regression test: TryBeginRun marks a check Running before its task runs, and only
        // Update() ever clears that flag. If the ambient cancellationToken (not a per-check
        // timeout) fires while the check is in flight - e.g. an HTTP client disconnecting, which
        // surfaces here as httpContext.RequestAborted - RunCheckAsync deliberately lets that
        // OperationCanceledException propagate uncaught, which used to make Task.WhenAll skip the
        // loop that calls Update entirely, wedging the check as Running forever. It must still be
        // released even though the exception itself is still expected to propagate to the caller.
        [Fact]
        public async Task CheckHealthPlusAsync_ShouldReleaseRunning_WhenAmbientTokenIsCancelledMidFlight()
        {
            var check = new CancelableCheck();
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test1"]);
            cache.Running("Test1", true);
            cache.Update("Test1", HealthCheckTrigger.Background, new HealthCheckResult(HealthStatus.Healthy), DateTime.UtcNow.AddSeconds(-10), TimeSpan.Zero);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test1", _ => check, null, null));

            var healthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1), "Test1");

            var service = BuildService(cache, hcOptions, healthyPolicy);

            using var cts = new CancellationTokenSource();
            var callTask = service.CheckHealthPlusAsync(null, null, HealthCheckTrigger.UrlRequest, cts.Token);

            // Generous timeout: the 3 target frameworks' test processes run concurrently in CI/local
            // full-suite runs, and under that contention the background Task.Run here can take a
            // few seconds to get scheduled even though nothing is actually stuck.
            Assert.True(check.Started.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken), "The check never started.");
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => callTask);

            var status = cache.FullStatus("Test1");
            Assert.False(status.Running, "The check was left permanently marked Running after the ambient token was cancelled.");
        }
    }
}
