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
    // Regression tests for the action plan (doc/plano-acao-healthcheckplus.md), Fase 0.
    // Each test here reproduces a finding from the audit (doc/healthcheckplus-audit.html) and is
    // expected to fail against the pre-Fase-0 code — that's intentional, it's proof the bug exists.
    public class DefaultHealthCheckServicePlusTests
    {
        private sealed class AlwaysHealthyCheck : IHealthCheck
        {
            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(HealthCheckResult.Healthy());
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

        // Critical finding: "Degraded policy is ignored on the HTTP request path"
        // (DefaultHealthCheckServicePlus.CheckHealthPlusAsync, case HealthStatus.Degraded branch).
        // Setup: check "Test1" has been Degraded for 10s. The Healthy policy has a 1000s period
        // (should NOT trigger a rerun). The Degraded policy has a 3s period (should trigger).
        // No Unhealthy policy is registered.
        // Current (buggy) behavior: the Degraded branch mistakenly looks up an Unhealthy policy,
        // doesn't find one, and `policy` keeps its initial value (the Healthy policy) — uses the
        // 1000s period and does NOT rerun the check.
        // Expected behavior (after the fix): should use the Degraded period (3s), which has
        // already elapsed, and rerun the check.
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

        // Critical finding: "NullReferenceException when mixing native checks with the
        // HealthCheckPlus API". Product decision (P0.4, recorded in doc/progresso-plano-acao.md):
        // Option A — fail early and clearly at service construction time, instead of letting
        // health evaluation throw a NullReferenceException later, at runtime.
        // Setup: "NativeCheck" is registered in HealthCheckServiceOptions (as would happen via
        // the native IHealthChecksBuilder) but no Healthy policy was registered for it (as would
        // happen if a developer forgot AddCheckPlus/AddCheckLinkTo).
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

        // Characterization test added alongside the P0.3 consolidation (doc/plano-acao-healthcheckplus.md)
        // to confirm the Unhealthy branch of the foreground/HTTP path — which was already correct
        // before the refactor — still behaves the same after ResolveForegroundPolicy replaced the
        // inline switch statement.
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

        // Characterization test added alongside the P0.3 consolidation. Unlike the foreground
        // path, the background path (ResolveBackgroundPolicy) falls back to the background
        // service's own per-status defaults (HealthCheckPlusBackGroundOptions), not to the
        // check's Healthy policy, when no explicit policy is registered for the current status.
        // This is intentionally different behavior between the two paths (see ADR-style rationale
        // in doc/plano-acao-healthcheckplus.md, Fase 0) and must survive the consolidation.
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
    }
}
