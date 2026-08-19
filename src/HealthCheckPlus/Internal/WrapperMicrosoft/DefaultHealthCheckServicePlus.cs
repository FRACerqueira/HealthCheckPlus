// ********************************************************************************************
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// ********************************************************************************************
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Abstractions;
using HealthCheckPlus.Internal.Policies;
using HealthCheckPlus.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;

namespace HealthCheckPlus.Internal.WrapperMicrosoft
{
    internal partial class DefaultHealthCheckServicePlus : HealthCheckService, IDisposable
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptions<HealthCheckServiceOptions> _options;
        private readonly IServiceProvider _services;
        private readonly ILogger<HealthCheckService> _logger;
        private readonly Dictionary<(string NormalizedName, HealthStatus Status), HealthCheckPlusPolicyStatus> _policyIndex;
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

            var policies = new List<HealthCheckPlusPolicyStatus>();
            policies.AddRange(_services
                .GetServices<HealthCheckPlusPolicyStatus>());

            _cacheStatus = InternalCast.To<CacheHealthCheckPlus>(_services.GetRequiredService<IStateHealthChecksPlus>(), "the registered IStateHealthChecksPlus");

            // Registrations (and their Tags) aren't known yet when InitCache runs, so the cache's
            // per-check Tags are populated here instead, once the real registrations exist - see
            // ItemCacheHealth.Tags and CacheHealthCheckPlus.CreateReport().
            foreach (var registration in _options.Value.Registrations)
            {
                _cacheStatus.SetTags(registration.Name, registration.Tags);
            }

            ValidateHealthyPolicies(_options.Value.Registrations, policies);
            ValidatePolicyUniqueness(policies);
            ValidatePolicyTargets(_options.Value.Registrations, policies);

