// ********************************************************************************************
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// ********************************************************************************************
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Abstractions;
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
    internal partial class DefaultHealthCheckServicePlus : HealthCheckService, IDisposable
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptions<HealthCheckServiceOptions> _options;
        private readonly IServiceProvider _services;
        private readonly ILogger<HealthCheckService> _logger;
        private readonly List<IHealthCheckPlusPolicyStatus> _policies;
        private readonly CacheHealthCheckPlus _cacheStatus;
        private readonly HealthChecksPlusRegistrationState _registrationState;

        public DefaultHealthCheckServicePlus(
            IServiceScopeFactory scopeFactory,
            IServiceProvider services,
            ILogger<HealthCheckService> logger,
            IOptions<HealthCheckServiceOptions> options,
            HealthChecksPlusRegistrationState registrationState)
        {
            _services = services;
            _logger = logger;
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _registrationState = registrationState ?? throw new ArgumentNullException(nameof(registrationState));
            // We're specifically going out of our way to do this at startup time. We want to make sure you
            // get any kind of health-check related error as early as possible. Waiting until someone
            // actually tries to **run** health checks would be real baaaaad.
            ValidateRegistrations(_options.Value.Registrations);

            _policies = [];
            _policies.AddRange(_services
                .GetServices<IHealthCheckPlusPolicyStatus>());

            _cacheStatus = (CacheHealthCheckPlus)_services.GetRequiredService<IStateHealthChecksPlus>();

            ValidateHealthyPolicies(_options.Value.Registrations, _policies);
            ValidateCacheRegistrations(_options.Value.Registrations, _cacheStatus);
        }

        // DefaultHealthCheckServicePlus is a container-constructed singleton (registered via
        // TryAddSingleton<HealthCheckService, DefaultHealthCheckServicePlus>() — a factory
        // registration, not a ready-made instance), so the container reliably disposes it at host
        // shutdown. Piggybacking the adopted external check instances' disposal here matters
        // because those instances live in HealthChecksPlusRegistrationState, which — unlike this
        // class — is registered as a ready-made instance and therefore is NOT disposed
        // automatically by the container (documented .NET DI behavior), so nothing else disposes
        // them at shutdown.
        public void Dispose()
        {
            foreach (var (name, lazyWrapper) in _registrationState.ExternalCheck)
            {
                if (!lazyWrapper.IsValueCreated)
                {
                    // Never actually constructed (adopted but never scheduled to run before
                    // shutdown) - nothing to dispose, and evaluating .Value here would construct
                    // it just to immediately dispose it.
                    continue;
                }

                try
                {
                    lazyWrapper.Value.Dispose();
                }
                catch (Exception ex)
                {
                    // A consumer-supplied IDisposable.Dispose() throwing must not abort the loop:
                    // without this try/catch, every adopted check after the first failing one would
                    // be silently left undisposed for the rest of process shutdown, with no signal
                    // anywhere that it happened.
                    Log.HealthCheckDisposeError(_logger, name, ex);

                    try
                    {
                        HealthCheckPlusMetrics.RecordAnomaly(AnomalyReason.AdoptedCheckDisposeFailed);
                    }
                    catch (Exception metricsEx)
                    {
                        // Metrics must never be able to break Dispose() either (same MeterListener
                        // risk as everywhere else metrics are recorded). Logged explicitly here
                        // rather than swallowed, since the log above is about the dispose failure,
                        // not about this separate metrics-recording failure.
                        Log.HealthCheckMetricsRecordingError(_logger, metricsEx);
                    }
                }
            }
        }

        // A health check with no matching Healthy policy (i.e. registered without going through
        // AddCheckPlus/AddCheckLinkTo) must fail early and clearly, instead of throwing a
        // NullReferenceException later, at runtime, when health is evaluated.
        private static void ValidateHealthyPolicies(IEnumerable<HealthCheckRegistration> registrations, List<IHealthCheckPlusPolicyStatus> policies)
        {
            var missing = registrations
                .Where(r => !policies.Any(p => p.PolicyForStatus == HealthStatus.Healthy && p.PolicyNameDep == r.Name))
                .Select(r => r.Name)
                .ToArray();

            if (missing.Length > 0)
            {
                throw new InvalidOperationException(
                    "The following health checks have no HealthCheckPlus policy registered: " +
                    string.Join(", ", missing) +
                    ". Register them with AddCheckPlus or AddCheckLinkTo before building the service provider.");
            }
        }

        // The opposite direction of the same misconfiguration ValidateHealthyPolicies guards
        // against: a registration with a Healthy policy (as AddCheckPlus/AddCheckLinkTo always
        // produce) but whose name was left out of the `names` list passed to AddHealthChecksPlus -
        // that list is what seeds the cache (CacheHealthCheckPlus.InitCache), so a name missing
        // from it has no cache entry. Left unchecked, every later request/cycle would hit
        // CacheHealthCheckPlus.FullStatus's raw dictionary indexer for that name and crash with an
        // unhandled KeyNotFoundException.
        private static void ValidateCacheRegistrations(IEnumerable<HealthCheckRegistration> registrations, CacheHealthCheckPlus cacheStatus)
        {
            var missing = registrations
                .Where(r => !cacheStatus.IsRegistered(r.Name))
                .Select(r => r.Name)
                .ToArray();

            if (missing.Length > 0)
            {
                throw new InvalidOperationException(
                    "The following health checks are registered but missing from the names list passed to AddHealthChecksPlus: " +
                    string.Join(", ", missing) +
                    ". Add them to that list before building the service provider.");
            }
        }

        // Shared by both execution paths (CheckHealthPlusAsync and BackGroudCheckHealthPlusAsync)
        // so that policy lookup can never diverge between them.
        private IHealthCheckPlusPolicyStatus? FindPolicy(string name, HealthStatus status)
        {
            return _policies.FirstOrDefault(x => x.PolicyNameDep == name && x.PolicyForStatus == status);
        }

        // Guaranteed non-null: ValidateHealthyPolicies (called from the constructor) already
        // rejected any registration without a matching Healthy policy.
        private IHealthCheckPlusPolicyStatus GetHealthyPolicy(string name)
        {
            return FindPolicy(name, HealthStatus.Healthy)!;
        }

        // Foreground/HTTP path: fall back to the check's own Healthy policy when there is no
        // policy registered for the current status.
        private IHealthCheckPlusPolicyStatus ResolveForegroundPolicy(string name, HealthStatus lastStatus)
        {
            return FindPolicy(name, lastStatus) ?? GetHealthyPolicy(name);
        }

        // Background path: fall back to the background service's own per-status defaults
        // (HealthCheckPlusBackGroundOptions) when there is no explicit policy for the current
        // status — this fallback source is only available on this path, since
        // HealthCheckPlusBackGroundOptions only exists when AddBackgroundPolicy was used.
        private IHealthCheckPlusPolicyStatus ResolveBackgroundPolicy(string name, ItemCacheHealth sta, HealthCheckPlusBackGroundOptions backgroudoptions)
        {
            switch (sta.LastResult.Status)
            {
                case HealthStatus.Unhealthy:
                    return FindPolicy(name, HealthStatus.Unhealthy)
                        ?? new HealthCheckPlusPolicyStatus(HealthStatus.Unhealthy, backgroudoptions.Delay, backgroudoptions.UnhealthyPeriod, name);

                case HealthStatus.Degraded:
                    return FindPolicy(name, HealthStatus.Degraded)
                        ?? new HealthCheckPlusPolicyStatus(HealthStatus.Degraded, backgroudoptions.Delay, backgroudoptions.DegradedPeriod, name);

                default: // HealthStatus.Healthy
                    {
                        var healthy = GetHealthyPolicy(name);
                        var delay = healthy.PolicyDelay ?? (sta.DateRef == _cacheStatus.DateRegister ? backgroudoptions.Delay : TimeSpan.Zero);
                        var period = healthy.PolicyPeriod ?? backgroudoptions.HealthyPeriod;
                        return new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, delay, period, name);
                    }
            }
        }

        // Builds the registration to run (with the resolved policy's Delay/Period applied) and
        // atomically checks-and-marks it running in the cache, but only if its schedule is
        // actually due. Shared by both execution paths. `fallbackWhenNull` covers the case where a
        // registered policy left Delay/Period unset (e.g. AddCheckPlus without explicit values,
        // foreground-only usage).
        //
        // The due-check and marking the check Running must happen as one atomic operation
        // (CacheHealthCheckPlus.TryBeginRun), not two separate steps - otherwise two concurrent
        // callers for the same check (e.g. an HTTP request and a background cycle) could both read
        // "not running, due" before either marked it, both schedule the same check, and run it
        // twice concurrently, with whichever finished second having its result silently dropped by
        // Update() (see AnomalyReason.UpdateResultDropped).
        private HealthCheckRegistration? ScheduleIfDue(HealthCheckRegistration item, IHealthCheckPlusPolicyStatus policy, TimeSpan fallbackWhenNull)
        {
            var itemToRun = new HealthCheckRegistration(item.Name, item.Factory, item.FailureStatus, item.Tags, item.Timeout)
            {
                Delay = policy.PolicyDelay ?? fallbackWhenNull,
                Period = policy.PolicyPeriod ?? fallbackWhenNull
            };

            var began = _cacheStatus.TryBeginRun(itemToRun.Name, current =>
                current.DateRef == _cacheStatus.DateRegister
                    ? _cacheStatus.DateRegister.Add(itemToRun.Delay!.Value) < DateTime.UtcNow
                    : current.DateRef.Add(itemToRun.Period!.Value) < DateTime.UtcNow);

            return began ? itemToRun : null;
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
                var sta = _cacheStatus.FullStatus(item.Name);
                var policy = ResolveForegroundPolicy(item.Name, sta.LastResult.Status);

                var itemToRun = ScheduleIfDue(item, policy, TimeSpan.Zero);
                if (itemToRun != null)
                {
                    registrationstorun.Add(itemToRun);
                }
            }


            var totalTime = Stopwatch.StartNew();

            var entries = new Dictionary<string, HealthReportEntry>(StringComparer.OrdinalIgnoreCase);

            if (registrationstorun.Count != 0)
            {
                Log.HealthCheckProcessingBegin(_logger);

                var tasks = new Task<HealthReportEntry>[registrationstorun.Count];
                var index = 0;

                var dtref = DateTime.UtcNow;
                foreach (var registration in registrationstorun)
                {
                    tasks[index++] = Task.Run(() => RunCheckAsync(registration, cancellationToken), cancellationToken);
                }

                try
                {
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
                finally
                {
                    // ScheduleIfDue/TryBeginRun already marked every one of these checks Running
                    // before the tasks above were started, and Update() is the only thing that
                    // clears it. That must still happen here even when Task.WhenAll faults - the
                    // ambient cancellationToken firing mid-flight (e.g. httpContext.RequestAborted)
                    // is deliberately not swallowed by RunCheckAsync - otherwise every check in
                    // this batch would stay marked Running forever and never be scheduled again.
                    // The exception, if any, still propagates normally once this finally block
                    // completes, so callers see the same behavior as before.
                    totalTime.Stop();

                    index = 0;
                    foreach (var registration in registrationstorun)
                    {
                        var task = tasks[index++];
                        HealthCheckResult result;
                        TimeSpan duration;
                        if (task.IsCompletedSuccessfully)
                        {
                            result = new HealthCheckResult(task.Result.Status, task.Result.Description, task.Result.Exception, task.Result.Data);
                            duration = task.Result.Duration;
                        }
                        else
                        {
                            result = new HealthCheckResult(registration.FailureStatus, "The health check did not complete because the operation was cancelled.");
                            duration = TimeSpan.Zero;
                        }
                        _cacheStatus.Update(registration.Name, resultHealthCheckFrom, result, dtref.Add(duration), duration);
                    }
                }
            }

            foreach (var registration in registrations)
            {
                var sta = _cacheStatus.FullStatus(registration.Name);
                var result = new HealthReportEntry(sta.LastResult.Status,
                    sta.LastResult.Description, sta.Duration, sta.LastResult.Exception, sta.LastResult.Data, registration.Tags);
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
                var sta = _cacheStatus.FullStatus(item.Name);
                var policy = ResolveBackgroundPolicy(item.Name, sta, backgroudoptions);

                var itemToRun = ScheduleIfDue(item, policy, TimeSpan.Zero);
                if (itemToRun != null)
                {
                    registrationstorun.Add(itemToRun);
                }
            }

            if (registrationstorun.Count != 0)
            {
                var tasks = new Task<HealthReportEntry>[registrationstorun.Count];
                var index = 0;

                var dtref = DateTime.UtcNow;
                foreach (var registration in registrationstorun)
                {
                    tasks[index++] = Task.Run(() => RunCheckAsync(registration, cancellationToken), cancellationToken);
                }

                try
                {
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
                finally
                {
                    // See the matching comment in CheckHealthPlusAsync: Update() must run for
                    // every check in this batch (releasing TryBeginRun's Running flag) even when
                    // Task.WhenAll faults - here, typically the per-cycle Timeout cancelling this
                    // call's linked token while a check is still in flight - otherwise it would
                    // stay marked Running forever and never run again on any later cycle. The
                    // exception still propagates afterward so HealthCheckPlusBackGroundService's
                    // own timeout/shutdown handling around this call is unaffected.
                    index = 0;
                    foreach (var registration in registrationstorun)
                    {
                        var task = tasks[index++];
                        HealthCheckResult result;
                        TimeSpan duration;
                        if (task.IsCompletedSuccessfully)
                        {
                            result = new HealthCheckResult(task.Result.Status, task.Result.Description, task.Result.Exception, task.Result.Data);
                            duration = task.Result.Duration;
                        }
                        else
                        {
                            result = new HealthCheckResult(registration.FailureStatus, "The health check did not complete because the operation was cancelled.");
                            duration = TimeSpan.Zero;
                        }
                        _cacheStatus.Update(registration.Name, HealthCheckTrigger.Background, result, dtref.Add(duration), duration);
                    }
                    _cacheStatus.UpdateStatusName();
                }
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

            [LoggerMessage(EventIds.HealthCheckDisposeErrorId, LogLevel.Warning,
                "Disposing the adopted external health check '{HealthCheckName}' threw an exception; continuing to dispose the remaining adopted checks.",
                EventName = EventIds.HealthCheckDisposeErrorName)]
            public static partial void HealthCheckDisposeError(ILogger logger, string HealthCheckName, Exception exception);

            [LoggerMessage(EventIds.HealthCheckMetricsRecordingErrorId, LogLevel.Warning,
                "Recording the anomaly metric for a health check dispose failure also failed; the dispose failure itself was already logged above.",
                EventName = EventIds.HealthCheckMetricsRecordingErrorName)]
            public static partial void HealthCheckMetricsRecordingError(ILogger logger, Exception exception);

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
            public const int HealthCheckDisposeErrorId = 106;
            public const int HealthCheckMetricsRecordingErrorId = 107;

            // Hard code the event names to avoid breaking changes. Even if the methods are renamed, these hard-coded names shouldn't change.
            public const string HealthCheckProcessingBeginName = "HealthCheckProcessingBegin";
            public const string HealthCheckProcessingEndName = "HealthCheckProcessingEnd";
            public const string HealthCheckBeginName = "HealthCheckBegin";
            public const string HealthCheckEndName = "HealthCheckEnd";
            public const string HealthCheckErrorName = "HealthCheckError";
            public const string HealthCheckDataName = "HealthCheckData";
            public const string HealthCheckMetricsRecordingErrorName = "HealthCheckPlusMetricsRecordingError";
            public const string HealthCheckDisposeErrorName = "HealthCheckDisposeError";

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
