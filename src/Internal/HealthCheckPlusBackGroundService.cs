// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using System.Diagnostics;
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
        private Task? _runningHealthCheckPlus;
        private readonly ILogger<HealthCheckPlusBackGroundService> _logger;

        public HealthCheckPlusBackGroundService(ILogger<HealthCheckPlusBackGroundService> logger, HealthCheckService healthCheckService, IOptions<HealthCheckServiceOptions> healthcheckserviceOptions, IOptions<HealthCheckPlusBackGroundOptions> options)
        {
            _optionsBackGround = options;
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

        private static partial class Log
        {
            [LoggerMessage(EventIds.HealthCheckPlusBackGroundProcessingBeginId, LogLevel.Debug, "Running HealthCheckPlus Background-Service checks", EventName = EventIds.HealthCheckProcessingBeginName)]
            public static partial void ProcessingBegin(ILogger logger);


            [LoggerMessage(EventIds.HealthCheckPlusBackGroundProcessingEndId, LogLevel.Debug, "HealthCheckPlus Background-Service completed after {ElapsedMilliseconds}ms", EventName = EventIds.HealthCheckProcessingEndName)]
            public static partial void ProcessingEnd(ILogger logger, double ElapsedMilliseconds);


            [LoggerMessage(EventIds.HealthCheckPlusBackGroundErrorId, LogLevel.Error, "HealthCheckPlus Background-Service threw an unhandled exception after {ElapsedMilliseconds}ms", EventName = EventIds.HealthCheckErrorName)]
            public static partial void ProcessingError(ILogger logger, double ElapsedMilliseconds, Exception exception);

            [LoggerMessage(EventIds.HealthCheckPlusBackGroundWarningId, LogLevel.Warning, "HealthCheckPlus Background-Service threw an timeout after {ElapsedMilliseconds}ms", EventName = EventIds.HealthCheckTimeoutName)]
            public static partial void ProcessingTimeout(ILogger logger, double ElapsedMilliseconds);
        }


    }
}
