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

        // Never completes on its own - it only stops when its cancellationToken is cancelled,
        // simulating a publisher with no timeout of its own (e.g. an HTTP call to an endpoint that
        // never responds).
        private sealed class HangingPublisher : IHealthCheckPublisher
        {
            public int InvocationCount;

            public async Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref InvocationCount);
                await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
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

        // Regression test: HealthCheckPlusBackGroundOptions.Timeout used to only bound the
        // check-execution phase of each cycle, not publisher dispatch - so a publisher with no
        // timeout of its own that never completes froze the entire background loop (checks
        // included, not just publishing) forever, since nothing else ever cancelled it. This drives
        // a real host with a publisher that only stops when cancelled, and expects both checks and
        // publish attempts to keep happening across several cycles despite every single one hanging
        // until the per-cycle Timeout cuts it off.
        [Fact]
        public async Task BackgroundService_ShouldKeepRunning_WhenAPublisherHangsPastTheCycleTimeout()
        {
            var check = new CountingCheck();
            var publisher = new HangingPublisher();

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
                        // Idle, Timeout and *Period all reject values under 1 second, so this is as
                        // fast as the cycle can be driven - each cycle is roughly Timeout (the
                        // hanging publisher is only ever cut off once it elapses) plus Idle.
                        opt.Delay = TimeSpan.FromMilliseconds(50);
                        opt.Timeout = TimeSpan.FromSeconds(1);
                        opt.Idle = TimeSpan.FromSeconds(1);
                        opt.AllStatusPeriod(TimeSpan.FromSeconds(1));
                        opt.Publishing = new PublishingOptions
                        {
                            AfterIdleCount = 1,
                            WhenReportChange = false
                        };
                    });
                },
                _ => { });

            await Task.Delay(TimeSpan.FromSeconds(9), TestContext.Current.CancellationToken);

            Assert.True(publisher.InvocationCount >= 3,
                $"Expected the hanging publisher to have been invoked at least 3 times as the loop recovered from each timeout, got {publisher.InvocationCount}.");
            Assert.True(check.CallCount >= 3,
                $"Expected the background service to keep rerunning checks across multiple cycles despite the hanging publisher, got {check.CallCount}.");

            await host.StopAsync(TestContext.Current.CancellationToken);
        }
    }
}
