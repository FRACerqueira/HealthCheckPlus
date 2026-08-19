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
    internal partial class HealthCheckPlusBackGroundService : IHostedService, IDisposable
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
            _healthCheckService = InternalCast.To<DefaultHealthCheckServicePlus>(healthCheckService, "the registered HealthCheckService");
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
                        // BuildReportForPublishing (which invokes the consumer-supplied Predicate)
                        // used to sit completely outside any try/catch in this loop - unlike every
                        // other step here, which already has one (the dispatch try/catch just
                        // below, and the check-execution try/catch above). A throw here (e.g. a
                        // Predicate that itself throws) propagated out of this method entirely,
                        // faulting the fire-and-forget Task this loop runs on with no log and no
                        // metric: checks stopped running and publishers stopped firing forever,
                        // with nothing showing the service was dead. The dispatch try/catch below
                        // never rethrows, so this outer catch only ever triggers for a failure in
                        // building/filtering the report itself.
                        try
                        {
                            var report = BuildReportForPublishing();
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
                        catch (Exception ex)
                        {
                            Log.PublishReportBuildError(_logger, ex);
                            SafeRecordMetric(() => HealthCheckPlusMetrics.RecordAnomaly(AnomalyReason.PublishReportBuildFailed));
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
                // ContinueWith with no options runs regardless of the antecedent's outcome, but
                // never inspects or rethrows it - a fault here (e.g. an exception escaping every
                // try/catch inside CheckHealthAsync's loop, which shouldn't normally happen but
                // isn't structurally impossible) used to vanish completely: no log, no exception
                // anywhere, the loop simply stops with nothing distinguishing it from a graceful
                // shutdown. Observing task.Exception here (even just to log it) also prevents an
                // UnobservedTaskException at finalization time. ObserveLoopCompletion is its own
                // method (rather than inline in the continuation) so it can be unit-tested
                // directly against synthetic Task states, the same pattern
                // DefaultHealthCheckServicePlus.ClassifyBatchTask already uses.
                // TaskScheduler.Default, not TaskScheduler.Current: this continuation must always
                // run on the thread pool, regardless of what scheduler happens to be ambient at
                // the point StopAsync itself is called - a well-known .NET pitfall, since
                // ContinueWith with no explicit scheduler otherwise captures TaskScheduler.Current.
                // If a host ever invoked StopAsync from within a task queued to a custom,
                // limited-concurrency scheduler, capturing that scheduler here would tie this
                // continuation's fate to it - a scheduler saturated with unrelated work could stall
                // shutdown indefinitely for no reason connected to this class's own logic.
                return _runningHealthCheckPlus.ContinueWith(task =>
                {
                    ObserveLoopCompletion(task, _logger);
                    _runningHealthCheckPlus.Dispose();
                }, TaskScheduler.Default);
            }
            return Task.CompletedTask;
        }

        internal static void ObserveLoopCompletion(Task loopTask, ILogger logger)
        {
            if (loopTask.IsFaulted)
            {
                Log.BackgroundLoopFaulted(logger, loopTask.Exception!);
            }
        }

        // AddBackgroundPolicy registers this class via AddHostedService, so the DI container
        // already disposes it (like every other disposable singleton) when the host/service
        // provider itself is disposed, strictly after StopAsync has run for every hosted service -
        // by then _stopping has already done its one job (cancelling the loop) and nothing else
        // still needs it. _runningHealthCheckPlus is disposed separately, inside StopAsync's own
        // continuation, once the loop task it wraps has actually completed.
        public void Dispose()
        {
            _stopping.Dispose();
        }

        private async Task RunPublisherAsync(IHealthCheckPublisher publisher, HealthReport report, CancellationToken cancellationToken)
        {
            var duration = Stopwatch.StartNew();

            try
            {
                // Evaluated inside this try/catch (unlike before) so a throwing PublisherCondition
                // is attributed to this exact publisher - HealthCheckPublisherError naming it and
                // the "error" metric tagged with its type - instead of only surfacing one level up,
                // as the generic HealthCheckPublisherCycleError/publisher_cycle_failed_but_continued
                // signal that doesn't say which publisher or why.
                if (publisher is IHealthCheckPlusPublisher publisherPlus &&
                    publisherPlus.PublisherCondition != null && !publisherPlus.PublisherCondition(report))
                {
                    SafeRecordMetric(() => HealthCheckPlusMetrics.RecordPublisherInvocation(PublisherTypeName(publisher), PublisherInvocationResult.SkippedCondition));
                    return;
                }

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

        // Two independent reasons a cache entry must not reach publishers or the WhenReportChange
        // hash, both enforced by DefaultHealthCheckServicePlus.CreateReport(includeName)'s single
        // per-entry snapshot read:
        //
        // 1. HealthCheckPlusBackGroundOptions.Predicate decides which checks this background
        //    service runs (see BackGroudCheckHealthPlusAsync), but a plain CreateReport() has no
        //    notion of it and always reports on every check tracked in the cache - including one
        //    the predicate excludes from ever running here, which would otherwise never leave its
        //    InitCache seed status (Healthy) and be published as such forever.
        // 2. A check the predicate *does* include can still not have run even once yet - e.g. its
        //    own Delay (AddCheckPlus/AddCheckLinkTo) is longer than this cycle's Delay+Idle, which
        //    the README's own example values (30s check delay vs. 5s background delay) trigger on
        //    the very first cycle. A plain CreateReport() reports InitCache's seed (Healthy,
        //    Origin=None) for it exactly the same as a real result, with nothing distinguishing
        //    "never checked" from "checked and found Healthy".
        //
        // Both checks must be decided from the exact same Snapshot read used to build each
        // entry's data, not a separate CreateReport() followed later by a per-name "has it run"
        // check: that used to let a check that completed its first real run in the gap between
        // the two calls be included while still carrying the InitCache seed data the first call
        // had already snapshotted - a phantom Healthy result published for a check that, at
        // publish time, had barely just started reporting real data. CreateReport(includeName)
        // closes that gap by deciding both in the same per-entry read.
        private HealthReport BuildReportForPublishing()
        {
            var predicate = _optionsBackGround.Value.Predicate;
            var eligibleNames = new HashSet<string>(
                predicate == null
                    ? _healthcheckserviceOptions.Value.Registrations.Select(r => r.Name)
                    : _healthcheckserviceOptions.Value.Registrations.Where(predicate).Select(r => r.Name),
                StringComparer.OrdinalIgnoreCase);

            return _healthCheckService.CreateReport(eligibleNames.Contains);
        }

        private bool SameReport(HealthReport report)
        {
            return _hashlaststatus == HealthCheckPlusBackGroundService.HashReport(report);
        }

        internal static int HashReport(HealthReport report)
        {
            return string.Join("", report.Entries.Select(x => (x.Key + x.Value.Status))).GetHashCode(StringComparison.InvariantCulture);
        }

#pragma warning disable IDE0079
        private static partial class Log
        {
            [LoggerMessage(HealthCheckPlusEventIds.HealthCheckPublisherBeginId, LogLevel.Debug, "Running health check publisher '{HealthCheckPublisher}'", EventName = HealthCheckPlusEventIds.HealthCheckPublisherBeginName)]
            public static partial void HealthCheckPublisherBegin(ILogger logger, IHealthCheckPublisher HealthCheckPublisher);

            [LoggerMessage(HealthCheckPlusEventIds.HealthCheckPublisherEndId, LogLevel.Debug, "Health check '{HealthCheckPublisher}' completed after {ElapsedMilliseconds}ms", EventName = HealthCheckPlusEventIds.HealthCheckPublisherEndName)]
            public static partial void HealthCheckPublisherEnd(ILogger logger, IHealthCheckPublisher HealthCheckPublisher, double ElapsedMilliseconds);

            [LoggerMessage(HealthCheckPlusEventIds.HealthCheckPublisherErrorId, LogLevel.Error, "Health check {HealthCheckPublisher} threw an unhandled exception after {ElapsedMilliseconds}ms", EventName = HealthCheckPlusEventIds.HealthCheckPublisherErrorName)]
            public static partial void HealthCheckPublisherError(ILogger logger, IHealthCheckPublisher HealthCheckPublisher, double ElapsedMilliseconds, Exception exception);

            [LoggerMessage(HealthCheckPlusEventIds.HealthCheckPublisherTimeoutId, LogLevel.Error, "Health check {HealthCheckPublisher} was canceled after {ElapsedMilliseconds}ms", EventName = HealthCheckPlusEventIds.HealthCheckPublisherTimeoutName)]
            public static partial void HealthCheckPublisherTimeout(ILogger logger, IHealthCheckPublisher HealthCheckPublisher, double ElapsedMilliseconds);

            [LoggerMessage(HealthCheckPlusEventIds.HealthCheckPublisherCycleErrorId, LogLevel.Warning,
                "One or more health check publishers failed this cycle (see the HealthCheckPublisherError/HealthCheckPublisherTimeout entries above for which one and why); HealthCheckPlus Background-Service will continue running.",
                EventName = HealthCheckPlusEventIds.HealthCheckPublisherCycleErrorName)]
            public static partial void HealthCheckPublisherCycleError(ILogger logger, Exception exception);

            [LoggerMessage(HealthCheckPlusEventIds.HealthCheckPublisherMetricsRecordingErrorId, LogLevel.Warning,
                "Recording the anomaly metric for a publisher cycle failure also failed; the cycle failure itself was already logged above.",
                EventName = HealthCheckPlusEventIds.HealthCheckPublisherMetricsRecordingErrorName)]
            public static partial void HealthCheckPublisherMetricsRecordingError(ILogger logger, Exception exception);

            [LoggerMessage(HealthCheckPlusEventIds.HealthCheckPlusBackGroundProcessingBeginId, LogLevel.Debug, "Running HealthCheckPlus Background-Service checks", EventName = HealthCheckPlusEventIds.HealthCheckPlusBackGroundProcessingBeginName)]
            public static partial void ProcessingBegin(ILogger logger);

            [LoggerMessage(HealthCheckPlusEventIds.HealthCheckPlusBackGroundProcessingEndId, LogLevel.Debug, "HealthCheckPlus Background-Service completed after {ElapsedMilliseconds}ms", EventName = HealthCheckPlusEventIds.HealthCheckPlusBackGroundProcessingEndName)]
            public static partial void ProcessingEnd(ILogger logger, double ElapsedMilliseconds);

            [LoggerMessage(HealthCheckPlusEventIds.HealthCheckPlusBackGroundErrorId, LogLevel.Error, "HealthCheckPlus Background-Service threw an unhandled exception after {ElapsedMilliseconds}ms", EventName = HealthCheckPlusEventIds.HealthCheckPlusBackGroundErrorName)]
            public static partial void ProcessingError(ILogger logger, double ElapsedMilliseconds, Exception exception);

            [LoggerMessage(HealthCheckPlusEventIds.HealthCheckPlusBackGroundWarningId, LogLevel.Warning, "HealthCheckPlus Background-Service threw an timeout after {ElapsedMilliseconds}ms", EventName = HealthCheckPlusEventIds.HealthCheckPlusBackGroundTimeoutName)]
            public static partial void ProcessingTimeout(ILogger logger, double ElapsedMilliseconds);

            [LoggerMessage(HealthCheckPlusEventIds.HealthCheckPlusBackGroundStopCancellationErrorId, LogLevel.Warning,
                "Cancelling the HealthCheckPlus Background-Service's stopping token threw; shutdown continues regardless.",
                EventName = HealthCheckPlusEventIds.HealthCheckPlusBackGroundStopCancellationErrorName)]
            public static partial void StopCancellationError(ILogger logger, Exception exception);

            [LoggerMessage(HealthCheckPlusEventIds.HealthCheckPlusBackGroundPublishReportBuildErrorId, LogLevel.Error,
                "Building the report to publish this cycle failed (e.g. the configured Predicate threw); no publishers were invoked this cycle. HealthCheckPlus Background-Service will continue running.",
                EventName = HealthCheckPlusEventIds.HealthCheckPlusBackGroundPublishReportBuildErrorName)]
            public static partial void PublishReportBuildError(ILogger logger, Exception exception);

            [LoggerMessage(HealthCheckPlusEventIds.HealthCheckPlusBackGroundLoopFaultedId, LogLevel.Critical,
                "The HealthCheckPlus Background-Service's loop terminated with an unhandled exception; it will not run again until the host restarts.",
                EventName = HealthCheckPlusEventIds.HealthCheckPlusBackGroundLoopFaultedName)]
            public static partial void BackgroundLoopFaulted(ILogger logger, Exception exception);
        }
#pragma warning restore IDE0079

    }
}

