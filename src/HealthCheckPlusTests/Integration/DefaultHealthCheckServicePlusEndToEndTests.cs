// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Abstractions;
using HealthCheckPlus.options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace HealthCheckPlusTests.Integration
{
    // End-to-end coverage: repeats the policy scenario already covered in-process by
    // DefaultHealthCheckServicePlusTests, but now through a real HTTP request against a real
    // ASP.NET Core pipeline.
    public class DefaultHealthCheckServicePlusEndToEndTests
    {
        private sealed class CountingCheck : IHealthCheck
        {
            public int CallCount;

            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref CallCount);
                return Task.FromResult(HealthCheckResult.Healthy());
            }
        }

        [Fact]
        public async Task GetHealth_ShouldRerunCheck_AfterDegradedPeriodElapses_ThroughRealHttpPipeline()
        {
            var check = new CountingCheck();

            using var host = await TestHost.CreateAsync(
                services =>
                {
                    services.AddLogging();
                    services.AddSingleton(check);
                    var ihb = services.AddHealthChecksPlus(["Test1"]);
                    ihb.AddCheckPlus<CountingCheck>("Test1");
                    ihb.AddDegradedPolicy("Test1", TimeSpan.FromSeconds(1));
                },
                app => app.UseHealthChecksPlus("/health"));

            var client = host.GetTestClient();

            // First hit: runs the check for the first time (initial state has no prior run yet).
            await client.GetAsync("/health", TestContext.Current.CancellationToken);
            Assert.Equal(1, check.CallCount);

            // Force the cached status to Degraded so the next request must consult the Degraded
            // policy (1s period) instead of the Healthy policy's default.
            var state = host.Services.GetRequiredService<IStateHealthChecksPlus>();
            state.SwitchToDegraded("Test1");

            // Immediately after switching, the Degraded period has not elapsed yet — no rerun.
            await client.GetAsync("/health", TestContext.Current.CancellationToken);
            Assert.Equal(1, check.CallCount);

            // Wait past the 1s Degraded period, then hit again — the check must rerun.
            await Task.Delay(TimeSpan.FromMilliseconds(1200), TestContext.Current.CancellationToken);
            await client.GetAsync("/health", TestContext.Current.CancellationToken);
            Assert.Equal(2, check.CallCount);
        }

        // Coverage for AddUnhealthyPolicy (the extension method itself, not just the underlying
        // HealthCheckPlusPolicyStatus record), through a real HTTP request.
        [Fact]
        public async Task GetHealth_ShouldRerunCheck_AfterUnhealthyPeriodElapses_ThroughRealHttpPipeline()
        {
            var check = new CountingCheck();

            using var host = await TestHost.CreateAsync(
                services =>
                {
                    services.AddLogging();
                    services.AddSingleton(check);
                    var ihb = services.AddHealthChecksPlus(["Test1"]);
                    ihb.AddCheckPlus<CountingCheck>("Test1");
                    ihb.AddUnhealthyPolicy("Test1", TimeSpan.FromSeconds(1));
                },
                app => app.UseHealthChecksPlus("/health"));

            var client = host.GetTestClient();

            await client.GetAsync("/health", TestContext.Current.CancellationToken);
            Assert.Equal(1, check.CallCount);

            var state = host.Services.GetRequiredService<IStateHealthChecksPlus>();
            state.SwitchToUnhealthy("Test1");

            await client.GetAsync("/health", TestContext.Current.CancellationToken);
            Assert.Equal(1, check.CallCount);

            await Task.Delay(TimeSpan.FromMilliseconds(1200), TestContext.Current.CancellationToken);
            await client.GetAsync("/health", TestContext.Current.CancellationToken);
            Assert.Equal(2, check.CallCount);
        }
    }
}
