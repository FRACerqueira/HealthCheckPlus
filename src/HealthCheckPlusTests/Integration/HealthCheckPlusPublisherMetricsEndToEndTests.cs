// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Abstractions;
using HealthCheckPlus.options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace HealthCheckPlusTests.Integration
{
    // End-to-end coverage for the publisher metrics, exercised through a real background service
    // run rather than by calling internal methods directly.
    public class HealthCheckPlusPublisherMetricsEndToEndTests
    {
        private sealed class AlwaysHealthyCheck : IHealthCheck
        {
            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(HealthCheckResult.Healthy());
            }
        }

        private sealed class NoopPublisher : IHealthCheckPublisher
        {
            public Task PublishAsync(HealthReport report, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private sealed class NoopPublisherA : IHealthCheckPublisher
        {
            public Task PublishAsync(HealthReport report, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private sealed class NoopPublisherB : IHealthCheckPublisher
        {
            public Task PublishAsync(HealthReport report, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private sealed class ConditionalPublisher : IHealthCheckPlusPublisher
        {
            public Func<HealthReport, bool>? PublisherCondition { get; set; } = _ => false;

            public Task PublishAsync(HealthReport report, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private sealed class ThrowingPublisher : IHealthCheckPublisher
        {
            public Task PublishAsync(HealthReport report, CancellationToken cancellationToken) =>
                throw new InvalidOperationException("simulated publisher failure");
        }

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
        public async Task BackgroundService_ShouldRecordPublished_ThenSkippedNoChange_AsReportStaysTheSame()
        {
            using var capture = new MetricsCapture();

            using var host = await TestHost.CreateAsync(
                services =>
                {
                    services.AddLogging();
                    services.AddSingleton<IHealthCheckPublisher, NoopPublisher>();

                    var ihb = services.AddHealthChecksPlus(["Test1"]);
                    ihb.AddCheckPlus<AlwaysHealthyCheck>("Test1");
                    ihb.AddBackgroundPolicy(opt =>
                    {
                        opt.Delay = TimeSpan.FromMilliseconds(100);
                        opt.Idle = TimeSpan.FromSeconds(1);
                        opt.AllStatusPeriod(TimeSpan.FromSeconds(1));
                        opt.Publishing = new PublishingOptions { AfterIdleCount = 1, WhenReportChange = true };
                    });
                },
                _ => { });

            // Status never changes (always Healthy): the first idle cycle publishes, subsequent
            // ones are filtered by WhenReportChange.
            await Task.Delay(TimeSpan.FromSeconds(4), TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            // The "HealthCheckPlus" Meter is process-wide, and xUnit runs test classes/methods
            // concurrently by default, so other tests' publishers can emit measurements while this
            // one is capturing. Filter by this test's own publisher type (a distinct concrete type
            // per test in this file) rather than asserting over every captured measurement.
            var invocations = capture.Measurements
                .Where(m => m.InstrumentName == "healthcheckplus.publisher.invocations" && (string?)m.Tags["healthcheckplus.publisher.type"] == nameof(NoopPublisher))
                .ToArray();

            Assert.Contains(invocations, m => (string?)m.Tags["healthcheckplus.publisher.result"] == "published");
            Assert.Contains(invocations, m => (string?)m.Tags["healthcheckplus.publisher.result"] == "skipped_no_change");

            var publishedDuration = capture.Measurements.Single(m =>
                m.InstrumentName == "healthcheckplus.publisher.duration" && (string?)m.Tags["healthcheckplus.publisher.type"] == nameof(NoopPublisher));
            Assert.Equal(nameof(NoopPublisher), publishedDuration.Tags["healthcheckplus.publisher.type"]);
        }

        // The WhenReportChange skip branch's `foreach (var publisher in _publishers)` loop
        // (HealthCheckPlusBackGroundService.cs) must record its own skipped_no_change measurement
        // for each of N registered publishers, not just the first/last one.
        [Fact]
        public async Task BackgroundService_ShouldRecordSkippedNoChange_ForEveryRegisteredPublisher_WhenMultiplePublishersAreRegistered()
        {
            using var capture = new MetricsCapture();

            using var host = await TestHost.CreateAsync(
                services =>
                {
                    services.AddLogging();
                    services.AddSingleton<IHealthCheckPublisher, NoopPublisherA>();
                    services.AddSingleton<IHealthCheckPublisher, NoopPublisherB>();

                    var ihb = services.AddHealthChecksPlus(["Test1"]);
                    ihb.AddCheckPlus<AlwaysHealthyCheck>("Test1");
                    ihb.AddBackgroundPolicy(opt =>
                    {
                        opt.Delay = TimeSpan.FromMilliseconds(100);
                        opt.Idle = TimeSpan.FromSeconds(1);
                        opt.AllStatusPeriod(TimeSpan.FromSeconds(1));
                        opt.Publishing = new PublishingOptions { AfterIdleCount = 1, WhenReportChange = true };
                    });
                },
                _ => { });

            await Task.Delay(TimeSpan.FromSeconds(4), TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            var invocations = capture.Measurements
                .Where(m => m.InstrumentName == "healthcheckplus.publisher.invocations")
                .ToArray();

            foreach (var publisherType in new[] { nameof(NoopPublisherA), nameof(NoopPublisherB) })
            {
                var thisPublisherInvocations = invocations
                    .Where(m => (string?)m.Tags["healthcheckplus.publisher.type"] == publisherType)
                    .ToArray();

                Assert.Contains(thisPublisherInvocations, m => (string?)m.Tags["healthcheckplus.publisher.result"] == "published");
                Assert.Contains(thisPublisherInvocations, m => (string?)m.Tags["healthcheckplus.publisher.result"] == "skipped_no_change");
            }
        }

        [Fact]
        public async Task BackgroundService_ShouldRecordSkippedCondition_WhenPublisherConditionReturnsFalse()
        {
            using var capture = new MetricsCapture();

            using var host = await TestHost.CreateAsync(
                services =>
                {
                    services.AddLogging();
                    services.AddSingleton<IHealthCheckPublisher, ConditionalPublisher>();

                    var ihb = services.AddHealthChecksPlus(["Test1"]);
                    ihb.AddCheckPlus<AlwaysHealthyCheck>("Test1");
                    ihb.AddBackgroundPolicy(opt =>
                    {
                        opt.Delay = TimeSpan.FromMilliseconds(100);
                        opt.Idle = TimeSpan.FromSeconds(1);
                        opt.AllStatusPeriod(TimeSpan.FromSeconds(1));
                        opt.Publishing = new PublishingOptions { AfterIdleCount = 1, WhenReportChange = false };
                    });
                },
                _ => { });

            await Task.Delay(TimeSpan.FromSeconds(1.5), TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            // See the comment in the test above about why filtering by this test's own publisher
            // type is necessary — the Meter is process-wide and tests run concurrently.
            var invocations = capture.Measurements
                .Where(m => m.InstrumentName == "healthcheckplus.publisher.invocations" && (string?)m.Tags["healthcheckplus.publisher.type"] == nameof(ConditionalPublisher))
                .ToArray();

            Assert.Contains(invocations, m => (string?)m.Tags["healthcheckplus.publisher.result"] == "skipped_condition");
            Assert.DoesNotContain(invocations, m => (string?)m.Tags["healthcheckplus.publisher.result"] == "published");
        }

        // "error" is one of the four possible healthcheckplus.publisher.invocations results — this
        // exercises a publisher that actually throws to prove it's recorded.
        [Fact]
        public async Task BackgroundService_ShouldRecordError_WhenPublisherThrows()
        {
            using var capture = new MetricsCapture();
            var check = new CountingCheck();
            var loggerProvider = new CapturingLoggerProvider();

            using var host = await TestHost.CreateAsync(
                services =>
                {
                    services.AddLogging(builder => builder.AddProvider(loggerProvider));
                    services.AddSingleton(check);
                    services.AddSingleton<IHealthCheckPublisher, ThrowingPublisher>();

                    var ihb = services.AddHealthChecksPlus(["Test1"]);
                    ihb.AddCheckPlus<CountingCheck>("Test1");
                    ihb.AddBackgroundPolicy(opt =>
                    {
                        opt.Delay = TimeSpan.FromMilliseconds(100);
                        opt.Idle = TimeSpan.FromSeconds(1);
                        opt.AllStatusPeriod(TimeSpan.FromSeconds(1));
                        opt.Publishing = new PublishingOptions { AfterIdleCount = 1, WhenReportChange = false };
                    });
                },
                _ => { });

            await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            var invocations = capture.Measurements
                .Where(m => m.InstrumentName == "healthcheckplus.publisher.invocations" && (string?)m.Tags["healthcheckplus.publisher.type"] == nameof(ThrowingPublisher))
                .ToArray();

            Assert.Contains(invocations, m => (string?)m.Tags["healthcheckplus.publisher.result"] == "error");

            // RunPublisherAsync rethrows after recording, and the call site
            // (await Task.WhenAll(tasks) in CheckHealthAsync) must catch it — an uncaught exception
            // there would fault the background loop's Task permanently, silently ending all future
            // checks AND publishes for the rest of the process lifetime. A failing publisher (this
            // one throws on every cycle) must not stop the check from continuing to run.
            Assert.True(check.CallCount > 1, $"Expected the background loop to keep running checks after a publisher failure, got {check.CallCount}.");

            var errorCount = invocations.Count(m => (string?)m.Tags["healthcheckplus.publisher.result"] == "error");
            Assert.True(errorCount > 1, $"Expected more than one publish attempt (and failure) across the wait window, got {errorCount}.");

            // The metric and the per-publisher log entry aren't enough on their own: an operator
            // reading logs needs an explicit signal that the background loop is still alive after
            // the failure, not just a record that a publisher failed once.
            Assert.Contains(loggerProvider.Entries, e => e.EventId.Name == "HealthCheckPublisherCycleError" && e.Level == LogLevel.Warning);

            // Same anomaly, also as a counted metric, so an operator can alert on rate/trend, not
            // just grep logs.
            Assert.Contains(capture.Measurements, m =>
                m.InstrumentName == "healthcheckplus.anomalies" && (string?)m.Tags["healthcheckplus.anomaly.reason"] == "publisher_cycle_failed_but_continued");
        }
    }
}
