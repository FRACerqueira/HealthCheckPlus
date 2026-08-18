// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Internal;
using HealthCheckPlus.options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

        // Registers a callback on its own cancellationToken that throws, and never completes on
        // its own - so it's still in flight, with that callback registered, whenever the host
        // shuts down. That mirrors how CancellationTokenSource.Cancel() can throw in production if
        // any consumer code registered a throwing callback anywhere on the cancellation chain a
        // background cycle's linked token is part of - not something this library's own code does,
        // but reachable from outside it.
        private sealed class ThrowingOnCancelCheck : IHealthCheck
        {
            public readonly ManualResetEventSlim Started = new(false);

            public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                cancellationToken.Register(() => throw new InvalidOperationException("simulated cancellation callback failure"));
                Started.Set();
                await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
                return HealthCheckResult.Healthy();
            }
        }

        private sealed class CapturingPublisher : IHealthCheckPublisher
        {
            public readonly List<HealthReport> Reports = [];

            public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
            {
                lock (Reports)
                {
                    Reports.Add(report);
                }
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

                    var ihb = services.AddHealthChecksPlus();
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
                    var ihb = services.AddHealthChecksPlus();
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
                    var ihb = services.AddHealthChecksPlus();
                    ihb.AddCheckPlus<CountingCheck>(checkName);
                    ihb.AddBackgroundPolicy(opt =>
                    {
                        opt.Delay = TimeSpan.FromMilliseconds(100);
                        opt.Idle = TimeSpan.FromSeconds(1);
                        opt.AllStatusPeriod(TimeSpan.FromSeconds(1));
                    });
                },
                _ => { });

            // A generous wait relative to the ~1.1s minimum cycle time: a CI runner slower than
            // this machine (observed in practice on windows-latest) can otherwise miss even the
            // single execution this test needs.
            await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
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

                    var ihb = services.AddHealthChecksPlus();
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

        // Regression test: FilterReportForPublishing (invoked only when building the report to hand
        // to publishers) calls the same consumer-supplied Predicate used to decide which checks
        // run - but that specific invocation used to sit completely outside any try/catch in the
        // background loop, unlike every other step here. A Predicate that throws while the report
        // is being built (as opposed to while filtering which checks run, which was already
        // guarded by the check-execution try/catch above) used to kill the loop permanently and
        // silently: checks stopped running, publishers stopped firing, with no log or metric at
        // all. The Predicate below alternates true/throw so the "which checks run" evaluation
        // (once per cycle, with a single registration) always succeeds while the later "report to
        // publish" evaluation (also once per cycle, over the same single registration) always
        // throws - isolating the fix under test from the already-guarded check-execution path.
        [Fact]
        public async Task BackgroundService_ShouldKeepRunning_WhenThePredicateThrowsWhileBuildingTheReportToPublish()
        {
            var check = new CountingCheck();
            var publisher = new RecordingPublisher();
            var predicateCallCount = 0;

            using var host = await TestHost.CreateAsync(
                services =>
                {
                    services.AddLogging();
                    services.AddSingleton(check);
                    services.AddSingleton<IHealthCheckPublisher>(publisher);

                    var ihb = services.AddHealthChecksPlus();
                    ihb.AddCheckPlus<CountingCheck>("Test1");
                    ihb.AddBackgroundPolicy(opt =>
                    {
                        opt.Delay = TimeSpan.FromMilliseconds(50);
                        opt.Idle = TimeSpan.FromSeconds(1);
                        opt.AllStatusPeriod(TimeSpan.FromSeconds(1));
                        opt.Predicate = _ =>
                        {
                            var call = Interlocked.Increment(ref predicateCallCount);
                            if (call % 2 == 0)
                            {
                                throw new InvalidOperationException("simulated Predicate failure while building the report to publish");
                            }
                            return true;
                        };
                        opt.Publishing = new PublishingOptions { AfterIdleCount = 1, WhenReportChange = false };
                    });
                },
                _ => { });

            // A generous wait relative to the ~1s minimum cycle time - see the CI-timing note on
            // the sibling "keep running" test above.
            await Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            Assert.True(check.CallCount >= 2,
                $"Expected the background service to keep rerunning checks across multiple cycles despite the Predicate throwing while building the report to publish, got {check.CallCount}.");
            Assert.Equal(0, publisher.PublishCount);
        }

        // Regression test: HealthCheckPlusBackGroundOptions.Predicate is used to decide which
        // checks the background service *runs* (see BackGroudCheckHealthPlusAsync), but the report
        // it hashes (for WhenReportChange) and hands to publishers used to come straight from
        // CacheHealthCheckPlus.CreateReport(), which has no notion of that predicate and always
        // includes every check tracked in the cache - including one the predicate excludes from
        // ever running, which therefore never leaves its InitCache seed status (Healthy). A
        // predicate-excluded check should never appear in what the background service publishes at
        // all, correct or not, since it's explicitly outside what this background service manages.
        [Fact]
        public async Task BackgroundService_ShouldExcludePredicateFilteredChecks_FromThePublishedReport()
        {
            var publisher = new CapturingPublisher();

            using var host = await TestHost.CreateAsync(
                services =>
                {
                    services.AddLogging();
                    services.AddSingleton<IHealthCheckPublisher>(publisher);

                    var ihb = services.AddHealthChecksPlus();
                    ihb.AddCheckPlus<CountingCheck>("Included");
                    ihb.AddCheckPlus<CountingCheck>("Excluded");
                    ihb.AddBackgroundPolicy(opt =>
                    {
                        opt.Delay = TimeSpan.FromMilliseconds(50);
                        opt.Idle = TimeSpan.FromSeconds(1);
                        opt.AllStatusPeriod(TimeSpan.FromSeconds(1));
                        opt.Predicate = r => r.Name == "Included";
                        opt.Publishing = new PublishingOptions { AfterIdleCount = 1, WhenReportChange = false };
                    });
                },
                _ => { });

            // A generous wait relative to the ~1s minimum cycle time: a CI runner slower than this
            // machine (observed in practice on windows-latest) can otherwise miss even the single
            // publish cycle this test needs.
            await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            Assert.NotEmpty(publisher.Reports);
            Assert.All(publisher.Reports, report =>
            {
                Assert.Contains("Included", report.Entries.Keys);
                Assert.DoesNotContain("Excluded", report.Entries.Keys);
            });
        }

        // Regression test: a check that hasn't run even once yet (its own Delay, from AddCheckPlus,
        // is longer than this cycle's Delay+Idle - the README's own example values, 30s vs. 5s,
        // trigger this on the very first cycle) used to be published as a genuine Healthy result,
        // because CreateReport() has no way to distinguish InitCache's seed (Healthy, Origin=None)
        // from a real observation. A not-yet-run check must be excluded from what the background
        // service publishes, the same way a Predicate-excluded one already is.
        [Fact]
        public async Task BackgroundService_ShouldExcludeNotYetRunChecks_FromThePublishedReport()
        {
            var publisher = new CapturingPublisher();

            using var host = await TestHost.CreateAsync(
                services =>
                {
                    services.AddLogging();
                    services.AddSingleton<IHealthCheckPublisher>(publisher);

                    var ihb = services.AddHealthChecksPlus();
                    ihb.AddCheckPlus<CountingCheck>("HasRun");
                    ihb.AddCheckPlus<CountingCheck>("NeverRun", delay: TimeSpan.FromSeconds(1000));
                    ihb.AddBackgroundPolicy(opt =>
                    {
                        opt.Delay = TimeSpan.FromMilliseconds(50);
                        opt.Idle = TimeSpan.FromSeconds(1);
                        opt.AllStatusPeriod(TimeSpan.FromSeconds(1));
                        opt.Publishing = new PublishingOptions { AfterIdleCount = 1, WhenReportChange = false };
                    });
                },
                _ => { });

            // A generous wait relative to the ~1s minimum cycle time: a CI runner slower than this
            // machine (observed in practice on windows-latest) can otherwise miss even the single
            // publish cycle this test needs.
            await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            Assert.NotEmpty(publisher.Reports);
            Assert.All(publisher.Reports, report =>
            {
                Assert.Contains("HasRun", report.Entries.Keys);
                Assert.DoesNotContain("NeverRun", report.Entries.Keys);
            });
        }

        // Regression test: StopAsync used to swallow an exception from _stopping.Cancel() with a
        // fully empty catch block - no log, no metric, nothing - which is exactly the pattern this
        // project's own rule forbids (see docs/ARCHITECTURE.md's logging-and-anomalies section).
        // CancellationTokenSource.Cancel() can throw if any callback registered anywhere on the
        // cancellation chain a background cycle's linked token belongs to itself throws; this
        // drives a real shutdown while a check with such a callback is in flight to reproduce it.
        [Fact]
        public async Task StopAsync_ShouldLogWarning_WhenCancellingTheStoppingTokenThrows()
        {
            var check = new ThrowingOnCancelCheck();
            var loggerProvider = new CapturingLoggerProvider();

            using var host = await TestHost.CreateAsync(
                services =>
                {
                    services.AddLogging(builder => builder.AddProvider(loggerProvider));
                    services.AddSingleton(check);

                    var ihb = services.AddHealthChecksPlus();
                    ihb.AddCheckPlus<ThrowingOnCancelCheck>("Test1");
                    ihb.AddBackgroundPolicy(opt =>
                    {
                        opt.Delay = TimeSpan.Zero;
                        opt.Idle = TimeSpan.FromSeconds(1);
                        opt.AllStatusPeriod(TimeSpan.FromSeconds(1));
                    });
                },
                _ => { });

            Assert.True(check.Started.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken), "The check never started.");

            await host.StopAsync(TestContext.Current.CancellationToken);

            Assert.Contains(loggerProvider.Entries, e => e.Level == LogLevel.Warning
                && e.EventId.Name == "HealthCheckPlusBackGroundStopCancellationError"
                && e.Exception != null);
        }
    }
}
