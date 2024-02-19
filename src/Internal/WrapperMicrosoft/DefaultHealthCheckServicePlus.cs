// ********************************************************************************************
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// ********************************************************************************************
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Internal.Policies;
using HealthCheckPlus.options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Diagnostics;
using System.Text;

namespace HealthCheckPlus.Internal.WrapperMicrosoft
{
    internal partial class DefaultHealthCheckServicePlus : HealthCheckService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptions<HealthCheckServiceOptions> _options;
        private readonly IServiceProvider _services;
        private readonly ILogger<HealthCheckService> _logger;
        private readonly List<IHealthCheckPlusPolicyStatus> _policies;
        private readonly CacheHealthCheckPlus _cacheStatus;

        public DefaultHealthCheckServicePlus(
            IServiceScopeFactory scopeFactory,
            IServiceProvider services,
            ILogger<HealthCheckService> logger,
            IOptions<HealthCheckServiceOptions> options)
        {
            _services = services;
            _logger = logger;
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            // We're specifically going out of our way to do this at startup time. We want to make sure you
            // get any kind of health-check related error as early as possible. Waiting until someone
            // actually tries to **run** health checks would be real baaaaad.
            ValidateRegistrations(_options.Value.Registrations);

            _policies = [];
            _policies.AddRange(_services
                .GetServices<IHealthCheckPlusPolicyStatus>());

            _cacheStatus = (CacheHealthCheckPlus)_services.GetRequiredService<IStateHealthChecksPlus>();

        }

        public override Task<HealthReport> CheckHealthAsync(
            Func<HealthCheckRegistration, bool>? predicate,
            CancellationToken cancellationToken = default)
        {
            return CheckHealthPlusAsync(predicate, null, HealthCheckTrigger.Default, cancellationToken);
        }

