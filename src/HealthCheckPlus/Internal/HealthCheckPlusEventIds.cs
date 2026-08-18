// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.Extensions.Logging;

namespace HealthCheckPlus.Internal
{
    // Single source of truth for every EventId this library logs with, across every logger
    // category (CacheHealthCheckPlus, DefaultHealthCheckServicePlus, HealthCheckPlusBackGroundService).
    // This used to be three independent classes (one per logger category, two of them -
    // EventIdsPublisher and EventIds inside HealthCheckPlusBackGroundService - actually feeding the
    // SAME logger and the same generated Log class), each restarting its own numbering from 100.
    // Numeric ids only need to be unique within a single logger category to avoid two different
    // events becoming indistinguishable when filtering logs by id alone, but that per-class
    // numbering is exactly what let a real collision happen twice in one afternoon: fixing one
    // collision meant picking "the next free number" by eye, and the two classes involved were far
    // enough apart in the file that nobody actually checked the other one's full list before
    // picking a value that turned out to already be taken there. One flat, project-wide numbering
    // here - a single list to scan - removes the need to remember which other class(es) also feed
    // the same logger category before adding or renumbering an entry.
    //
    // Hard code the event names, not just the ids, to avoid breaking changes: even if a method is
    // renamed, or this whole class is reorganized, the string an operator's log query matches on
    // must never change.
    internal static class HealthCheckPlusEventIds
    {
        // CacheHealthCheckPlus (ILogger<CacheHealthCheckPlus>)
        public const int CacheMetricsRecordingErrorId = 100;
        public const int UpdateDroppedId = 101;
        public const int SwitchToDroppedId = 102;

        public const string CacheMetricsRecordingErrorName = "HealthCheckPlusMetricsRecordingError";
        public const string UpdateDroppedName = "HealthCheckPlusUpdateDropped";
        public const string SwitchToDroppedName = "HealthCheckPlusSwitchToDropped";

        public static readonly EventId CacheMetricsRecordingError = new(CacheMetricsRecordingErrorId, CacheMetricsRecordingErrorName);
        public static readonly EventId UpdateDropped = new(UpdateDroppedId, UpdateDroppedName);
        public static readonly EventId SwitchToDropped = new(SwitchToDroppedId, SwitchToDroppedName);

        // DefaultHealthCheckServicePlus (ILogger<HealthCheckService>)
        public const int HealthCheckProcessingBeginId = 103;
        public const int HealthCheckProcessingEndId = 104;
        public const int HealthCheckBeginId = 105;
        public const int HealthCheckEndId = 106;
        public const int HealthCheckErrorId = 107;
        public const int HealthCheckDataId = 108;
        public const int HealthCheckDisposeErrorId = 109;
        public const int ServiceMetricsRecordingErrorId = 110;
        public const int HealthCheckExecutionAbortedId = 111;

        public const string HealthCheckProcessingBeginName = "HealthCheckProcessingBegin";
        public const string HealthCheckProcessingEndName = "HealthCheckProcessingEnd";
        public const string HealthCheckBeginName = "HealthCheckBegin";
        public const string HealthCheckEndName = "HealthCheckEnd";
        public const string HealthCheckErrorName = "HealthCheckError";
        public const string HealthCheckDataName = "HealthCheckData";
        public const string HealthCheckDisposeErrorName = "HealthCheckDisposeError";
        public const string ServiceMetricsRecordingErrorName = "HealthCheckPlusMetricsRecordingError";
        public const string HealthCheckExecutionAbortedName = "HealthCheckExecutionAborted";

        public static readonly EventId HealthCheckData = new(HealthCheckDataId, HealthCheckDataName);

        // HealthCheckPlusBackGroundService - publisher dispatch (ILogger<HealthCheckPlusBackGroundService>)
        public const int HealthCheckPublisherBeginId = 112;
        public const int HealthCheckPublisherEndId = 113;
        public const int HealthCheckPublisherErrorId = 114;
        public const int HealthCheckPublisherTimeoutId = 115;
        public const int HealthCheckPublisherCycleErrorId = 116;
        public const int HealthCheckPublisherMetricsRecordingErrorId = 117;

        public const string HealthCheckPublisherBeginName = "HealthCheckPublisherBegin";
        public const string HealthCheckPublisherEndName = "HealthCheckPublisherEnd";
        public const string HealthCheckPublisherErrorName = "HealthCheckPublisherError";
        public const string HealthCheckPublisherTimeoutName = "HealthCheckPublisherTimeout";
        public const string HealthCheckPublisherCycleErrorName = "HealthCheckPublisherCycleError";
        public const string HealthCheckPublisherMetricsRecordingErrorName = "HealthCheckPlusPublisherMetricsRecordingError";

        // HealthCheckPlusBackGroundService - the background loop itself (ILogger<HealthCheckPlusBackGroundService>)
        public const int HealthCheckPlusBackGroundProcessingBeginId = 118;
        public const int HealthCheckPlusBackGroundProcessingEndId = 119;
        public const int HealthCheckPlusBackGroundErrorId = 120;
        public const int HealthCheckPlusBackGroundWarningId = 121;
        public const int HealthCheckPlusBackGroundStopCancellationErrorId = 122;
        public const int HealthCheckPlusBackGroundPublishReportBuildErrorId = 123;
        public const int HealthCheckPlusBackGroundLoopFaultedId = 124;

        public const string HealthCheckPlusBackGroundProcessingBeginName = "HealthCheckPlusBackGroundProcessingBegin";
        public const string HealthCheckPlusBackGroundProcessingEndName = "HealthCheckPlusBackGroundProcessingEnd";
        public const string HealthCheckPlusBackGroundErrorName = "HealthCheckPlusBackGroundError";
        public const string HealthCheckPlusBackGroundTimeoutName = "HealthCheckPlusBackGroundTimeout";
        public const string HealthCheckPlusBackGroundStopCancellationErrorName = "HealthCheckPlusBackGroundStopCancellationError";
        public const string HealthCheckPlusBackGroundPublishReportBuildErrorName = "HealthCheckPlusBackGroundPublishReportBuildError";
        public const string HealthCheckPlusBackGroundLoopFaultedName = "HealthCheckPlusBackGroundLoopFaulted";
    }
}
