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
                // This is a cancellation - if the app is shutting down we want to ignore it. Otherwise, it's
                // a timeout and we want to log it.
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
                        var report = _healthCheckService.CreateReport();
                        if (_optionsBackGround.Value.Publishing.WhenReportChange && SameReport(report))
                        {
                            runpublish = false;
                            // The whole publish cycle is skipped here, before any per-publisher
                            // dispatch, so record the "filtered by WhenReportChange" outcome for
                            // each registered publisher rather than leaving it invisible.
                            foreach (var publisher in _publishers)
                            {
                                HealthCheckPlusMetrics.RecordPublisherInvocation(publisher.GetType().Name, PublisherInvocationResult.SkippedNoChange);
                            }
                        }
                        if (runpublish)
                        {
                            _hashlaststatus = HealthCheckPlusBackGroundService.HashReport(report);
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

                                try
                                {
                                    HealthCheckPlusMetrics.RecordAnomaly(AnomalyReason.PublisherCycleFailedButContinued);
                                }
                                catch (Exception metricsEx)
                                {
                                    // Metrics must never be able to break this loop either (same
                                    // MeterListener risk as everywhere else metrics are recorded).
                                    // Logged explicitly rather than swallowed, since the log above is
                                    // about the publisher cycle failure, not this separate
                                    // metrics-recording failure.
                                    Log.HealthCheckPublisherMetricsRecordingError(_logger, metricsEx);
                                }
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
            catch
            {
                // Ignore exceptions thrown as a result of a cancellation.
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
                    HealthCheckPlusMetrics.RecordPublisherInvocation(publisher.GetType().Name, PublisherInvocationResult.SkippedCondition);
                    return;
                }
            }

            var duration = Stopwatch.StartNew();

            try
            {
                Log.HealthCheckPublisherBegin(_logger, publisher);
                await publisher.PublishAsync(report, cancellationToken).ConfigureAwait(false);
                Log.HealthCheckPublisherEnd(_logger, publisher, duration.ElapsedMilliseconds);
                HealthCheckPlusMetrics.RecordPublisherInvocation(publisher.GetType().Name, PublisherInvocationResult.Published, duration.Elapsed);
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
                HealthCheckPlusMetrics.RecordPublisherInvocation(publisher.GetType().Name, PublisherInvocationResult.Error, duration.Elapsed);
                throw;
            }
            catch (Exception ex)
            {
                Log.HealthCheckPublisherError(_logger, publisher, duration.ElapsedMilliseconds, ex);
                HealthCheckPlusMetrics.RecordPublisherInvocation(publisher.GetType().Name, PublisherInvocationResult.Error, duration.Elapsed);
                throw;
            }
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

            // Hard code the event names to avoid breaking changes. Even if the methods are renamed, these hard-coded names shouldn't change.
            public const string HealthCheckProcessingBeginName = "HealthCheckPlusBackGroundProcessingBegin";
            public const string HealthCheckProcessingEndName = "HealthCheckPlusBackGroundProcessingEnd";
            public const string HealthCheckErrorName = "HealthCheckPlusBackGroundError";
            public const string HealthCheckTimeoutName = "HealthCheckPlusBackGroundTimeout";

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
        }
#pragma warning restore IDE0079

    }
}