        public async Task<HealthReport> CheckHealthPlusAsync(
            Func<HealthCheckRegistration, bool>? predicate,
            Func<HealthReport, HealthStatus>? statusHealthreport,
            HealthCheckTrigger resultHealthCheckFrom,
            CancellationToken cancellationToken = default)
        {
            var registrations = _options.Value.Registrations;
            if (predicate != null)
            {
                registrations = registrations.Where(predicate).ToArray();
            }

            //update policy using last state
            var registrationstorun = new List<HealthCheckRegistration>();
            foreach (var item in registrations)
            {

                IHealthCheckPlusPolicyStatus policy = _policies
                    .Where(x => x.PolicyNameDep == item.Name && x.PolicyForStatus == HealthStatus.Healthy)
                    .FirstOrDefault()!;

                var sta = _cacheStatus.FullStatus(item.Name);
                switch (sta.Lastresult.Status)
                {
                    case HealthStatus.Unhealthy:
                        {
                            var aux = _policies
                                .Where(x => x.PolicyNameDep == item.Name && x.PolicyForStatus == HealthStatus.Unhealthy)
                                .FirstOrDefault();
                            if (aux is not null)
                            {
                                policy = aux;
                            }
                        }
                        break;
                    case HealthStatus.Degraded:
                        {
                            var aux = _policies
                                .Where(x => x.PolicyNameDep == item.Name && x.PolicyForStatus == HealthStatus.Unhealthy)
                                .FirstOrDefault();
                            if (aux is not null)
                            {
                                policy = aux;
                            }
                        }
                        break;
                    default: //HealthStatus.
                        break;
                }

                var itemToRum = new HealthCheckRegistration(item.Name, item.Factory, item.FailureStatus, item.Tags, item.Timeout)
                {
                    Delay = item.Delay ?? policy.PolicyDelay??TimeSpan.Zero,
                    Period = item.Period ?? policy.PolicyPeriod ?? TimeSpan.Zero
                };

                if (sta.Dateref == _cacheStatus.DateRegister)
                {
                    if (_cacheStatus.DateRegister.Add(itemToRum.Delay!.Value) < DateTime.Now)
                    {
                        _cacheStatus.Running(itemToRum.Name, true);
                        registrationstorun.Add(itemToRum);
                    }
                }
                else
                {
                    if (sta.Dateref.Add(itemToRum.Period!.Value) < DateTime.Now)
                    {
                        _cacheStatus.Running(itemToRum.Name, true);
                        registrationstorun.Add(itemToRum);
                    }
                }
            }


            var totalTime = Stopwatch.StartNew();

            var entries = new Dictionary<string, HealthReportEntry>(StringComparer.OrdinalIgnoreCase);

            if (registrationstorun.Count != 0)
            {
                Log.HealthCheckProcessingBegin(_logger);

                var tasks = new Task<HealthReportEntry>[registrationstorun.Count];
                var index = 0;

                var dtref = DateTime.Now;
                foreach (var registration in registrationstorun)
                {
                    tasks[index++] = Task.Run(() => RunCheckAsync(registration, cancellationToken), cancellationToken);
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
                totalTime.Stop();

                index = 0;
                foreach (var registration in registrationstorun)
                {
                    var item = new HealthCheckResult(tasks[index].Result.Status,
                        tasks[index].Result.Description,
                        tasks[index].Result.Exception,
                        tasks[index].Result.Data);
                    _cacheStatus.Update(
                        registration.Name,
                        resultHealthCheckFrom,
                        item,
                        dtref.Add(tasks[index].Result.Duration),
                        tasks[index].Result.Duration);
                    index++;
                }
            }

            foreach (var registration in registrations)
            {
                var sta = _cacheStatus.FullStatus(registration.Name);
                var result = new HealthReportEntry(sta.Lastresult.Status,
                    sta.Lastresult.Description, sta.Duration, sta.Lastresult.Exception, sta.Lastresult.Data, registration.Tags);
                entries[registration.Name] = result;
            }
            var report = new HealthReport(entries, totalTime.Elapsed);

            _cacheStatus.UpdateStatusName();

            if (statusHealthreport != null)
            {
                var sta = statusHealthreport.Invoke(report);
                report = new HealthReport(entries, sta, totalTime.Elapsed);
            }

            if (registrationstorun.Count != 0)
            {
                Log.HealthCheckProcessingEnd(_logger, report.Status, totalTime.Elapsed);
            }
            return report;
        }

        public async Task BackGroudCheckHealthPlusAsync(
            HealthCheckPlusBackGroundOptions backgroudoptions,
            CancellationToken cancellationToken = default)
        {
            var registrations = _options.Value.Registrations;
            if (backgroudoptions.Predicate != null)
            {
                registrations = registrations.Where(backgroudoptions.Predicate).ToArray();
            }

            //update policy using last state
            var registrationstorun = new List<HealthCheckRegistration>();
            foreach (var item in registrations)
            {

                IHealthCheckPlusPolicyStatus policy;
                
                var sta = _cacheStatus.FullStatus(item.Name);
                switch (sta.Lastresult.Status)
                {
                    case HealthStatus.Unhealthy:
                        {
                            var aux = _policies
                                .Where(x => x.PolicyNameDep == item.Name && x.PolicyForStatus == HealthStatus.Unhealthy)
                                .FirstOrDefault();
                            policy = aux ?? new HealthCheckPlusPolicyStatus(HealthStatus.Unhealthy, backgroudoptions.Delay, backgroudoptions.UnhealthyPeriod, item.Name);
                        }
                        break;
                    case HealthStatus.Degraded:
                        {
                            var aux = _policies
                                .Where(x => x.PolicyNameDep == item.Name && x.PolicyForStatus == HealthStatus.Degraded)
                                .FirstOrDefault();
                            policy = aux ?? new HealthCheckPlusPolicyStatus(HealthStatus.Degraded, backgroudoptions.Delay, backgroudoptions.DegradedPeriod, item.Name);
                        }
                        break;
                    default: //HealthStatus.Healthy
                        {
                            var aux = _policies
                                .Where(x => x.PolicyNameDep == item.Name && x.PolicyForStatus == HealthStatus.Healthy)
                                .FirstOrDefault()!;

                            var delay = aux.PolicyDelay ?? (sta.Dateref == _cacheStatus.DateRegister ? backgroudoptions.Delay : TimeSpan.Zero);
                            var period = aux.PolicyPeriod ?? backgroudoptions.HealthyPeriod;
                            policy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, delay, period, item.Name);
                        }
                        break;
                }
                var itemToRum = new HealthCheckRegistration(item.Name, item.Factory, item.FailureStatus, item.Tags, item.Timeout)
                {
                    Delay = policy.PolicyDelay!.Value,
                    Period = policy.PolicyPeriod!.Value
                };

                if (sta.Dateref == _cacheStatus.DateRegister)
                {
                    if (_cacheStatus.DateRegister.Add(itemToRum.Delay.Value) < DateTime.Now)
                    {
                        _cacheStatus.Running(item.Name, true);
                        registrationstorun.Add(itemToRum);
                    }
                }
                else
                {
                    if (sta.Dateref.Add(itemToRum.Period.Value) < DateTime.Now)
                    {
                        _cacheStatus.Running(item.Name, true);
                        registrationstorun.Add(itemToRum);
                    }
                }
            }

            if (registrationstorun.Count != 0)
            {
                var tasks = new Task<HealthReportEntry>[registrationstorun.Count];
                var index = 0;

                var dtref = DateTime.Now;
                foreach (var registration in registrationstorun)
                {
                    tasks[index++] = Task.Run(() => RunCheckAsync(registration, cancellationToken), cancellationToken);
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);

                index = 0;
                foreach (var registration in registrationstorun)
                {
                    var item = new HealthCheckResult(tasks[index].Result.Status,
                        tasks[index].Result.Description,
                        tasks[index].Result.Exception,
                        tasks[index].Result.Data);
                    _cacheStatus.Update(
                        registration.Name,
                        HealthCheckTrigger.BackGround,
                        item,
                        dtref.Add(tasks[index].Result.Duration),
                        tasks[index].Result.Duration);
                    index++;
                }
                _cacheStatus.UpdateStatusName();
            }
        }

