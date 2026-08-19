// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using System.Diagnostics.Metrics;
using HealthCheckPlus.Abstractions;
using HealthCheckPlus.Options;
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

        private sealed class ConditionThrowingPublisher : IHealthCheckPlusPublisher
        {
            public Func<HealthReport, bool>? PublisherCondition { get; set; } =
                _ => throw new InvalidOperationException("simulated PublisherCondition failure");

            public Task PublishAsync(HealthReport report, CancellationToken cancellationToken) => Task.CompletedTask;
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

        private sealed class MetricsThrowingTestPublisher : IHealthCheckPublisher
        {
            public Task PublishAsync(HealthReport report, CancellationToken cancellationToken) => Task.CompletedTask;
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

                    var ihb = services.AddHealthChecksPlus();
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
            // ones are filtered by WhenReportChange. Poll for both outcomes actually showing up,
            // rather than a fixed delay - see TestHost.WaitUntilAsync's own comment for why a
            // fixed sleep here turned out not to be reliable under this project's own
            // full-solution, 3-TFM-parallel test run.
            //
            // The "HealthCheckPlus" Meter is process-wide, and xUnit runs test classes/methods
            // concurrently by default, so other tests' publishers can emit measurements while this
            // one is capturing. Filter by this test's own publisher type (a distinct concrete type
            // per test in this file) rather than asserting over every captured measurement.
            CapturedMeasurement[] Invocations() => capture.Measurements
                .Where(m => m.InstrumentName == "healthcheckplus.publisher.invocations" && (string?)m.Tags["healthcheckplus.publisher.type"] == typeof(NoopPublisher).FullName)
                .ToArray();

            await TestHost.WaitUntilAsync(
                () => Invocations().Any(m => (string?)m.Tags["healthcheckplus.publisher.result"] == "published")
                    && Invocations().Any(m => (string?)m.Tags["healthcheckplus.publisher.result"] == "skipped_no_change"),
                TimeSpan.FromSeconds(30),
                "Expected both a 'published' and a 'skipped_no_change' invocation measurement.",
                TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            var invocations = Invocations();
            Assert.Contains(invocations, m => (string?)m.Tags["healthcheckplus.publisher.result"] == "published");
            Assert.Contains(invocations, m => (string?)m.Tags["healthcheckplus.publisher.result"] == "skipped_no_change");

            // Not .Single(): "Test1" can legitimately miss the very first idle cycle if it hasn't
            // run yet by then (see BackgroundService_ShouldExcludeNotYetRunChecks_FromThePublishedReport),
            // in which case that cycle publishes an empty report and the next cycle - once "Test1"
            // has actually run - legitimately publishes again because the report genuinely changed,
            // producing a second "published" duration measurement here. Both are real publishes;
            // asserting "at least one" is what this test actually needs to verify.
            var publishedDurations = capture.Measurements.Where(m =>
                m.InstrumentName == "healthcheckplus.publisher.duration" && (string?)m.Tags["healthcheckplus.publisher.type"] == typeof(NoopPublisher).FullName).ToArray();
            Assert.NotEmpty(publishedDurations);
            Assert.All(publishedDurations, m => Assert.Equal(typeof(NoopPublisher).FullName, m.Tags["healthcheckplus.publisher.type"]));
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

                    var ihb = services.AddHealthChecksPlus();
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

            foreach (var publisherType in new[] { typeof(NoopPublisherA).FullName, typeof(NoopPublisherB).FullName })
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

                    var ihb = services.AddHealthChecksPlus();
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

            // A generous wait relative to the ~1.1s minimum cycle time (Delay + Idle): a CI runner
            // slower than this machine (observed in practice on windows-latest) can otherwise miss
            // even the single cycle this test needs.
            await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            // See the comment in the test above about why filtering by this test's own publisher
            // type is necessary — the Meter is process-wide and tests run concurrently.
            var invocations = capture.Measurements
                .Where(m => m.InstrumentName == "healthcheckplus.publisher.invocations" && (string?)m.Tags["healthcheckplus.publisher.type"] == typeof(ConditionalPublisher).FullName)
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

                    var ihb = services.AddHealthChecksPlus();
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

            // A generous wait relative to the 1s cycle: this test's own full-suite runs (3 target
            // frameworks plus xUnit's own parallelization, all driving several TestHost instances
            // at once) showed this flake intermittently at 3s under heavy contention, with only 1
            // cycle observed instead of the several expected.
            await Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            var invocations = capture.Measurements
                .Where(m => m.InstrumentName == "healthcheckplus.publisher.invocations" && (string?)m.Tags["healthcheckplus.publisher.type"] == typeof(ThrowingPublisher).FullName)
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

        // Regression test: a throwing IHealthCheckPlusPublisher.PublisherCondition used to be
        // evaluated outside RunPublisherAsync's own try/catch - so it was only ever caught one
        // level up, by the generic cycle-dispatch catch (HealthCheckPublisherCycleError /
        // publisher_cycle_failed_but_continued), which names neither the publisher nor that its
        // PublisherCondition specifically was the cause. It must now be attributed to this exact
        // publisher, the same way a throwing PublishAsync already is.
        [Fact]
        public async Task BackgroundService_ShouldAttributeFailureToThePublisher_WhenItsPublisherConditionThrows()
        {
            using var capture = new MetricsCapture();
            var loggerProvider = new CapturingLoggerProvider();

            using var host = await TestHost.CreateAsync(
                services =>
                {
                    services.AddLogging(builder => builder.AddProvider(loggerProvider));
                    services.AddSingleton<IHealthCheckPublisher, ConditionThrowingPublisher>();

                    var ihb = services.AddHealthChecksPlus();
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

            await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            var invocations = capture.Measurements
                .Where(m => m.InstrumentName == "healthcheckplus.publisher.invocations" && (string?)m.Tags["healthcheckplus.publisher.type"] == typeof(ConditionThrowingPublisher).FullName)
                .ToArray();

            Assert.Contains(invocations, m => (string?)m.Tags["healthcheckplus.publisher.result"] == "error");

            Assert.Contains(loggerProvider.Entries, e => e.EventId.Name == "HealthCheckPublisherError"
                && e.Message.Contains(typeof(ConditionThrowingPublisher).Name, StringComparison.Ordinal));
        }

        // Regression test: _hashlaststatus used to be committed to the new report's hash *before*
        // dispatching publishers, not after they actually succeeded. With WhenReportChange enabled,
        // a publish attempt that failed still left the cache believing "this status was already
        // published" - the very next cycle's SameReport check would then match (the report itself
        // never changed) and skip retrying entirely, permanently losing the notification for that
        // status until it changed again. This check's status never changes here, so the *only* way
        // more than one publish attempt can happen with WhenReportChange = true is if a failed
        // attempt is retried instead of being mistaken for "no change".
        [Fact]
        public async Task BackgroundService_ShouldRetryPublishing_WhenThePreviousAttemptFailed()
        {
            var loggerProvider = new CapturingLoggerProvider();

            using var host = await TestHost.CreateAsync(
                services =>
                {
                    services.AddLogging(builder => builder.AddProvider(loggerProvider));
                    services.AddSingleton<IHealthCheckPublisher, ThrowingPublisher>();

                    var ihb = services.AddHealthChecksPlus();
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

            // Poll for the actual condition instead of a fixed delay - see TestHost.WaitUntilAsync's
            // own comment for why a fixed sleep here turned out not to be reliable under this
            // project's own full-solution, 3-TFM-parallel test run.
            int ErrorLogCount() => loggerProvider.Entries.Count(e => e.EventId.Name == "HealthCheckPublisherError");
            await TestHost.WaitUntilAsync(
                () => ErrorLogCount() > 1,
                TimeSpan.FromSeconds(30),
                "Expected more than one publish attempt despite WhenReportChange and an unchanging report (a failed attempt must be retried, not mistaken for 'no change').",
                TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            var errorLogCount = ErrorLogCount();
            Assert.True(errorLogCount > 1,
                $"Expected more than one publish attempt despite WhenReportChange and an unchanging report (a failed attempt must be retried, not mistaken for 'no change'), got {errorLogCount}.");
        }

        // Regression test: RecordPublisherInvocation's "published" call in RunPublisherAsync used
        // to be the last statement inside its own try block, with no guard of its own - a throwing
        // MeterListener there was caught by the *publisher-failure* catch below it, misattributing
        // an instrumentation bug as the publisher itself having thrown (wrong HealthCheckPublisherError
        // log, wrong "error" metric) instead of surfacing it as what it actually is.
        [Fact]
        public async Task BackgroundService_ShouldNotMisattributeFailure_WhenAMetricsListenerThrowsRecordingAPublishedInvocation()
        {
            var check = new CountingCheck();
            var loggerProvider = new CapturingLoggerProvider();

            // Only throws for this test's own publisher type recording a "published" result, so a
            // concurrently-running test's measurements for the process-wide "HealthCheckPlus" Meter
            // pass through unaffected.
            using var listener = new MeterListener
            {
                InstrumentPublished = (instrument, l) =>
                {
                    if (instrument.Meter.Name == "HealthCheckPlus")
                    {
                        l.EnableMeasurementEvents(instrument);
                    }
                }
            };
            listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) => ThrowIfPublishedInvocationForThisTest(instrument.Name, tags));
            listener.Start();

            using var host = await TestHost.CreateAsync(
                services =>
                {
                    services.AddLogging(builder => builder.AddProvider(loggerProvider));
                    services.AddSingleton(check);
                    services.AddSingleton<IHealthCheckPublisher, MetricsThrowingTestPublisher>();

                    var ihb = services.AddHealthChecksPlus();
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

            // A generous wait relative to the ~1.1s minimum cycle time: this needs more than one
            // cycle to observe, and a CI runner slower than this machine (observed in practice on
            // windows-latest) can otherwise leave only one cycle within a tighter window.
            await Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            Assert.True(check.CallCount > 1,
                $"Expected the background loop to keep running checks despite the throwing metrics listener, got {check.CallCount}.");

            Assert.DoesNotContain(loggerProvider.Entries, e => e.EventId.Name == "HealthCheckPublisherError");
            Assert.Contains(loggerProvider.Entries, e => e.EventId.Name == "HealthCheckPlusPublisherMetricsRecordingError" && e.Level == LogLevel.Warning);
        }

        private sealed class NamespaceOne
        {
            public sealed class SameNamePublisher : IHealthCheckPublisher
            {
                public Task PublishAsync(HealthReport report, CancellationToken cancellationToken) => Task.CompletedTask;
            }
        }

        private sealed class NamespaceTwo
        {
            public sealed class SameNamePublisher : IHealthCheckPublisher
            {
                public Task PublishAsync(HealthReport report, CancellationToken cancellationToken) => Task.CompletedTask;
            }
        }

        // Regression test: publisher metrics were keyed by GetType().Name (the short class name),
        // so two publishers with the same class name in different namespaces collapsed into the
        // same "healthcheckplus.publisher.type" tag value, silently merging their measurements into
        // one series.
        [Fact]
        public async Task BackgroundService_ShouldNotCollidePublisherMetrics_ForSameShortTypeNameInDifferentNamespaces()
        {
            using var capture = new MetricsCapture();

            using var host = await TestHost.CreateAsync(
                services =>
                {
                    services.AddLogging();
                    services.AddSingleton<IHealthCheckPublisher, NamespaceOne.SameNamePublisher>();
                    services.AddSingleton<IHealthCheckPublisher, NamespaceTwo.SameNamePublisher>();

                    var ihb = services.AddHealthChecksPlus();
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

            // A generous wait relative to the ~1.1s minimum cycle time: a CI runner slower than
            // this machine (observed in practice on windows-latest) can otherwise miss even the
            // single cycle this test needs.
            await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            var publisherTypeTags = capture.Measurements
                .Where(m => m.InstrumentName == "healthcheckplus.publisher.invocations"
                    && ((string?)m.Tags["healthcheckplus.publisher.type"])?.Contains("SameNamePublisher", StringComparison.Ordinal) == true)
                .Select(m => (string?)m.Tags["healthcheckplus.publisher.type"])
                .Distinct()
                .ToArray();

            Assert.Equal(2, publisherTypeTags.Length);
        }

        private static void ThrowIfPublishedInvocationForThisTest(string instrumentName, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            if (instrumentName != "healthcheckplus.publisher.invocations")
            {
                return;
            }

            string? publisherType = null;
            string? result = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "healthcheckplus.publisher.type")
                {
                    publisherType = (string?)tag.Value;
                }
                if (tag.Key == "healthcheckplus.publisher.result")
                {
                    result = (string?)tag.Value;
                }
            }

            if (publisherType == typeof(MetricsThrowingTestPublisher).FullName && result == "published")
            {
                throw new InvalidOperationException("simulated metrics exporter failure");
            }
        }
    }
}