            // Indexed by (name, status) for O(1) lookup in FindPolicy below, instead of a linear
            // scan through every policy - BuildDueRegistrations resolves a policy for every
            // registration on every background cycle and every HTTP request, so a linear scan
            // here used to make that cost proportional to registrations × policies (effectively
            // quadratic, since policy count grows with registration count), not just registrations.
            // ValidatePolicyUniqueness above already guarantees no two policies collide on this
            // same (normalized name, status) key, so this can never throw for a duplicate key.
            _policyIndex = policies.ToDictionary(p => (p.PolicyNameDep.ToUpperInvariant(), p.PolicyForStatus));
        }

        // DefaultHealthCheckServicePlus is a container-constructed singleton (registered via
        // TryAddSingleton<HealthCheckService, DefaultHealthCheckServicePlus>() — an
        // implementation-type registration, not a ready-made instance), so the container reliably
        // disposes it at host shutdown. Piggybacking the adopted external check instances'
        // disposal here matters
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
                    // anywhere that it happened. SafeLog guards the log call itself for the same
                    // reason - a throwing ILogger sink here must not reintroduce the exact bug this
                    // try/catch exists to prevent.
                    SafeLog(() => Log.HealthCheckDisposeError(_logger, name, ex));

                    SafeRecordMetric(() => HealthCheckPlusMetrics.RecordAnomaly(AnomalyReason.AdoptedCheckDisposeFailed));
                }
            }
        }

        // A MeterListener callback (e.g. a third-party OTel exporter) runs synchronously on this
        // thread, so a bug in it must never be allowed to break Dispose() or check execution either
        // (same risk as everywhere else metrics are recorded). Every RecordAnomaly call in this
        // class goes through here.
        private void SafeRecordMetric(Action recordMetric)
        {
            try
            {
                recordMetric();
            }
            catch (Exception metricsEx)
            {
                SafeLog(() => Log.HealthCheckMetricsRecordingError(_logger, metricsEx));
            }
        }

        // Mirror image of SafeRecordMetric above: a throwing ILogger sink (a broken third-party
        // logging provider) must never be able to break Dispose(), a batch, or a single check's
        // execution any more than a broken metrics pipeline can. The failure becomes observable via
        // AnomalyReason.LoggingSinkFailed instead of another log call - see that reason's doc for
        // why (same circularity risk SafeRecordMetric's own catch above already avoids, mirrored).
        //
        // Undefended residual: the RecordAnomaly call in the catch below is itself unguarded, so a
        // second, independent failure recording THAT (a broken MeterListener) still propagates out
        // of SafeLog and, transitively, out of whatever called it. Closing that fully would need a
        // nested empty catch here, which this codebase's own no-silent-catch doctrine treats as a
        // decision requiring explicit sign-off, not something to add unasked - left as a known gap.
        private void SafeLog(Action logCall) => HealthCheckPlusMetrics.SafeLog(logCall);

        // A health check with no matching Healthy policy (i.e. registered without going through
        // AddCheckPlus/AddCheckLinkTo) must fail early and clearly, instead of throwing a
        // NullReferenceException later, at runtime, when health is evaluated. Compared
        // OrdinalIgnoreCase, matching FindPolicy's own comparison below and CacheHealthCheckPlus's
        // _statusDeps - a policy name differing only in casing from its check's registered Name is
        // still the same check everywhere else in this library.
        private static void ValidateHealthyPolicies(IEnumerable<HealthCheckRegistration> registrations, List<HealthCheckPlusPolicyStatus> policies)
        {
            var missing = registrations
                .Where(r => !policies.Any(p => p.PolicyForStatus == HealthStatus.Healthy && string.Equals(p.PolicyNameDep, r.Name, StringComparison.OrdinalIgnoreCase)))
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

        // AddUnhealthyPolicy/AddDegradedPolicy/AddCheckPlus/AddCheckLinkTo all register a policy via
        // IServiceCollection.AddSingleton, which accumulates rather than replaces - calling one of
        // them twice for the same check and status used to collide silently: FindPolicy's
        // FirstOrDefault always picks whichever was registered first, so the second call's
        // Delay/Period was ignored with no warning at all. Grouped by the upper-invariant name (not
        // the raw name) so two calls differing only in casing are caught as the same duplicate too -
        // FindPolicy below would already treat them as one policy at lookup time.
        private static void ValidatePolicyUniqueness(List<HealthCheckPlusPolicyStatus> policies)
        {
            var duplicates = policies
                .GroupBy(p => (p.PolicyNameDep.ToUpperInvariant(), p.PolicyForStatus))
                .Where(g => g.Count() > 1)
                .Select(g => $"{g.First().PolicyNameDep} ({g.Key.PolicyForStatus})")
                .ToArray();

            if (duplicates.Length > 0)
            {
                throw new InvalidOperationException(
                    "The following health checks have more than one policy registered for the same status: " +
                    string.Join(", ", duplicates) +
                    ". Call AddUnhealthyPolicy/AddDegradedPolicy/AddCheckPlus/AddCheckLinkTo for a given check and status only once.");
            }
        }

        // ValidateHealthyPolicies only catches the opposite direction (a registered check with no
        // matching Healthy policy). Nothing previously caught a policy naming a check that was
        // never actually registered - e.g. a typo in AddUnhealthyPolicy/AddDegradedPolicy's
        // `namedep` - which used to silently register a policy that FindPolicy could never match
        // against any real check, with no signal that it was doing nothing. OrdinalIgnoreCase,
        // matching FindPolicy's own comparison.
        private static void ValidatePolicyTargets(IEnumerable<HealthCheckRegistration> registrations, List<HealthCheckPlusPolicyStatus> policies)
        {
            var registeredNames = new HashSet<string>(registrations.Select(r => r.Name), StringComparer.OrdinalIgnoreCase);

            var orphaned = policies
                .Where(p => !registeredNames.Contains(p.PolicyNameDep))
                .Select(p => $"{p.PolicyNameDep} ({p.PolicyForStatus})")
                .Distinct()
                .ToArray();

            if (orphaned.Length > 0)
            {
                throw new InvalidOperationException(
                    "The following HealthCheckPlus policies target a health check name that isn't registered: " +
                    string.Join(", ", orphaned) +
                    ". Check for a typo between the policy's name and the check's actual registered name.");
            }
        }

        // Shared by both execution paths (CheckHealthPlusAsync and BackGroudCheckHealthPlusAsync)
        // so that policy lookup can never diverge between them. O(1) via _policyIndex rather than
        // a linear scan. OrdinalIgnoreCase (via the same upper-invariant normalization used to
        // build _policyIndex) to match CacheHealthCheckPlus's _statusDeps and HealthReport.Entries
        // - a policy registered for a name differing only in casing from the check's actual
        // registered Name is still found.
        private HealthCheckPlusPolicyStatus? FindPolicy(string name, HealthStatus status)
        {
            return _policyIndex.TryGetValue((name.ToUpperInvariant(), status), out var policy) ? policy : null;
        }

        // Guaranteed non-null: ValidateHealthyPolicies (called from the constructor) already
        // rejected any registration without a matching Healthy policy.
        private HealthCheckPlusPolicyStatus GetHealthyPolicy(string name)
        {
            return FindPolicy(name, HealthStatus.Healthy)!;
        }

        // Foreground/HTTP path: fall back to the check's own Healthy policy when there is no
        // policy registered for the current status.
        private HealthCheckPlusPolicyStatus ResolveForegroundPolicy(string name, HealthStatus lastStatus)
        {
            return FindPolicy(name, lastStatus) ?? GetHealthyPolicy(name);
        }

        // Background path: fall back to the background service's own per-status defaults
        // (HealthCheckPlusBackGroundOptions) when there is no explicit policy for the current
        // status — this fallback source is only available on this path, since
        // HealthCheckPlusBackGroundOptions only exists when AddBackgroundPolicy was used.
        private HealthCheckPlusPolicyStatus ResolveBackgroundPolicy(string name, ItemCacheHealth sta, HealthCheckPlusBackGroundOptions backgroudoptions)
        {
            // Read once and reuse for both the status switch and the DateRef comparison below -
            // sta is the live, mutable cache item, and LastResult/DateRef are two different
            // properties of the same underlying snapshot (see ItemCacheHealth.Snapshot). Reading
            // them as two separate property accesses risked a concurrent Update() landing in
            // between: the Healthy branch chosen from one generation's status, paired with a
            // DateRef from the next generation, could make a genuinely-first-run check compare
            // unequal to DateRegister and fall through to TimeSpan.Zero instead of Delay.
            var snapshot = sta.Snapshot;
            switch (snapshot.LastResult.Status)
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
                        var delay = healthy.PolicyDelay ?? (snapshot.DateRef == _cacheStatus.DateRegister ? backgroudoptions.Delay : TimeSpan.Zero);
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
        private HealthCheckRegistration? ScheduleIfDue(HealthCheckRegistration item, HealthCheckPlusPolicyStatus policy, TimeSpan fallbackWhenNull)
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

        // Shared by both execution paths: resolves each candidate registration's policy (the only
        // part that differs between them - see the resolvePolicy callers in CheckHealthPlusAsync
        // and BackGroudCheckHealthPlusAsync) and filters down to the ones actually due via
        // ScheduleIfDue.
        //
        // ScheduleIfDue/TryBeginRun marks a check Running as soon as it decides that check is due -
        // one item at a time, inside this loop. If a LATER item's FullStatus/resolvePolicy throws
        // (FullStatus throwing means the cache and _options.Value.Registrations have somehow
        // diverged; resolvePolicy throwing would mean the "every registration has a Healthy policy"
        // invariant ValidateHealthyPolicies enforces at startup was somehow violated - neither is
        // reachable through the public API today, but nothing prevents a future change from making
        // one of them reachable), every item already marked Running earlier in this same loop would
        // otherwise be lost - registrationstorun is local and never returned, so nothing else could
        // ever release them, and they'd stay marked Running (and therefore un-schedulable) forever.
        private List<HealthCheckRegistration> BuildDueRegistrations(
            IEnumerable<HealthCheckRegistration> registrations,
            Func<HealthCheckRegistration, ItemCacheHealth, HealthCheckPlusPolicyStatus> resolvePolicy)
        {
            var registrationstorun = new List<HealthCheckRegistration>();
            try
            {
                foreach (var item in registrations)
                {
                    var sta = _cacheStatus.FullStatus(item.Name);
                    var policy = resolvePolicy(item, sta);

                    var itemToRun = ScheduleIfDue(item, policy, TimeSpan.Zero);
                    if (itemToRun != null)
                    {
                        registrationstorun.Add(itemToRun);
                    }
                }
            }
            catch
            {
                ReleaseRunningForBatch(registrationstorun, DateTime.UtcNow);
                throw;
            }
            return registrationstorun;
        }

        // Shared release-on-failure fallback for a batch BuildDueRegistrations/TryBeginRun already
        // marked Running, used wherever something can throw after that marking but before
        // ApplyBatchResults gets a chance to run for the batch (which normally does the releasing).
        // Safe to call even for an item ApplyBatchResults already handled: ReleaseRunning just
        // re-preserves whatever result is already there, only nudging DateRef - the call sites below
        // only ever reach this for items ApplyBatchResults never got the chance to touch.
        private void ReleaseRunningForBatch(IReadOnlyList<HealthCheckRegistration> registrationstorun, DateTime releasedAt)
        {
            foreach (var registration in registrationstorun)
            {
                _cacheStatus.ReleaseRunning(registration.Name, releasedAt);
            }
        }

        // Shared by both execution paths: fans every due registration out to its own
        // RunCheckAsync task. Awaiting and classifying the results is the caller's
        // responsibility (see ApplyBatchResults) - this only starts them.
        //
        // Considered and consciously not fixed: if Task.Run itself throws synchronously partway
        // through this loop (in practice, only reachable via an OutOfMemoryException while
        // scheduling - RunCheckAsync's own body never runs synchronously here), the caller's
        // catch block releases Running for every registration in the batch, including the ones
        // whose tasks are already running in the background from earlier loop iterations - those
        // tasks can't be un-scheduled, so releasing their Running flag could in principle let
        // TryBeginRun schedule the same check again while the orphaned task is still executing.
        // Correctly handling this would mean threading a partial-success/partial-orphan result out
        // of this method instead of a plain array-or-throw, solely to cover a condition that only
        // arises when the process is already failing to allocate memory - not worth the extra
        // surface area on this hot path for that.
        private Task<HealthReportEntry>[] StartBatch(List<HealthCheckRegistration> registrationstorun, CancellationToken cancellationToken)
        {
            var tasks = new Task<HealthReportEntry>[registrationstorun.Count];
            for (var index = 0; index < registrationstorun.Count; index++)
            {
                var registration = registrationstorun[index];
                tasks[index] = Task.Run(() => RunCheckAsync(registration, cancellationToken), cancellationToken);
            }
            return tasks;
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
            var registrationstorun = BuildDueRegistrations(registrations, (item, sta) => ResolveForegroundPolicy(item.Name, sta.LastResult.Status));

            var totalTime = Stopwatch.StartNew();

            var entries = new Dictionary<string, HealthReportEntry>(StringComparer.OrdinalIgnoreCase);

            if (registrationstorun.Count != 0)
            {
                // ScheduleIfDue/TryBeginRun already marked every one of these checks Running before
                // this point. Log.HealthCheckProcessingBegin and StartBatch used to run completely
                // unguarded here - if the injected ILogger threw (a broken third-party logging
                // provider/sink), or StartBatch's own Task.Run somehow threw synchronously, the
                // exception propagated before the try/finally below ever got a chance to open, and
                // every check in this batch stayed marked Running forever: TryBeginRun would never
                // schedule it again, and /health would keep serving its frozen cached value with no
                // signal at all - recoverable only by restarting the process. This class already
                // wraps every metrics call in SafeRecordMetric for exactly this class of risk; this
                // one log call was missed. ReleaseRunningForBatch's own doc explains why releasing
                // here is safe even in the (impossible today) case both catches below somehow fired
                // for the same batch.
                //
                // The log call itself now goes through SafeLog rather than relying on this
                // try/catch: a broken logger no longer aborts the whole batch (every check in it
                // reported as failed via ReleaseRunningForBatch+rethrow) just because a debug log
                // line couldn't be written - the checks still run, and only the logging failure
                // itself surfaces, via AnomalyReason.LoggingSinkFailed. The try/catch remains for
                // StartBatch genuinely throwing synchronously, which is a real batch-start failure.
                Task<HealthReportEntry>[] tasks;
                DateTime dtref;
                try
                {
                    SafeLog(() => Log.HealthCheckProcessingBegin(_logger));
                    dtref = DateTime.UtcNow;
                    tasks = StartBatch(registrationstorun, cancellationToken);
                }
                catch
                {
                    ReleaseRunningForBatch(registrationstorun, DateTime.UtcNow);
                    throw;
                }

                // Running must still be released here even when Task.WhenAll faults - the ambient
                // cancellationToken firing mid-flight (e.g. httpContext.RequestAborted) is
                // deliberately not swallowed by RunCheckAsync - otherwise every check in this batch
                // would stay marked Running forever and never be scheduled again. ApplyBatchResults
                // below is what actually releases it, and must run whether or not Task.WhenAll
                // faulted - previously via a `finally`, which meant a second, independent failure
                // inside ApplyBatchResults itself (e.g. a throwing ILogger sink on one item) would
                // silently replace whatever exception Task.WhenAll raised, per ordinary CLR
                // exception-in-finally semantics. Capturing it explicitly instead lets both surface
                // together if both happen, and preserves the original stack trace via
                // ExceptionDispatchInfo in the (normal) single-failure case, instead of a bare
                // `throw ex;` resetting it to this catch block. See ApplyBatchResults for how each
                // task's outcome is classified and applied - shared with BackGroudCheckHealthPlusAsync
                // so there is exactly one place that decides this, instead of two copies that can
                // drift apart.
                // ApplyBatchResults itself throwing (independently of whenAllFailure) used to be
                // reachable via a throwing ILogger sink on its own AmbientCancellation log call -
                // now guarded by SafeLog (see ApplyBatchResults), the same way the batch-start log
                // above it is. That was the only demonstrated way to make this specific try throw;
                // it's kept as a backstop for a future failure mode inside ApplyBatchResults, the
                // same category as BackGroudCheckHealthPlusAsync's identical StartBatch guard
                // below, which also has no realistic trigger today.
                ExceptionDispatchInfo? whenAllFailure = null;
                try
                {
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    whenAllFailure = ExceptionDispatchInfo.Capture(ex);
                }
                totalTime.Stop();
                try
                {
                    ApplyBatchResults(registrationstorun, tasks, dtref, resultHealthCheckFrom, cancellationToken);

                    // UpdateStatusName() before the rethrow below, not after: ApplyBatchResults
                    // above already committed every completed check's result via Update()
                    // regardless of whether the batch as a whole is about to be reported as
                    // faulted (e.g. the ambient token firing mid-flight while most checks still
                    // completed normally) - a named aggregate (Status(name)) must reflect those
                    // committed results the same cycle they landed, not stay stale until whatever
                    // next cycle happens not to fault. This used to only run once whenAllFailure
                    // had already been checked (rethrowing here first), so a faulted batch's
                    // Update() calls never made it into any named aggregate at all - an asymmetry
                    // with BackGroudCheckHealthPlusAsync below, which already calls
                    // UpdateStatusName() before its own equivalent rethrow.
                    //
                    // Deliberately still inside this same try, not just moved above it and left
                    // unguarded - UpdateStatusName() invokes every registered StatusHealthReport
                    // delegate (CacheHealthCheckPlus.UpdateStatusName), and a throwing delegate
                    // used to propagate directly from here, bypassing whenAllFailure?.Throw()
                    // below entirely and silently discarding the real batch failure it was
                    // capturing - the exact class of masking this same method's own
                    // ApplyBatchResults guard above exists to prevent, just one line lower.
                    _cacheStatus.UpdateStatusName();
                }
                catch (Exception ex) when (whenAllFailure != null)
                {
                    throw new AggregateException(whenAllFailure.SourceException, ex);
                }
                whenAllFailure?.Throw();
            }
            else
            {
                _cacheStatus.UpdateStatusName();
            }

            foreach (var registration in registrations)
            {
                // One Snapshot read, every field pulled from that same local value - reading
                // .LastResult/.Duration as separate property accesses here (as this used to)
                // could each land on a different generation if a concurrent Update() lands in
                // between.
                var snapshot = _cacheStatus.FullStatus(registration.Name).Snapshot;
                var result = new HealthReportEntry(snapshot.LastResult.Status,
                    snapshot.LastResult.Description, snapshot.Duration, snapshot.LastResult.Exception, snapshot.LastResult.Data, registration.Tags);
                entries[registration.Name] = result;
            }
            var report = new HealthReport(entries, totalTime.Elapsed);

            if (statusHealthreport != null)
            {
                var sta = statusHealthreport.Invoke(report);
                report = new HealthReport(entries, sta, totalTime.Elapsed);
            }

            if (registrationstorun.Count != 0)
            {
                // Unguarded before: every check result in `report` is already computed and
                // committed by this point (Update()/UpdateStatusName() above already ran), so a
                // throwing sink here used to turn an already-successful evaluation into a 500 on
                // /health purely because the closing debug log couldn't be written.
                SafeLog(() => Log.HealthCheckProcessingEnd(_logger, report.Status, totalTime.Elapsed));
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
            var registrationstorun = BuildDueRegistrations(registrations, (item, sta) => ResolveBackgroundPolicy(item.Name, sta, backgroudoptions));

            if (registrationstorun.Count != 0)
            {
                // See the matching comment in CheckHealthPlusAsync: StartBatch used to run
                // completely unguarded here, after BuildDueRegistrations/TryBeginRun already marked
                // every one of these checks Running - a synchronous throw here would have leaked
                // Running for the whole batch forever, with nothing left to release it.
                Task<HealthReportEntry>[] tasks;
                DateTime dtref;
                try
                {
                    dtref = DateTime.UtcNow;
                    tasks = StartBatch(registrationstorun, cancellationToken);
                }
                catch
                {
                    ReleaseRunningForBatch(registrationstorun, DateTime.UtcNow);
                    throw;
                }

                // Running must be released for every check in this batch even when Task.WhenAll
                // faults - here, typically the per-cycle Timeout cancelling this call's linked
                // token while a check is still in flight - otherwise it would stay marked Running
                // forever and never run again on any later cycle. ApplyBatchResults below is what
                // actually releases it, and must run whether or not Task.WhenAll faulted -
                // previously via a `finally`, which meant a second, independent failure inside
                // ApplyBatchResults itself (e.g. a throwing ILogger sink on one item) would silently
                // replace whatever exception Task.WhenAll raised, per ordinary CLR
                // exception-in-finally semantics. Capturing it explicitly instead lets both surface
                // together if both happen, and preserves the original stack trace via
                // ExceptionDispatchInfo in the (normal) single-failure case. The exception, if any,
                // still propagates afterward so HealthCheckPlusBackGroundService's own
                // timeout/shutdown handling around this call is unaffected. See ApplyBatchResults
                // for how each task's outcome is classified and applied - shared with
                // CheckHealthPlusAsync so there is exactly one place that decides this, instead
                // of two copies that can drift apart.
                ExceptionDispatchInfo? whenAllFailure = null;
                try
                {
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    whenAllFailure = ExceptionDispatchInfo.Capture(ex);
                }
                try
                {
                    ApplyBatchResults(registrationstorun, tasks, dtref, HealthCheckTrigger.Background, cancellationToken);
                    _cacheStatus.UpdateStatusName();
                }
                catch (Exception ex) when (whenAllFailure != null)
                {
                    throw new AggregateException(whenAllFailure.SourceException, ex);
                }
                whenAllFailure?.Throw();
            }
        }

        // Outcome of a single fanned-out RunCheckAsync task, as seen by the caller once
        // Task.WhenAll has settled (successfully or not). Kept as its own type - rather than an
        // inline bool check - so the classification rule itself is a small, pure, directly
        // testable function (ClassifyBatchTask), independent of the cache/logging side effects
        // that consume it.
        internal enum BatchTaskOutcome
        {
            Success,
            AmbientCancellation,
            Failure
        }

        // The one place that decides whether a task that didn't complete successfully means "the
        // caller went away" (AmbientCancellation) or "the check itself failed" (Failure). Three
        // rounds of adversarial review each found a variant of the same bug living in an inline
        // copy of this check at a different call site (CheckHealthPlusAsync's finally,
        // BackGroudCheckHealthPlusAsync's finally, and a related guard inside RunCheckAsync) -
        // centralizing the finally-block half here, and unit-testing it directly with synthetic
        // Task states below, means the rule only needs to be gotten right once.
        //
        // task.IsCanceled alone is not proof that cancellationToken caused it: .NET's async
        // machinery routes ANY OperationCanceledException thrown by an async delegate to the
        // Canceled task state, regardless of which token, if any, it's tied to. Requiring
        // cancellationToken.IsCancellationRequested as well is only a safe proxy for "this task's
        // cancellation is attributable to cancellationToken" because RunCheckAsync itself
        // guarantees no other OperationCanceledException can reach this point Canceled - it
        // sterilizes a check factory's own OCE into a Faulted InvalidOperationException, and its
        // own catch guard rejects an unrelated OCE (compared by the exact CancellationToken it
        // carries) rather than deferring to a batch-wide flag. That invariant lives in
        // RunCheckAsync, not here - if it's ever relaxed, this classification must be revisited.
        internal static BatchTaskOutcome ClassifyBatchTask(Task task, CancellationToken cancellationToken)
        {
            if (task.IsCompletedSuccessfully)
            {
                return BatchTaskOutcome.Success;
            }

            if (task.IsCanceled && cancellationToken.IsCancellationRequested)
            {
                return BatchTaskOutcome.AmbientCancellation;
            }

            return BatchTaskOutcome.Failure;
        }

        // Applies ClassifyBatchTask's verdict for every task in a fanned-out batch: a genuine
        // ambient cancellation only releases the Running flag and advances DateRef to the actual
        // release time (not dtref, the batch's *start* time - the cancellation is only ever
        // observed after however long the batch ran for, so reusing dtref would leave DateRef
        // stale by that same amount and defeat the whole point of advancing it), leaving the last
        // real result untouched; anything else - a completed result, or a genuine failure - is
        // reported via Update() like any other result. Shared by CheckHealthPlusAsync and
        // BackGroudCheckHealthPlusAsync so this logic exists in exactly one place.
        //
        // Log.HealthCheckExecutionAborted in the AmbientCancellation case used to run completely
        // unguarded, before ReleaseRunning - the same class of bug fixed elsewhere in this class
        // for the batch-start log/StartBatch, just missed here. Worse here: since this all runs
        // in a single loop over the whole batch, a throwing ILogger on item N used to abort the
        // loop entirely, leaving every item from N onward - not just item N - stuck Running
        // forever. Each iteration now has its own try/catch: whatever throws, this item's
        // Running is still released (ReleaseRunning is safe to call even for an item Update()
        // already handled - it just re-preserves the already-fresh result), and the loop
        // continues to the next item instead of aborting the rest of the batch. The exception
        // isn't swallowed - every one caught is collected and (re)thrown together once every
        // item in the batch has been finalized, so the failure still surfaces to the caller.
        private void ApplyBatchResults(
            List<HealthCheckRegistration> registrationstorun,
            Task<HealthReportEntry>[] tasks,
            DateTime dtref,
            HealthCheckTrigger trigger,
            CancellationToken cancellationToken)
        {
            var releasedAt = DateTime.UtcNow;
            List<Exception>? failures = null;

            for (var index = 0; index < registrationstorun.Count; index++)
            {
                var registration = registrationstorun[index];
                var task = tasks[index];

                try
                {
                    switch (ClassifyBatchTask(task, cancellationToken))
                    {
                        case BatchTaskOutcome.Success:
                            {
                                var result = new HealthCheckResult(task.Result.Status, task.Result.Description, task.Result.Exception, task.Result.Data);
                                _cacheStatus.Update(registration.Name, trigger, result, dtref.Add(task.Result.Duration), task.Result.Duration);
                                break;
                            }

                        case BatchTaskOutcome.AmbientCancellation:
                            // SafeLog here (not a bare Log.* call): a throwing sink used to be
                            // caught by this iteration's own catch below and folded into the
                            // AggregateException thrown after the whole batch, reporting a
                            // perfectly legitimate ambient-cancellation outcome as a batch failure
                            // purely because logging it failed.
                            SafeLog(() => Log.HealthCheckExecutionAborted(_logger, registration.Name));
                            SafeRecordMetric(() => HealthCheckPlusMetrics.RecordAnomaly(AnomalyReason.CheckExecutionAborted));
                            _cacheStatus.ReleaseRunning(registration.Name, releasedAt);
                            break;

                        default: // Failure
                            {
                                var exception = task.Exception?.GetBaseException();
                                var result = new HealthCheckResult(registration.FailureStatus, exception?.Message ?? "The health check threw an unhandled exception before it could run.", exception, null);
                                _cacheStatus.Update(registration.Name, trigger, result, releasedAt, TimeSpan.Zero);
                                break;
                            }
                    }
                }
                catch (Exception ex)
                {
                    _cacheStatus.ReleaseRunning(registration.Name, releasedAt);
                    (failures ??= []).Add(ex);
                }
            }

            if (failures is { Count: > 0 })
            {
                throw new AggregateException(
                    $"Applying results for {failures.Count} of {registrationstorun.Count} check(s) in this batch failed.",
                    failures);
            }
        }

        public HealthReport CreateReport()
        {
            return _cacheStatus.CreateReport();
        }

        // Used by HealthCheckPlusBackGroundService to build the report it publishes - see
        // CacheHealthCheckPlus.CreateReport(Func<string, bool>).
        internal HealthReport CreateReport(Func<string, bool> includeName)
        {
            return _cacheStatus.CreateReport(includeName);
        }

        public DateTime? LastReport()
        {
            return _cacheStatus.LastReport();
        }

        private async Task<HealthReportEntry> RunCheckAsync(HealthCheckRegistration registration, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var scope = _scopeFactory.CreateScope();
            IHealthCheck healthCheck;
            try
            {
                healthCheck = registration.Factory(scope.ServiceProvider);
            }
            catch (OperationCanceledException ex)
            {
                // registration.Factory is a Func<IServiceProvider, IHealthCheck> - it has no
                // CancellationToken parameter at all, so it can never legitimately observe the
                // ambient token. Any OperationCanceledException it throws is a genuine construction
                // failure, not "the caller went away", and must never let this method's task end up
                // in the Canceled state: the caller's finally block treats a Canceled task as
                // ambient cancellation whenever cancellationToken.IsCancellationRequested happens to
                // also be true by the time it checks - a batch-wide flag, not proof that THIS
                // exception was caused by that token (e.g. a different, genuinely slow check in the
                // same batch triggering the real cancellation). Wrapping it as Faulted routes it
                // correctly regardless of what else is happening in the batch.
                // SafeLog before the rethrow: a throwing sink here would otherwise replace the real
                // construction failure with a logging failure as the exception this check reports.
                SafeLog(() => Log.HealthCheckError(_logger, registration, ex, TimeSpan.Zero));
                throw new InvalidOperationException($"Health check '{registration.Name}' could not be constructed.", ex);
            }
            catch (Exception ex)
            {
                SafeLog(() => Log.HealthCheckError(_logger, registration, ex, TimeSpan.Zero));
                throw;
            }

            // If the health check does things like make Database queries using EF or backend HTTP calls,
            // it may be valuable to know that logs it generates are part of a health check. So we start a scope.
            //
            // Deliberately not behind SafeLog, unlike every Log.* call in this class: BeginScope
            // returns an IDisposable consumed by this using statement, not an Action SafeLog can
            // wrap and swallow - defending it would mean restructuring this into a try/catch around
            // the whole scope instead of a one-line wrap. A throwing ILoggerProvider's own
            // BeginScope here would still abort this check the same way it did before SafeLog
            // existed - a known, intentionally deferred residual gap, not an oversight.
            using (_logger.BeginScope(new HealthCheckLogScopePlus(registration.Name)))
            {
                var stopwatch = Stopwatch.StartNew();
                var context = new HealthCheckContext { Registration = registration };

                // Unguarded before: a throwing sink here ran before healthCheck.CheckHealthAsync
                // below ever got a chance to execute, so the check would be reported as having
                // thrown an unhandled exception without ever actually having run.
                SafeLog(() => Log.HealthCheckBegin(_logger, registration.Name));

                HealthReportEntry entry;
                CancellationTokenSource? timeoutCancellationTokenSource = null;
                var checkCancellationToken = cancellationToken;
                try
                {
                    HealthCheckResult result;

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

                    // Unguarded before: a throwing sink here was caught by the catch below (the one
                    // that treats a genuine check failure as Unhealthy), turning a check that just
                    // succeeded into a reported failure whose description was the logging sink's own
                    // exception message, not anything about the real result.
                    SafeLog(() => Log.HealthCheckEnd(_logger, registration, entry, duration));
                    SafeLog(() => Log.HealthCheckData(_logger, registration, entry));

                }
                catch (OperationCanceledException ex) when (ex.CancellationToken != checkCancellationToken || !cancellationToken.IsCancellationRequested)
                {
                    // cancellationToken.IsCancellationRequested alone isn't proof that THIS
                    // exception was caused by that token - it's a batch-wide flag, so a different,
                    // genuinely slow check in the same batch tripping the ambient token would make
                    // this guard wrongly defer to the propagate-uncaught path below for a completely
                    // unrelated check's own OperationCanceledException (e.g. an HttpClient's internal
                    // timeout that doesn't honor checkCancellationToken at all). Comparing the
                    // exception's own CancellationToken against checkCancellationToken (the exact
                    // token this call handed to CheckHealthAsync) is the only way to know whether
                    // this specific exception is actually attributable to it.
                    var duration = stopwatch.Elapsed;
                    entry = new HealthReportEntry(
                        status: registration.FailureStatus,
                        description: "A timeout occurred while running check.",
                        duration: duration,
                        exception: ex,
                        data: null,
                        tags: registration.Tags);

                    SafeLog(() => Log.HealthCheckError(_logger, registration, ex, duration));
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

                    SafeLog(() => Log.HealthCheckError(_logger, registration, ex, duration));
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
            [LoggerMessage(HealthCheckPlusEventIds.HealthCheckProcessingBeginId, LogLevel.Debug, "Running health checks", EventName = HealthCheckPlusEventIds.HealthCheckProcessingBeginName)]
            public static partial void HealthCheckProcessingBegin(ILogger logger);

            public static void HealthCheckProcessingEnd(ILogger logger, HealthStatus status, TimeSpan duration) =>
                HealthCheckProcessingEnd(logger, status, duration.TotalMilliseconds);

            [LoggerMessage(HealthCheckPlusEventIds.HealthCheckProcessingEndId, LogLevel.Debug, "Health check processing with combined status {HealthStatus} completed after {ElapsedMilliseconds}ms", EventName = HealthCheckPlusEventIds.HealthCheckProcessingEndName)]
            private static partial void HealthCheckProcessingEnd(ILogger logger, HealthStatus HealthStatus, double ElapsedMilliseconds);

            [LoggerMessage(HealthCheckPlusEventIds.HealthCheckBeginId, LogLevel.Debug, "Running health check {HealthCheckName}", EventName = HealthCheckPlusEventIds.HealthCheckBeginName)]
            public static partial void HealthCheckBegin(ILogger logger, string HealthCheckName);

            // These are separate so they can have different log levels
            private const string HealthCheckEndText = "Health check {HealthCheckName} with status {HealthStatus} completed after {ElapsedMilliseconds}ms with message '{HealthCheckDescription}'";

            [LoggerMessage(HealthCheckPlusEventIds.HealthCheckEndId, LogLevel.Debug, HealthCheckEndText, EventName = HealthCheckPlusEventIds.HealthCheckEndName)]
            private static partial void HealthCheckEndHealthy(ILogger logger, string HealthCheckName, HealthStatus HealthStatus, double ElapsedMilliseconds, string? HealthCheckDescription);

#pragma warning disable SYSLIB1006 // Multiple logging methods cannot use the same event id within a class
#pragma warning disable SYSLIB1025 // Multiple logging methods should not use the same event name within a class
            [LoggerMessage(HealthCheckPlusEventIds.HealthCheckEndId, LogLevel.Warning, HealthCheckEndText, EventName = HealthCheckPlusEventIds.HealthCheckEndName)]
#pragma warning restore SYSLIB1025 // Multiple logging methods should not use the same event name within a class
#pragma warning restore SYSLIB1006 // Multiple logging methods cannot use the same event id within a class
            private static partial void HealthCheckEndDegraded(ILogger logger, string HealthCheckName, HealthStatus HealthStatus, double ElapsedMilliseconds, string? HealthCheckDescription, Exception? exception);

#pragma warning disable SYSLIB1006 // Multiple logging methods cannot use the same event id within a class
#pragma warning disable SYSLIB1025 // Multiple logging methods should not use the same event name within a class
            [LoggerMessage(HealthCheckPlusEventIds.HealthCheckEndId, LogLevel.Error, HealthCheckEndText, EventName = HealthCheckPlusEventIds.HealthCheckEndName)]
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

            [LoggerMessage(HealthCheckPlusEventIds.HealthCheckErrorId, LogLevel.Error, "Health check {HealthCheckName} threw an unhandled exception after {ElapsedMilliseconds}ms", EventName = HealthCheckPlusEventIds.HealthCheckErrorName)]
            private static partial void HealthCheckError(ILogger logger, string HealthCheckName, double ElapsedMilliseconds, Exception exception);

            public static void HealthCheckError(ILogger logger, HealthCheckRegistration registration, Exception exception, TimeSpan duration) =>
                HealthCheckError(logger, registration.Name, duration.TotalMilliseconds, exception);

            [LoggerMessage(HealthCheckPlusEventIds.HealthCheckDisposeErrorId, LogLevel.Warning,
                "Disposing the adopted external health check '{HealthCheckName}' threw an exception; continuing to dispose the remaining adopted checks.",
                EventName = HealthCheckPlusEventIds.HealthCheckDisposeErrorName)]
            public static partial void HealthCheckDisposeError(ILogger logger, string HealthCheckName, Exception exception);

            [LoggerMessage(HealthCheckPlusEventIds.ServiceMetricsRecordingErrorId, LogLevel.Warning,
                "Recording an anomaly metric also failed; the anomaly itself was already logged separately above.",
                EventName = HealthCheckPlusEventIds.ServiceMetricsRecordingErrorName)]
            public static partial void HealthCheckMetricsRecordingError(ILogger logger, Exception exception);

            [LoggerMessage(HealthCheckPlusEventIds.HealthCheckExecutionAbortedId, LogLevel.Warning,
                "Health check '{HealthCheckName}' did not complete because the operation was cancelled (e.g. the HTTP client disconnected, or the background cycle timed out); its last known result is unchanged and it remains eligible to run again.",
                EventName = HealthCheckPlusEventIds.HealthCheckExecutionAbortedName)]
            public static partial void HealthCheckExecutionAborted(ILogger logger, string HealthCheckName);

            public static void HealthCheckData(ILogger logger, HealthCheckRegistration registration, HealthReportEntry entry)
            {
                if (entry.Data.Count > 0 && logger.IsEnabled(LogLevel.Debug))
                {
                    logger.Log(
                        LogLevel.Debug,
                        HealthCheckPlusEventIds.HealthCheckData,
                        new HealthCheckDataLogValue(registration.Name, entry.Data),
                        null,
                        (state, ex) => state.ToString());
                }
            }
        }
#pragma warning restore IDE0079

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