        public HealthReport CreateReport()
        {
            return _cacheStatus.CreateReport();
        }
        public DateTime? LastReport()
        {
            return _cacheStatus.LastReport();
        }

        private async Task<HealthReportEntry> RunCheckAsync(HealthCheckRegistration registration, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var scope = _scopeFactory.CreateScope();
            var healthCheck = registration.Factory(scope.ServiceProvider);

            // If the health check does things like make Database queries using EF or backend HTTP calls,
            // it may be valuable to know that logs it generates are part of a health check. So we start a scope.
            using (_logger.BeginScope(new HealthCheckLogScopePlus(registration.Name)))
            {
                var stopwatch = Stopwatch.StartNew();
                var context = new HealthCheckContext { Registration = registration };

                Log.HealthCheckBegin(_logger, registration.Name);

                HealthReportEntry entry;
                CancellationTokenSource? timeoutCancellationTokenSource = null;
                try
                {
                    HealthCheckResult result;

                    var checkCancellationToken = cancellationToken;
                    if (registration.Timeout > TimeSpan.Zero)
                    {
                        timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        timeoutCancellationTokenSource.CancelAfter(registration.Timeout);
                        checkCancellationToken = timeoutCancellationTokenSource.Token;
                    }

                    result = await healthCheck.CheckHealthAsync(context, checkCancellationToken).ConfigureAwait(false);

                    var duration = stopwatch.Elapsed;

                    entry = new HealthReportEntry(
                        status: result.Status,
                        description: result.Description,
                        duration: duration,
                        exception: result.Exception,
                        data: result.Data,
                        tags: registration.Tags);

                    Log.HealthCheckEnd(_logger, registration, entry, duration);
                    Log.HealthCheckData(_logger, registration, entry);

                }
                catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    var duration = stopwatch.Elapsed;
                    entry = new HealthReportEntry(
                        status: registration.FailureStatus,
                        description: "A timeout occurred while running check.",
                        duration: duration,
                        exception: ex,
                        data: null,
                        tags: registration.Tags);

                    Log.HealthCheckError(_logger, registration, ex, duration);
                }

                // Allow cancellation to propagate if it's not a timeout.
                catch (Exception ex) when (ex as OperationCanceledException == null)
                {
                    var duration = stopwatch.Elapsed;
                    entry = new HealthReportEntry(
                        status: registration.FailureStatus,
                        description: ex.Message,
                        duration: duration,
                        exception: ex,
                        data: null,
                        tags: registration.Tags);

                    Log.HealthCheckError(_logger, registration, ex, duration);
                }

                finally
                {
                    stopwatch.Stop();
                    timeoutCancellationTokenSource?.Dispose();
                }
                return entry;
            }
        }

        private static void ValidateRegistrations(IEnumerable<HealthCheckRegistration> registrations)
        {
            // Scan the list for duplicate names to provide a better error if there are duplicates.

            StringBuilder? builder = null;
            var distinctRegistrations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var registration in registrations)
            {
                if (!distinctRegistrations.Add(registration.Name))
                {
                    builder ??= new StringBuilder("Duplicate health checks were registered with the name(s): ");

                    builder.Append(registration.Name).Append(", ");
                }
            }

            if (builder is not null)
            {
                throw new ArgumentException(builder.ToString(0, builder.Length - 2), nameof(registrations));
            }
        }

