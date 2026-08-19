// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using System.Diagnostics.Metrics;
using HealthCheckPlus.Abstractions;
using HealthCheckPlus.Internal;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace HealthCheckPlusTests
{
    // Tests for the check-related metrics recorded in CacheHealthCheckPlus.Update, the single
    // point every execution path (and the manual SwitchTo override) converges on.
    //
    // The "HealthCheckPlus" Meter is process-wide, and xUnit runs tests concurrently by default,
    // so every test here uses its own uniquely-named check and filters captured measurements by
    // that name — "Test1" (used by dozens of other tests in this suite) would otherwise pick up
    // unrelated concurrent tests' measurements.
    public class HealthCheckPlusMetricsTests
    {
        [Fact]
        public void Update_ShouldRecordExecutionAndDuration_WhenOriginIsNotSwitchTo()
        {
            const string checkName = nameof(Update_ShouldRecordExecutionAndDuration_WhenOriginIsNotSwitchTo);
            using var capture = new MetricsCapture();
            var cache = new CacheHealthCheckPlus();
            cache.InitCache([checkName]);
            cache.Running(checkName, true);

            var duration = TimeSpan.FromMilliseconds(250);
            cache.Update(checkName, HealthCheckTrigger.UrlRequest, new HealthCheckResult(HealthStatus.Degraded), DateTime.UtcNow, duration);

            var executions = capture.Measurements.Single(m =>
                m.InstrumentName == "healthcheckplus.check.executions" && (string?)m.Tags["healthcheckplus.check.name"] == checkName);
            Assert.Equal(1, executions.Value);
            Assert.Equal("Degraded", executions.Tags["healthcheckplus.check.status"]);
            Assert.Equal("UrlRequest", executions.Tags["healthcheckplus.check.origin"]);

            var durationMeasurement = capture.Measurements.Single(m =>
                m.InstrumentName == "healthcheckplus.check.duration" && (string?)m.Tags["healthcheckplus.check.name"] == checkName);
            Assert.Equal(duration.TotalSeconds, durationMeasurement.Value, precision: 6);
        }

        [Fact]
        public void Update_ShouldRecordStatusTransition_OnlyWhenStatusActuallyChanges()
        {
            const string checkName = nameof(Update_ShouldRecordStatusTransition_OnlyWhenStatusActuallyChanges);
            using var capture = new MetricsCapture();
            var cache = new CacheHealthCheckPlus();
            cache.InitCache([checkName]); // starts Healthy

            cache.Running(checkName, true);
            cache.Update(checkName, HealthCheckTrigger.UrlRequest, new HealthCheckResult(HealthStatus.Healthy), DateTime.UtcNow, TimeSpan.Zero);

            Assert.DoesNotContain(capture.Measurements, m =>
                m.InstrumentName == "healthcheckplus.check.status_transitions" && (string?)m.Tags["healthcheckplus.check.name"] == checkName);

            cache.Running(checkName, true);
            cache.Update(checkName, HealthCheckTrigger.UrlRequest, new HealthCheckResult(HealthStatus.Unhealthy), DateTime.UtcNow, TimeSpan.Zero);

            var transition = capture.Measurements.Single(m =>
                m.InstrumentName == "healthcheckplus.check.status_transitions" && (string?)m.Tags["healthcheckplus.check.name"] == checkName);
            Assert.Equal("Healthy", transition.Tags["healthcheckplus.check.previous_status"]);
            Assert.Equal("Unhealthy", transition.Tags["healthcheckplus.check.status"]);
        }

        // The manual override (IStateHealthChecksPlus.SwitchToUnhealthy/SwitchToDegraded) is not a
        // check execution — no code ran, no duration elapsed — but it is a real status change, so
        // it must still be visible via status_transitions while staying invisible to
        // executions/duration (which would otherwise misrepresent "the check ran" when it didn't).
        [Fact]
        public void SwitchToUnhealthy_ShouldRecordTransition_ButNotExecutionOrDuration()
        {
            const string checkName = nameof(SwitchToUnhealthy_ShouldRecordTransition_ButNotExecutionOrDuration);
            using var capture = new MetricsCapture();
            var cache = new CacheHealthCheckPlus();
            cache.InitCache([checkName]); // starts Healthy

            cache.SwitchToUnhealthy(checkName);

            Assert.DoesNotContain(capture.Measurements, m =>
                m.InstrumentName is "healthcheckplus.check.executions" or "healthcheckplus.check.duration"
                && (string?)m.Tags["healthcheckplus.check.name"] == checkName);

            var transition = capture.Measurements.Single(m =>
                m.InstrumentName == "healthcheckplus.check.status_transitions" && (string?)m.Tags["healthcheckplus.check.name"] == checkName);
            Assert.Equal("Healthy", transition.Tags["healthcheckplus.check.previous_status"]);
            Assert.Equal("Unhealthy", transition.Tags["healthcheckplus.check.status"]);
        }

        // A MeterListener measurement callback (e.g. a third-party OTel exporter) runs
        // synchronously/inline on the recording thread, so a bug in it can throw straight out of
        // Counter.Add/Histogram.Record. On the HTTP path
        // (DefaultHealthCheckServicePlus.CheckHealthPlusAsync has no try/catch around Update),
        // an unhandled throw here would turn an instrumentation bug into a 500 on /health.
        // Metrics must never be able to break health evaluation.
        [Fact]
        public void Update_ShouldNotThrow_AndShouldStillUpdateHealthState_WhenAMetricsListenerThrows()
        {
            const string checkName = nameof(Update_ShouldNotThrow_AndShouldStillUpdateHealthState_WhenAMetricsListenerThrows);
            var loggerProvider = new CapturingLoggerProvider();
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));
            var logger = loggerFactory.CreateLogger<CacheHealthCheckPlus>();

            // Only throws for measurements tagged with this test's own check name, so a
            // concurrently-running test's measurements for the process-wide "HealthCheckPlus"
            // Meter pass through unaffected.
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
            listener.SetMeasurementEventCallback<long>((_, _, tags, _) => ThrowIfTaggedForThisTest(tags, checkName));
            listener.SetMeasurementEventCallback<double>((_, _, tags, _) => ThrowIfTaggedForThisTest(tags, checkName));
            listener.Start();

            var cache = new CacheHealthCheckPlus(logger);
            cache.InitCache([checkName]); // starts Healthy
            cache.Running(checkName, true);

            cache.Update(checkName, HealthCheckTrigger.UrlRequest, new HealthCheckResult(HealthStatus.Unhealthy), DateTime.UtcNow, TimeSpan.FromMilliseconds(10));

            Assert.Equal(HealthStatus.Unhealthy, cache.StatusResult(checkName).Status);
            Assert.Contains(loggerProvider.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains(checkName, StringComparison.Ordinal));
        }

        private static void ThrowIfTaggedForThisTest(ReadOnlySpan<KeyValuePair<string, object?>> tags, string checkName)
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "healthcheckplus.check.name" && (string?)tag.Value == checkName)
                {
                    throw new InvalidOperationException("simulated metrics exporter failure");
                }
            }
        }

        // Update()'s guard clause (`_statusDeps.TryGetValue(key, out var item) && item.Running`)
        // must not silently drop the result — no log, no metric — whenever it fails, including the
        // reachable case of two overlapping executions of the same check (a known scheduling race
        // in ScheduleIfDue) where the second one to finish finds Running already cleared by the first.
        [Fact]
        public void Update_ShouldLogWarning_AndNotThrow_WhenNoExecutionWasMarkedRunning()
        {
            const string checkName = nameof(Update_ShouldLogWarning_AndNotThrow_WhenNoExecutionWasMarkedRunning);
            using var capture = new MetricsCapture();
            var loggerProvider = new CapturingLoggerProvider();
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));
            var logger = loggerFactory.CreateLogger<CacheHealthCheckPlus>();

            var cache = new CacheHealthCheckPlus(logger);
            cache.InitCache([checkName]); // starts Healthy, Running = false

            // Deliberately not calling cache.Running(checkName, true) first, to simulate the
            // overlapping-execution race: this call arrives after another already cleared Running.
            var exception = Record.Exception(() =>
                cache.Update(checkName, HealthCheckTrigger.UrlRequest, new HealthCheckResult(HealthStatus.Unhealthy), DateTime.UtcNow, TimeSpan.Zero));

            Assert.Null(exception);
            Assert.Equal(HealthStatus.Healthy, cache.StatusResult(checkName).Status); // result was dropped, as before
            Assert.Contains(loggerProvider.Entries, e =>
                e.Level == LogLevel.Warning && e.EventId.Name == "HealthCheckPlusUpdateDropped" && e.Message.Contains(checkName, StringComparison.Ordinal));
            Assert.Contains(capture.Measurements, m =>
                m.InstrumentName == "healthcheckplus.anomalies" && (string?)m.Tags["healthcheckplus.anomaly.reason"] == "update_result_dropped");
        }

        [Fact]
        public void Update_ShouldLogWarning_WhenKeyIsNotRegistered()
        {
            using var capture = new MetricsCapture();
            var loggerProvider = new CapturingLoggerProvider();
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));
            var logger = loggerFactory.CreateLogger<CacheHealthCheckPlus>();

            var cache = new CacheHealthCheckPlus(logger);
            cache.InitCache([]); // "UnknownCheck" is never registered

            var exception = Record.Exception(() =>
                cache.Update("UnknownCheck", HealthCheckTrigger.UrlRequest, new HealthCheckResult(HealthStatus.Unhealthy), DateTime.UtcNow, TimeSpan.Zero));

            Assert.Null(exception);
            Assert.Contains(loggerProvider.Entries, e =>
                e.Level == LogLevel.Warning && e.EventId.Name == "HealthCheckPlusUpdateDropped" && e.Message.Contains("UnknownCheck", StringComparison.Ordinal));
            Assert.Contains(capture.Measurements, m =>
                m.InstrumentName == "healthcheckplus.anomalies" && (string?)m.Tags["healthcheckplus.anomaly.reason"] == "update_result_dropped");
        }

        // SwithState (SwitchToUnhealthy/SwitchToDegraded) used to drop a manual override with zero
        // signal - no exception, no log, no metric - whenever the check was already Running (a
        // scheduled execution in flight). A consumer would see no error and reasonably assume the
        // override took effect. This is exactly the "no silent catch" pattern this project's own
        // doctrine forbids elsewhere.
        [Fact]
        public void SwitchToUnhealthy_ShouldLogWarning_AndRecordAnomaly_WhenCheckIsCurrentlyRunning()
        {
            const string checkName = nameof(SwitchToUnhealthy_ShouldLogWarning_AndRecordAnomaly_WhenCheckIsCurrentlyRunning);
            using var capture = new MetricsCapture();
            var loggerProvider = new CapturingLoggerProvider();
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));
            var logger = loggerFactory.CreateLogger<CacheHealthCheckPlus>();

            var cache = new CacheHealthCheckPlus(logger);
            cache.InitCache([checkName]); // starts Healthy, Running = false
            cache.Running(checkName, true); // simulate a scheduled execution currently in flight

            var exception = Record.Exception(() => cache.SwitchToUnhealthy(checkName));

            Assert.Null(exception);
            Assert.Equal(HealthStatus.Healthy, cache.StatusResult(checkName).Status); // override was dropped
            Assert.Contains(loggerProvider.Entries, e =>
                e.Level == LogLevel.Warning && e.EventId.Name == "HealthCheckPlusSwitchToDropped" && e.Message.Contains(checkName, StringComparison.Ordinal));
            Assert.Contains(capture.Measurements, m =>
                m.InstrumentName == "healthcheckplus.anomalies" && (string?)m.Tags["healthcheckplus.anomaly.reason"] == "switchto_dropped_while_running");
        }
    }
}
