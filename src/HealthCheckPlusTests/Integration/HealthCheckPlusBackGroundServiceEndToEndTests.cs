// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Internal;
using HealthCheckPlus.options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace HealthCheckPlusTests.Integration
{
    // End-to-end coverage for the background service and its publishing filters (PublishingOptions).
    public class HealthCheckPlusBackGroundServiceEndToEndTests
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

        private sealed class RecordingPublisher : IHealthCheckPublisher
        {
            public int PublishCount;

            public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref PublishCount);
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task BackgroundService_ShouldRerunPeriodically_AndPublishOnlyWhenReportChanges()
        {
            var check = new CountingCheck();
            var publisher = new RecordingPublisher();

            using var host = await TestHost.CreateAsync(
                services =>
                {
                    services.AddLogging();
                    services.AddSingleton(check);
                    services.AddSingleton<IHealthCheckPublisher>(publisher);

                    var ihb = services.AddHealthChecksPlus(["Test1"]);
                    ihb.AddCheckPlus<CountingCheck>("Test1");
                    ihb.AddBackgroundPolicy(opt =>
                    {
                        opt.Delay = TimeSpan.FromMilliseconds(100);
                        opt.Idle = TimeSpan.FromSeconds(1);
                        opt.AllStatusPeriod(TimeSpan.FromSeconds(1));
                        opt.Publishing = new PublishingOptions
                        {
                            AfterIdleCount = 1,
                            WhenReportChange = true
                        };
                    });
                },
                _ => { });

            // Give the background service a few idle cycles to run: enough for several reruns
            // (period 1s, idle 1s) but the status never changes (always Healthy), so with
            // WhenReportChange = true the publisher should fire once and then stay quiet.
            await Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.True(check.CallCount >= 2, $"Expected the background service to have rerun the check at least twice, got {check.CallCount}.");
            Assert.Equal(1, publisher.PublishCount);

            await host.StopAsync(TestContext.Current.CancellationToken);
        }

        // Direct, timing-independent regression test for the native HealthCheckPublisherHostedService
        // removal in AddBackgroundPolicy — the test above infers this indirectly from publish
        // counts within a timing window, which a fast/slow CI run could make inconclusive; this
        // asserts the DI wiring directly instead.
        [Fact]
        public async Task AddBackgroundPolicy_ShouldRemoveNativeHealthCheckPublisherHostedService()
        {
            using var host = await TestHost.CreateAsync(
                services =>
                {
                    services.AddLogging();
                    var ihb = services.AddHealthChecksPlus(["Test1"]);
                    ihb.AddCheckPlus<CountingCheck>("Test1");
                    ihb.AddBackgroundPolicy();
                },
                _ => { });

            var hostedServices = host.Services.GetServices<IHostedService>().ToArray();

            Assert.DoesNotContain(hostedServices, s => s.GetType().FullName == "Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckPublisherHostedService");
            Assert.Contains(hostedServices, s => s is HealthCheckPlusBackGroundService);

            await host.StopAsync(TestContext.Current.CancellationToken);
        }

        // healthcheckplus.check.executions/duration must carry check.origin=Background when the
        // execution is actually triggered by the background service, not just by a direct call to
        // CacheHealthCheckPlus.Update with HealthCheckTrigger.UrlRequest.
        [Fact]
        public async Task BackgroundService_ShouldRecordCheckExecutionMetrics_WithBackgroundOrigin()
        {
            const string checkName = nameof(BackgroundService_ShouldRecordCheckExecutionMetrics_WithBackgroundOrigin);
            using var capture = new MetricsCapture();

            using var host = await TestHost.CreateAsync(
                services =>
                {
                    services.AddLogging();
                    var ihb = services.AddHealthChecksPlus([checkName]);
                    ihb.AddCheckPlus<CountingCheck>(checkName);
                    ihb.AddBackgroundPolicy(opt =>
                    {
                        opt.Delay = TimeSpan.FromMilliseconds(100);
                        opt.Idle = TimeSpan.FromSeconds(1);
                        opt.AllStatusPeriod(TimeSpan.FromSeconds(1));
                    });
                },
                _ => { });

            await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            var executions = capture.Measurements
                .Where(m => m.InstrumentName == "healthcheckplus.check.executions" && (string?)m.Tags["healthcheckplus.check.name"] == checkName)
                .ToArray();

            Assert.NotEmpty(executions);
            Assert.All(executions, m => Assert.Equal("Background", m.Tags["healthcheckplus.check.origin"]));

            Assert.Contains(capture.Measurements, m =>
                m.InstrumentName == "healthcheckplus.check.duration" && (string?)m.Tags["healthcheckplus.check.name"] == checkName);
        }
    }
}