#pragma warning disable IDE0079
        private static partial class Log
        {
            [LoggerMessage(EventIds.HealthCheckProcessingBeginId, LogLevel.Debug, "Running health checks", EventName = EventIds.HealthCheckProcessingBeginName)]
            public static partial void HealthCheckProcessingBegin(ILogger logger);

            public static void HealthCheckProcessingEnd(ILogger logger, HealthStatus status, TimeSpan duration) =>
                HealthCheckProcessingEnd(logger, status, duration.TotalMilliseconds);

            [LoggerMessage(EventIds.HealthCheckProcessingEndId, LogLevel.Debug, "Health check processing with combined status {HealthStatus} completed after {ElapsedMilliseconds}ms", EventName = EventIds.HealthCheckProcessingEndName)]
            private static partial void HealthCheckProcessingEnd(ILogger logger, HealthStatus HealthStatus, double ElapsedMilliseconds);

            [LoggerMessage(EventIds.HealthCheckBeginId, LogLevel.Debug, "Running health check {HealthCheckName}", EventName = EventIds.HealthCheckBeginName)]
            public static partial void HealthCheckBegin(ILogger logger, string HealthCheckName);

            // These are separate so they can have different log levels
            private const string HealthCheckEndText = "Health check {HealthCheckName} with status {HealthStatus} completed after {ElapsedMilliseconds}ms with message '{HealthCheckDescription}'";

            [LoggerMessage(EventIds.HealthCheckEndId, LogLevel.Debug, HealthCheckEndText, EventName = EventIds.HealthCheckEndName)]
            private static partial void HealthCheckEndHealthy(ILogger logger, string HealthCheckName, HealthStatus HealthStatus, double ElapsedMilliseconds, string? HealthCheckDescription);

#pragma warning disable SYSLIB1006 // Multiple logging methods cannot use the same event id within a class
#pragma warning disable SYSLIB1025 // Multiple logging methods should not use the same event name within a class
            [LoggerMessage(EventIds.HealthCheckEndId, LogLevel.Warning, HealthCheckEndText, EventName = EventIds.HealthCheckEndName)]
#pragma warning restore SYSLIB1025 // Multiple logging methods should not use the same event name within a class
#pragma warning restore SYSLIB1006 // Multiple logging methods cannot use the same event id within a class
            private static partial void HealthCheckEndDegraded(ILogger logger, string HealthCheckName, HealthStatus HealthStatus, double ElapsedMilliseconds, string? HealthCheckDescription, Exception? exception);

#pragma warning disable SYSLIB1006 // Multiple logging methods cannot use the same event id within a class
#pragma warning disable SYSLIB1025 // Multiple logging methods should not use the same event name within a class
            [LoggerMessage(EventIds.HealthCheckEndId, LogLevel.Error, HealthCheckEndText, EventName = EventIds.HealthCheckEndName)]
#pragma warning restore SYSLIB1025 // Multiple logging methods should not use the same event name within a class
#pragma warning restore SYSLIB1006 // Multiple logging methods cannot use the same event id within a class
            private static partial void HealthCheckEndUnhealthy(ILogger logger, string HealthCheckName, HealthStatus HealthStatus, double ElapsedMilliseconds, string? HealthCheckDescription, Exception? exception);

            public static void HealthCheckEnd(ILogger logger, HealthCheckRegistration registration, HealthReportEntry entry, TimeSpan duration)
            {
                switch (entry.Status)
                {
                    case HealthStatus.Healthy:
                        HealthCheckEndHealthy(logger, registration.Name, entry.Status, duration.TotalMilliseconds, entry.Description);
                        break;

                    case HealthStatus.Degraded:
                        HealthCheckEndDegraded(logger, registration.Name, entry.Status, duration.TotalMilliseconds, entry.Description, entry.Exception);
                        break;

                    case HealthStatus.Unhealthy:
                        HealthCheckEndUnhealthy(logger, registration.Name, entry.Status, duration.TotalMilliseconds, entry.Description, entry.Exception);
                        break;
                }
            }

            [LoggerMessage(EventIds.HealthCheckErrorId, LogLevel.Error, "Health check {HealthCheckName} threw an unhandled exception after {ElapsedMilliseconds}ms", EventName = EventIds.HealthCheckErrorName)]
            private static partial void HealthCheckError(ILogger logger, string HealthCheckName, double ElapsedMilliseconds, Exception exception);

            public static void HealthCheckError(ILogger logger, HealthCheckRegistration registration, Exception exception, TimeSpan duration) =>
                HealthCheckError(logger, registration.Name, duration.TotalMilliseconds, exception);

            public static void HealthCheckData(ILogger logger, HealthCheckRegistration registration, HealthReportEntry entry)
            {
                if (entry.Data.Count > 0 && logger.IsEnabled(LogLevel.Debug))
                {
                    logger.Log(
                        LogLevel.Debug,
                        EventIds.HealthCheckData,
                        new HealthCheckDataLogValue(registration.Name, entry.Data),
                        null,
                        (state, ex) => state.ToString());
                }
            }
        }
