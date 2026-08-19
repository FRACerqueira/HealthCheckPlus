// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using System.Diagnostics;
using System.Diagnostics.Metrics;
using HealthCheckPlus.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlus.Internal
{
    // Instrument names/tags are a public contract once shipped, so treat any change here as a
    // breaking change. Uses System.Diagnostics.Metrics directly (no OpenTelemetry SDK dependency)
    // so any exporter (OTel, Prometheus, App Insights, ...) can consume it via Meter name
    // "HealthCheckPlus", matching the project's zero-external-dependency rule for the main package
    // (CONTRIBUTING.md).
    // Closed set of "healthcheckplus.publisher.result" tag values — an enum here, mapped to its
    // wire-format string only inside RecordPublisherInvocation, keeps call sites from ever emitting
    // an undocumented value.
    internal enum PublisherInvocationResult
    {
        Published,
        SkippedNoChange,
        SkippedCondition,
        Error
    }

    // Closed set of "healthcheckplus.anomalies" tag values. Every reason except LoggingSinkFailed
    // is an internal defensive path that was handled without failing the caller - logged as
    // Warning at the point it happens (with the specific exception/check name) *and* counted
    // here, so an operator can alert on rate/trend instead of only discovering it by reading logs
    // after the fact. LoggingSinkFailed is the deliberate exception to that pairing: it exists
    // precisely because the log call itself is what failed, so it is metric-only by design - see
    // RecordAnomaly's own comment below.
    internal enum AnomalyReason
    {
        AdoptedCheckDisposeFailed,
        PublisherCycleFailedButContinued,
        UpdateResultDropped,
        CheckExecutionAborted,
        PublishReportBuildFailed,
        SwitchToDroppedWhileRunning,
        LoggingSinkFailed
    }

    internal static class HealthCheckPlusMetrics
    {
        private static readonly Meter Meter = new("HealthCheckPlus", typeof(HealthCheckPlusMetrics).Assembly.GetName().Version?.ToString());

        private static readonly Histogram<double> CheckDuration = Meter.CreateHistogram<double>(
            "healthcheckplus.check.duration", unit: "s", description: "Duration of a health check execution.");

        private static readonly Counter<long> CheckExecutions = Meter.CreateCounter<long>(
            "healthcheckplus.check.executions", description: "Number of health check executions.");

        private static readonly Counter<long> CheckStatusTransitions = Meter.CreateCounter<long>(
            "healthcheckplus.check.status_transitions", description: "Number of times a health check's status changed.");

        private static readonly Counter<long> PublisherInvocations = Meter.CreateCounter<long>(
            "healthcheckplus.publisher.invocations", description: "Number of health check publisher invocations.");

        private static readonly Histogram<double> PublisherDuration = Meter.CreateHistogram<double>(
            "healthcheckplus.publisher.duration", unit: "s", description: "Duration of a health check publisher invocation.");

        private static readonly Counter<long> Anomalies = Meter.CreateCounter<long>(
            "healthcheckplus.anomalies", description: "Internal defensive/error paths that were handled without failing the caller — see the reason tag and the accompanying Warning log for detail.");

        // Only real check executions are recorded here — HealthCheckTrigger.SwitchTo is a manual
        // override (IStateHealthChecksPlus.SwitchToUnhealthy/SwitchToDegraded), not an execution,
        // so it must not count toward duration/executions (it still counts as a status transition,
        // see RecordStatusTransition below).
        public static void RecordCheckExecution(string checkName, HealthStatus status, HealthCheckTrigger origin, TimeSpan duration)
        {
            if (origin == HealthCheckTrigger.SwitchTo)
            {
                return;
            }

            var tags = new TagList
            {
                { "healthcheckplus.check.name", checkName },
                { "healthcheckplus.check.status", status.ToString() },
                { "healthcheckplus.check.origin", origin.ToString() }
            };
            CheckExecutions.Add(1, tags);
            CheckDuration.Record(duration.TotalSeconds, tags);
        }

        // Counts every status change, including manual overrides via SwitchTo — unlike
        // RecordCheckExecution above, a transition is meaningful regardless of what triggered it.
        public static void RecordStatusTransition(string checkName, HealthStatus previousStatus, HealthStatus newStatus)
        {
            if (previousStatus == newStatus)
            {
                return;
            }

            CheckStatusTransitions.Add(1,
                new TagList
                {
                    { "healthcheckplus.check.name", checkName },
                    { "healthcheckplus.check.previous_status", previousStatus.ToString() },
                    { "healthcheckplus.check.status", newStatus.ToString() }
                });
        }

        public static void RecordPublisherInvocation(string publisherType, PublisherInvocationResult result, TimeSpan? duration = null)
        {
            var resultTag = result switch
            {
                PublisherInvocationResult.Published => "published",
                PublisherInvocationResult.SkippedNoChange => "skipped_no_change",
                PublisherInvocationResult.SkippedCondition => "skipped_condition",
                PublisherInvocationResult.Error => "error",
                _ => throw new ArgumentOutOfRangeException(nameof(result), result, null)
            };

            PublisherInvocations.Add(1,
                new TagList
                {
                    { "healthcheckplus.publisher.type", publisherType },
                    { "healthcheckplus.publisher.result", resultTag }
                });

            if (duration.HasValue)
            {
                PublisherDuration.Record(duration.Value.TotalSeconds,
                    new TagList { { "healthcheckplus.publisher.type", publisherType } });
            }
        }

        // Deliberately NOT used for the metrics-recording-failure path itself (CacheHealthCheckPlus's
        // MetricsRecordingError): if the Meter's own recording is what's failing, calling back into
        // it to report that failure would be circular, and could throw again for the same reason.
        // That path stays log-only. Every other anomaly here is unrelated to the metrics pipeline
        // itself, so recording is safe.
        //
        // LoggingSinkFailed is the mirror image: every class with a SafeLog helper (search that
        // name) routes its log calls through it, and its catch reports LoggingSinkFailed via this
        // method instead of trying to log the logging failure - the same circularity risk as above,
        // in the other direction. This is the one place that failure becomes observable.
        public static void RecordAnomaly(AnomalyReason reason)
        {
            var reasonTag = reason switch
            {
                AnomalyReason.AdoptedCheckDisposeFailed => "adopted_check_dispose_failed",
                AnomalyReason.PublisherCycleFailedButContinued => "publisher_cycle_failed_but_continued",
                AnomalyReason.UpdateResultDropped => "update_result_dropped",
                AnomalyReason.CheckExecutionAborted => "check_execution_aborted",
                AnomalyReason.PublishReportBuildFailed => "publish_report_build_failed",
                AnomalyReason.SwitchToDroppedWhileRunning => "switchto_dropped_while_running",
                AnomalyReason.LoggingSinkFailed => "logging_sink_failed",
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
            };

            Anomalies.Add(1, new TagList { { "healthcheckplus.anomaly.reason", reasonTag } });
        }

        // Shared by CacheHealthCheckPlus/DefaultHealthCheckServicePlus/HealthCheckPlusBackGroundService,
        // each of which keeps its own private SafeLog(Action) wrapper delegating to this one -
        // call sites stay unchanged (SafeLog(() => ...)), but the guard body itself, which never
        // referenced any instance state to begin with, now lives in exactly one place instead of
        // three verbatim copies that could individually drift.
        public static void SafeLog(Action logCall)
        {
            try
            {
                logCall();
            }
            catch (Exception)
            {
                RecordAnomaly(AnomalyReason.LoggingSinkFailed);
            }
        }
    }
}
