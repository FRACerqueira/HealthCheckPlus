// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using System.Diagnostics;
using System.Threading;
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
            if (_optionsBackGround.Value.Publishing.Enabled  && publishers.Any())
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
            _runningHealthCheckPlus = new Task(CheckHealthAsync);
            _runningHealthCheckPlus.Start();



            return Task.CompletedTask;
        }

        private async void CheckHealthAsync()
        {
            _stopping.Token.WaitHandle.WaitOne(_optionsBackGround.Value.Delay);
            while (!_stopping.IsCancellationRequested)
            {
                var duration = Stopwatch.StartNew();

                Log.ProcessingBegin(_logger);

                CancellationTokenSource? cancellation = null;
                try
                {
                    cancellation = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
                    cancellation.CancelAfter(_optionsBackGround.Value.Timeout);
                    await _healthCheckService.BackGroudCheckHealthPlusAsync(_optionsBackGround.Value,cancellation.Token);
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
                        if (_countIdletopublish >= int.MaxValue-1)
                        {
                            _countIdletopublish = _optionsBackGround.Value.Publishing.AfterIdleCount;
                        }
                        runpublish = true;
                    }
                    if (runpublish) 
                    {
                        var report = _healthCheckService.CreateReport();
                        if (runpublish && _optionsBackGround.Value.Publishing.WhenReportChange && SameReport(report))
                        {
                            runpublish = false;
                        }
                        if (runpublish)
                        {
                            _hashlaststatus = HashReport(report);
                            _countIdletopublish = 0;
                            var tasks = new Task[_publishers.Length];
                            for (var i = 0; i < _publishers.Length; i++)
                            {
                                tasks[i] = RunPublisherAsync(_publishers[i], report, _stopping.Token);
                            }
                            await Task.WhenAll(tasks).ConfigureAwait(false);
                        }
                    }
                }
                _stopping.Token.WaitHandle.WaitOne(_optionsBackGround.Value.Idle);
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
                var timeout = DateTime.Now.AddSeconds(10);
                while (!_runningHealthCheckPlus.IsCompleted)
                {
                    Thread.Sleep(10);
                    if (DateTime.Now > timeout)
                    {
                        //ignore status
                        break;
                    }
                }
                if (_runningHealthCheckPlus.IsCompleted)
                {
                    _runningHealthCheckPlus.Dispose();
                }
                _runningHealthCheckPlus = null;
            }
            return Task.CompletedTask;
        }

        private async Task RunPublisherAsync(IHealthCheckPublisher publisher, HealthReport report, CancellationToken cancellationToken)
        {
            var duration = Stopwatch.StartNew();

            try
            {
                Log.HealthCheckPublisherBegin(_logger, publisher);
                await publisher.PublishAsync(report, cancellationToken).ConfigureAwait(false);
                Log.HealthCheckPublisherEnd(_logger, publisher, duration.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // This is a cancellation - if the app is shutting down we want to ignore it. Otherwise, it's
                // a timeout and we want to log it.
            }
            catch (OperationCanceledException)
            {
                Log.HealthCheckPublisherTimeout(_logger, publisher, duration.ElapsedMilliseconds);
                throw;
            }
            catch (Exception ex)
            {
                Log.HealthCheckPublisherError(_logger, publisher, duration.ElapsedMilliseconds, ex);
                throw;
            }
        }

        private bool SameReport(HealthReport report)
        {
            return _hashlaststatus == HashReport(report);
        }

        private int HashReport(HealthReport report)
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

            // Hard code the event names to avoid breaking changes. Even if the methods are renamed, these hard-coded names shouldn't change.
            public const string HealthCheckPublisherProcessingBeginName = "HealthCheckPublisherProcessingBegin";
            public const string HealthCheckPublisherProcessingEndName = "HealthCheckPublisherProcessingEnd";
            public const string HealthCheckPublisherBeginName = "HealthCheckPublisherBegin";
            public const string HealthCheckPublisherEndName = "HealthCheckPublisherEnd";
            public const string HealthCheckPublisherErrorName = "HealthCheckPublisherError";
            public const string HealthCheckPublisherTimeoutName = "HealthCheckPublisherTimeout";
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