#pragma warning disable IDE0079

        private sealed class HealthCheckDataLogValue : IReadOnlyList<KeyValuePair<string, object>>
        {
            private readonly string _name;
            private readonly List<KeyValuePair<string, object>> _values;

            private string? _formatted;

            public HealthCheckDataLogValue(string name, IReadOnlyDictionary<string, object> values)
            {
                _name = name;
                _values = [.. values];

                // We add the name as a kvp so that you can filter by health check name in the logs.
                // This is the same parameter name used in the other logs.
                _values.Add(new KeyValuePair<string, object>("HealthCheckName", name));
            }

            public KeyValuePair<string, object> this[int index]
            {
                get
                {
                    if (index < 0 || index >= Count)
                    {
                        throw new ArgumentOutOfRangeException(nameof(index));
                    }

                    return _values[index];
                }
            }

            public int Count => _values.Count;

            public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
            {
                return _values.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return _values.GetEnumerator();
            }

            public override string ToString()
            {
                if (_formatted == null)
                {
                    var builder = new StringBuilder();
                    builder.AppendLine(FormattableString.Invariant($"Health check data for {_name}:"));

                    var values = _values;
                    for (var i = 0; i < values.Count; i++)
                    {
                        var kvp = values[i];
                        builder.Append("    ");
                        builder.Append(kvp.Key);
                        builder.Append(": ");
                        builder.AppendLine((kvp.Value??"Null").ToString());
                    }

                    _formatted = builder.ToString();
                }

                return _formatted;
            }
        }

        private static class EventIds
        {
            public const int HealthCheckProcessingBeginId = 100;
            public const int HealthCheckProcessingEndId = 101;
            public const int HealthCheckBeginId = 102;
            public const int HealthCheckEndId = 103;
            public const int HealthCheckErrorId = 104;
            public const int HealthCheckDataId = 105;

            // Hard code the event names to avoid breaking changes. Even if the methods are renamed, these hard-coded names shouldn't change.
            public const string HealthCheckProcessingBeginName = "HealthCheckProcessingBegin";
            public const string HealthCheckProcessingEndName = "HealthCheckProcessingEnd";
            public const string HealthCheckBeginName = "HealthCheckBegin";
            public const string HealthCheckEndName = "HealthCheckEnd";
            public const string HealthCheckErrorName = "HealthCheckError";
            public const string HealthCheckDataName = "HealthCheckData";

            public static readonly EventId HealthCheckData = new(HealthCheckDataId, HealthCheckDataName);
        }

        private class HealthCheckLogScopePlus(string healthCheckName) : IReadOnlyList<KeyValuePair<string, object>>
        {
            public string HealthCheckName { get; } = healthCheckName;

            int IReadOnlyCollection<KeyValuePair<string, object>>.Count { get; } = 1;

            KeyValuePair<string, object> IReadOnlyList<KeyValuePair<string, object>>.this[int index]
            {
                get
                {
                    if (index == 0)
                    {
                        return new KeyValuePair<string, object>(nameof(HealthCheckName), HealthCheckName);
                    }

                    throw new ArgumentOutOfRangeException(nameof(index));
                }
            }

            IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
            {
                yield return new KeyValuePair<string, object>(nameof(HealthCheckName), HealthCheckName);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return ((IEnumerable<KeyValuePair<string, object>>)this).GetEnumerator();
            }
        }

    }
}
