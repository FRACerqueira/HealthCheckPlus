// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using System.Diagnostics;
using HealthCheckPlus.Abstractions;
using HealthCheckPlus.Internal.WrapperMicrosoft;
using HealthCheckPlus.options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthCheckPlus.Internal
{
    internal partial class HealthCheckPlusBackGroundService : IHostedService
    {
        private readonly IOptions<HealthCheckPlusBackGroundOptions> _optionsBackGround;
        private readonly IOptions<HealthCheckServiceOptions> _healthcheckserviceOptions;
        private readonly DefaultHealthCheckServicePlus _healthCheckService;
        private readonly CancellationTokenSource _stopping;
        private readonly IHealthCheckPublisher[] _publishers;
        private readonly bool _haspublishers;
        private Task? _runningHealthCheckPlus;
        private readonly ILogger<HealthCheckPlusBackGroundService> _logger;
        private int _countIdletopublish = 0;
        private int _hashlaststatus;

        public HealthCheckPlusBackGroundService(
            ILogger<HealthCheckPlusBackGroundService> logger,
            HealthCheckService healthCheckService,
            IOptions<HealthCheckServiceOptions> healthcheckserviceOptions,
            IOptions<HealthCheckPlusBackGroundOptions> options,
            IEnumerable<IHealthCheckPublisher> publishers)
        {
            _optionsBackGround = options;
            _publishers = [];
            if (_optionsBackGround.Value.Publishing.Enabled && publishers.Any())
            {
                _publishers = publishers.ToArray();
                _haspublishers = true;
            }
            _healthcheckserviceOptions = healthcheckserviceOptions;
            _healthCheckService = (DefaultHealthCheckServicePlus)healthCheckService;
            _logger = logger;
            _stopping = new CancellationTokenSource();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (_healthcheckserviceOptions.Value.Registrations.Count == 0)
            {
                return Task.CompletedTask;
            }

            // IMPORTANT - make sure this is the last thing that happens in this method. The task can
            // fire before other code runs.
            _runningHealthCheckPlus = Task.Run(CheckHealthAsync, _stopping.Token);

            return Task.CompletedTask;
        }

        private async Task CheckHealthAsync()
        {
            try
            {
                await Task.Delay(_optionsBackGround.Value.Delay, _stopping.Token);
            }
            catch (OperationCanceledException)
            {
                // _stopping.Token is the only cancellation source for this initial delay - unlike
                // the per-cycle waits below, there's no separate timeout token here, so this is
                // always a shutdown, never a timeout to log.
            }
            while (!_stopping.IsCancellationRequested)
            {
                var duration = Stopwatch.StartNew();

                Log.ProcessingBegin(_logger);

                CancellationTokenSource? cancellation = null;
                try
                {
                    cancellation = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
                    cancellation.CancelAfter(_optionsBackGround.Value.Timeout);
                    await _healthCheckService.BackGroudCheckHealthPlusAsync(_optionsBackGround.Value, cancellation.Token);
                }
                catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
                {
                    // This is a cancellation - if the app is shutting down we want to ignore it. Otherwise, it's
                    // a timeout and we want to log it.
                }
                catch (OperationCanceledException)
                {
                    // This is a timeout
                    Log.ProcessingTimeout(_logger, duration.Elapsed.TotalMilliseconds);
                }
                catch (Exception ex)
                {
                    // This is an error,  CheckHealthAsync failed.
                    Log.ProcessingError(_logger, duration.Elapsed.TotalMilliseconds, ex);
                }
                finally
                {
                    cancellation?.Dispose();
                }

                Log.ProcessingEnd(_logger, duration.Elapsed.TotalMilliseconds);

                if (_haspublishers)
                {
                    var runpublish = false;
                    _countIdletopublish++;
                    if (_countIdletopublish >= _optionsBackGround.Value.Publishing.AfterIdleCount)
                    {
                        if (_countIdletopublish >= int.MaxValue - 1)
                        {
                            _countIdletopublish = _optionsBackGround.Value.Publishing.AfterIdleCount;
                        }
                        runpublish = true;
                    }
                    if (runpublish)
                    {
                        var report = FilterReportByPredicate(_healthCheckService.CreateReport());
                        if (_optionsBackGround.Value.Publishing.WhenReportChange && SameReport(report))
                        {
                            runpublish = false;
                            // The whole publish cycle is skipped here, before any per-publisher
                            // dispatch, so record the "filtered by WhenReportChange" outcome for
                            // each registered publisher rather than leaving it invisible.
                            foreach (var publisher in _publishers)
                            {
                                SafeRecordMetric(() => HealthCheckPlusMetrics.RecordPublisherInvocation(PublisherTypeName(publisher), PublisherInvocationResult.SkippedNoChange));
                            }
                        }
                        if (runpublish)
                        {
                            _countIdletopublish = 0;

                            CancellationTokenSource? publishCancellation = null;
                            try
                            {
                                // Bound publisher dispatch by the same per-cycle Timeout that
                                // already bounds check execution above. Without this, a publisher
                                // with no timeout of its own (e.g. an HTTP call to an endpoint that
                                // never responds) blocked Task.WhenAll below indefinitely, freezing
                                // the entire background loop - checks included, not just
                                // publishing - since nothing else here ever cancelled it.
                                publishCancellation = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
                                publishCancellation.CancelAfter(_optionsBackGround.Value.Timeout);
                                var tasks = _publishers.Select(publisher => RunPublisherAsync(publisher, report, publishCancellation.Token)).ToArray();
                                await Task.WhenAll(tasks).ConfigureAwait(false);

                                // Only commit the new hash once dispatch has actually succeeded.
                                // Committing it unconditionally (as before) meant a failed dispatch
                                // still marked this status as "already published" - the next cycle's
                                // SameReport check would then match and skip retrying, permanently
                                // losing the notification for that status change until it changed
                                // again.
                                _hashlaststatus = HealthCheckPlusBackGroundService.HashReport(report);
                            }
                            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
                            {
                                // Shutting down - let the loop's natural exit path (the Task.Delay
                                // below) observe the cancellation, same as the check execution
                                // block above.
                            }
                            catch (Exception ex)
                            {
                                // Each failing publisher already logged its own error/timeout and
                                // recorded the "error" metric inside RunPublisherAsync (with which
                                // publisher, duration, and exception) - this also covers a publisher
                                // that didn't finish within Timeout, since RunPublisherAsync's own
                                // timeout catch (observing this same linked token) logs/metrics it
                                // and rethrows. This log adds the signal that was otherwise missing:
                                // that the background loop is continuing despite the failure above,
                                // instead of leaving no operational trace of whether it's still
                                // alive or has silently died - without this try/catch (unlike the
                                // check-execution block above it, which already has one),
                                // Task.WhenAll's rethrown exception would fault the loop's
                                // fire-and-forget Task silently.
                                Log.HealthCheckPublisherCycleError(_logger, ex);

                                SafeRecordMetric(() => HealthCheckPlusMetrics.RecordAnomaly(AnomalyReason.PublisherCycleFailedButContinued));
                            }
                            finally
                            {
                                publishCancellation?.Dispose();
                            }
                        }
                    }
                }
                await Task.Delay(_optionsBackGround.Value.Idle, _stopping.Token);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                _stopping.Cancel();
            }
            catch (Exception ex)
            {
                // _stopping.Cancel() can throw if any callback registered anywhere on the
                // cancellation chain a background cycle's linked token belongs to (see
                // CheckHealthAsync's `cancellation = CreateLinkedTokenSource(_stopping.Token)`)
                // itself throws - not a scenario this class's own code creates, but reachable from
                // outside it (e.g. a health check's own CancellationToken.Register callback).
                // Shutdown must proceed regardless of what happens here (the rest of this method
                // still needs to run), so this is deliberately swallowed rather than rethrown - but
                // it must not vanish with zero signal.
                Log.StopCancellationError(_logger, ex);
            }

            if (_healthcheckserviceOptions.Value.Registrations.Count == 0)
            {
                return Task.CompletedTask;
            }
            if (_runningHealthCheckPlus != null)
            {
                return _runningHealthCheckPlus.ContinueWith(task => _runningHealthCheckPlus.Dispose(), TaskScheduler.Current);
            }
            return Task.CompletedTask;
        }

        private async Task RunPublisherAsync(IHealthCheckPublisher publisher, HealthReport report, CancellationToken cancellationToken)
        {
            if (publisher is IHealthCheckPlusPublisher publisherPlus)
            {
                if (publisherPlus.PublisherCondition != null && !publisherPlus.PublisherCondition(report))
                {
                    SafeRecordMetric(() => HealthCheckPlusMetrics.RecordPublisherInvocation(PublisherTypeName(publisher), PublisherInvocationResult.SkippedCondition));
                    return;
                }
            }

            var duration = Stopwatch.StartNew();

            try
            {
                Log.HealthCheckPublisherBegin(_logger, publisher);
                await publisher.PublishAsync(report, cancellationToken).ConfigureAwait(false);
                Log.HealthCheckPublisherEnd(_logger, publisher, duration.ElapsedMilliseconds);
                SafeRecordMetric(() => HealthCheckPlusMetrics.RecordPublisherInvocation(PublisherTypeName(publisher), PublisherInvocationResult.Published, duration.Elapsed));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Shutting down - deliberately not logged as an error (this mirrors ASP.NET Core's
                // own HealthCheckPublisherHostedService behavior) and, unlike every other outcome
                // in this method, deliberately not recorded as a metric either: a shutdown-time
                // cancellation is not an operational failure an operator needs to see counted, and
                // recording one more result value here would mean "invocations" no longer means
                // "attempts to publish while the app was actually running". This is the one place
                // where healthcheckplus.publisher.invocations intentionally under-counts.
            }
            catch (OperationCanceledException)
            {
                Log.HealthCheckPublisherTimeout(_logger, publisher, duration.ElapsedMilliseconds);
                SafeRecordMetric(() => HealthCheckPlusMetrics.RecordPublisherInvocation(PublisherTypeName(publisher), PublisherInvocationResult.Error, duration.Elapsed));
                throw;
            }
            catch (Exception ex)
            {
                Log.HealthCheckPublisherError(_logger, publisher, duration.ElapsedMilliseconds, ex);
                SafeRecordMetric(() => HealthCheckPlusMetrics.RecordPublisherInvocation(PublisherTypeName(publisher), PublisherInvocationResult.Error, duration.Elapsed));
                throw;
            }
        }

        // A MeterListener callback (e.g. a third-party OTel exporter) runs synchronously on this
        // thread, so a bug in it must never be allowed to break publisher dispatch or be
        // misattributed as the publisher itself having failed (a throwing metrics call used to sit
        // as the last statement inside the "success" try block above, so it was caught by the
        // publisher-failure catch below it, logging a false HealthCheckPublisherError and
        // recording a false "error" metric instead of surfacing the real problem). Every
        // RecordPublisherInvocation/RecordAnomaly call in this class goes through here instead.
        // GetType().Name (the short class name) collapses two publishers of the same class name in
        // different namespaces into one "healthcheckplus.publisher.type" tag value, silently
        // merging their measurements into a single series. FullName is unique per type; Name is a
        // defensive fallback for the (practically unreachable, for a concrete publisher instance)
        // case where FullName is null, e.g. a generic type parameter.
        private static string PublisherTypeName(IHealthCheckPublisher publisher)
        {
            var type = publisher.GetType();
            return type.FullName ?? type.Name;
        }

        private void SafeRecordMetric(Action recordMetric)
        {
            try
            {
                recordMetric();
            }
            catch (Exception ex)
            {
                Log.HealthCheckPublisherMetricsRecordingError(_logger, ex);
            }
        }

        // HealthCheckPlusBackGroundOptions.Predicate decides which checks this background service
        // runs (see BackGroudCheckHealthPlusAsync), but CacheHealthCheckPlus.CreateReport() has no
        // notion of it and always reports on every check tracked in the cache - including one the
        // predicate excludes from ever running here, which would otherwise never leave its
        // InitCache seed status (Healthy) and be published as such forever. A predicate-excluded
        // check is explicitly outside what this background service manages, so it must not appear
        // in the report this service hashes (for WhenReportChange) or hands to its publishers at
        // all - not reported incorrectly, not reported.
        private HealthReport FilterReportByPredicate(HealthReport report)
        {
            var predicate = _optionsBackGround.Value.Predicate;
            if (predicate == null)
            {
                return report;
            }

            var includedNames = new HashSet<string>(
                _healthcheckserviceOptions.Value.Registrations.Where(predicate).Select(r => r.Name),
                StringComparer.OrdinalIgnoreCase);

            if (includedNames.Count == report.Entries.Count)
            {
                return report;
            }

            var filteredEntries = report.Entries
                .Where(e => includedNames.Contains(e.Key))
                .ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);

            return new HealthReport(filteredEntries, report.TotalDuration);
        }

        private bool SameReport(HealthReport report)
        {
            return _hashlaststatus == HealthCheckPlusBackGroundService.HashReport(report);
        }

        internal static int HashReport(HealthReport report)
        {
            return string.Join("", report.Entries.Select(x => (x.Key + x.Value.Status))).GetHashCode(StringComparison.InvariantCulture);
        }

        private static class EventIdsPublisher
        {
            public const int HealthCheckPublisherProcessingBeginId = 100;
            public const int HealthCheckPublisherProcessingEndId = 101;
            public const int HealthCheckPublisherBeginId = 102;
            public const int HealthCheckPublisherEndId = 103;
            public const int HealthCheckPublisherErrorId = 104;
            public const int HealthCheckPublisherTimeoutId = 104;
            public const int HealthCheckPublisherCycleErrorId = 106;
            public const int HealthCheckPublisherMetricsRecordingErrorId = 107;

            // Hard code the event names to avoid breaking changes. Even if the methods are renamed, these hard-coded names shouldn't change.
            public const string HealthCheckPublisherProcessingBeginName = "HealthCheckPublisherProcessingBegin";
            public const string HealthCheckPublisherProcessingEndName = "HealthCheckPublisherProcessingEnd";
            public const string HealthCheckPublisherBeginName = "HealthCheckPublisherBegin";
            public const string HealthCheckPublisherEndName = "HealthCheckPublisherEnd";
            public const string HealthCheckPublisherErrorName = "HealthCheckPublisherError";
            public const string HealthCheckPublisherTimeoutName = "HealthCheckPublisherTimeout";
            public const string HealthCheckPublisherCycleErrorName = "HealthCheckPublisherCycleError";
            public const string HealthCheckPublisherMetricsRecordingErrorName = "HealthCheckPlusPublisherMetricsRecordingError";
        }

        private static class EventIds
        {
            public const int HealthCheckPlusBackGroundProcessingBeginId = 100;
            public const int HealthCheckPlusBackGroundProcessingEndId = 101;
            public const int HealthCheckPlusBackGroundErrorId = 104;
            public const int HealthCheckPlusBackGroundWarningId = 105;
            public const int HealthCheckPlusBackGroundStopCancellationErrorId = 106;

            // Hard code the event names to avoid breaking changes. Even if the methods are renamed, these hard-coded names shouldn't change.
            public const string HealthCheckProcessingBeginName = "HealthCheckPlusBackGroundProcessingBegin";
            public const string HealthCheckProcessingEndName = "HealthCheckPlusBackGroundProcessingEnd";
            public const string HealthCheckErrorName = "HealthCheckPlusBackGroundError";
            public const string HealthCheckTimeoutName = "HealthCheckPlusBackGroundTimeout";
            public const string HealthCheckStopCancellationErrorName = "HealthCheckPlusBackGroundStopCancellationError";

        }

#pragma warning disable IDE0079
        private static partial class Log
        {
            [LoggerMessage(EventIdsPublisher.HealthCheckPublisherBeginId, LogLevel.Debug, "Running health check publisher '{HealthCheckPublisher}'", EventName = EventIdsPublisher.HealthCheckPublisherBeginName)]
            public static partial void HealthCheckPublisherBegin(ILogger logger, IHealthCheckPublisher HealthCheckPublisher);

            [LoggerMessage(EventIdsPublisher.HealthCheckPublisherEndId, LogLevel.Debug, "Health check '{HealthCheckPublisher}' completed after {ElapsedMilliseconds}ms", EventName = EventIdsPublisher.HealthCheckPublisherEndName)]
            public static partial void HealthCheckPublisherEnd(ILogger logger, IHealthCheckPublisher HealthCheckPublisher, double ElapsedMilliseconds);

            [LoggerMessage(EventIdsPublisher.HealthCheckPublisherErrorId, LogLevel.Error, "Health check {HealthCheckPublisher} threw an unhandled exception after {ElapsedMilliseconds}ms", EventName = EventIdsPublisher.HealthCheckPublisherErrorName)]
            public static partial void HealthCheckPublisherError(ILogger logger, IHealthCheckPublisher HealthCheckPublisher, double ElapsedMilliseconds, Exception exception);

#pragma warning disable SYSLIB1006 // Multiple logging methods cannot use the same event id within a class
            [LoggerMessage(EventIdsPublisher.HealthCheckPublisherTimeoutId, LogLevel.Error, "Health check {HealthCheckPublisher} was canceled after {ElapsedMilliseconds}ms", EventName = EventIdsPublisher.HealthCheckPublisherTimeoutName)]
#pragma warning restore SYSLIB1006 // Multiple logging methods cannot use the same event id within a class
            public static partial void HealthCheckPublisherTimeout(ILogger logger, IHealthCheckPublisher HealthCheckPublisher, double ElapsedMilliseconds);

            [LoggerMessage(EventIdsPublisher.HealthCheckPublisherCycleErrorId, LogLevel.Warning,
                "One or more health check publishers failed this cycle (see the HealthCheckPublisherError/HealthCheckPublisherTimeout entries above for which one and why); HealthCheckPlus Background-Service will continue running.",
                EventName = EventIdsPublisher.HealthCheckPublisherCycleErrorName)]
            public static partial void HealthCheckPublisherCycleError(ILogger logger, Exception exception);

            [LoggerMessage(EventIdsPublisher.HealthCheckPublisherMetricsRecordingErrorId, LogLevel.Warning,
                "Recording the anomaly metric for a publisher cycle failure also failed; the cycle failure itself was already logged above.",
                EventName = EventIdsPublisher.HealthCheckPublisherMetricsRecordingErrorName)]
            public static partial void HealthCheckPublisherMetricsRecordingError(ILogger logger, Exception exception);

            [LoggerMessage(EventIds.HealthCheckPlusBackGroundProcessingBeginId, LogLevel.Debug, "Running HealthCheckPlus Background-Service checks", EventName = EventIds.HealthCheckProcessingBeginName)]
            public static partial void ProcessingBegin(ILogger logger);

            [LoggerMessage(EventIds.HealthCheckPlusBackGroundProcessingEndId, LogLevel.Debug, "HealthCheckPlus Background-Service completed after {ElapsedMilliseconds}ms", EventName = EventIds.HealthCheckProcessingEndName)]
            public static partial void ProcessingEnd(ILogger logger, double ElapsedMilliseconds);

#pragma warning disable SYSLIB1006 // Multiple logging methods cannot use the same event id within a class
            [LoggerMessage(EventIds.HealthCheckPlusBackGroundErrorId, LogLevel.Error, "HealthCheckPlus Background-Service threw an unhandled exception after {ElapsedMilliseconds}ms", EventName = EventIds.HealthCheckErrorName)]
#pragma warning restore SYSLIB1006 // Multiple logging methods cannot use the same event id within a class
            public static partial void ProcessingError(ILogger logger, double ElapsedMilliseconds, Exception exception);

            [LoggerMessage(EventIds.HealthCheckPlusBackGroundWarningId, LogLevel.Warning, "HealthCheckPlus Background-Service threw an timeout after {ElapsedMilliseconds}ms", EventName = EventIds.HealthCheckTimeoutName)]
            public static partial void ProcessingTimeout(ILogger logger, double ElapsedMilliseconds);

            [LoggerMessage(EventIds.HealthCheckPlusBackGroundStopCancellationErrorId, LogLevel.Warning,
                "Cancelling the HealthCheckPlus Background-Service's stopping token threw; shutdown continues regardless.",
                EventName = EventIds.HealthCheckStopCancellationErrorName)]
            public static partial void StopCancellationError(ILogger logger, Exception exception);
        }
#pragma warning restore IDE0079

    }
}

