// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Abstractions;
using System.Collections.Concurrent;
using HealthCheckPlus.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HealthCheckPlus.Internal
{
    internal class CacheHealthCheckPlus : IStateHealthChecksPlus
    {
        private static readonly EventId MetricsRecordingErrorEventId = HealthCheckPlusEventIds.CacheMetricsRecordingError;
        private static readonly EventId UpdateDroppedEventId = HealthCheckPlusEventIds.UpdateDropped;
        private static readonly EventId SwitchToDroppedEventId = HealthCheckPlusEventIds.SwitchToDropped;

        private readonly ConcurrentDictionary<string, ItemCacheHealth> _statusDeps;
        // Each entry carries the _stateVersion the stored status was computed as-of, so a stale
        // write (an UpdateStatusName()/Status(name) call whose StatusHealthReport delegate took
        // long enough that a NEWER call already wrote a fresher value) can be detected and
        // dropped instead of blindly overwriting - see TryStoreStatusName.
        private readonly ConcurrentDictionary<string, (HealthStatus Status, long Version)> _statusName;
        // ConcurrentDictionary, not a plain Dictionary - AddStatusName (the only writer) is only
        // ever called during app startup configuration today, before UpdateStatusName()/Status(name)
        // (the readers) can run, but nothing enforces that ordering; a plain Dictionary is only
        // safe for concurrent reads with zero concurrent writes, and a plain Dictionary sitting
        // right next to two ConcurrentDictionary fields for the same class's other shared state
        // reads as an oversight, not a deliberate choice.
        private readonly ConcurrentDictionary<string, StatusNameRegistration> _statusFunction;
        private readonly DateTime _dateregister;
        private readonly ILogger<CacheHealthCheckPlus> _logger;
#pragma warning disable IDE0330
        private readonly object _lock = new();
#pragma warning restore IDE0330
        // Incremented once per real state change (see Update()) - not a "generation" of any single
        // check, but a global heartbeat used purely to order UpdateStatusName()/Status(name) writes
        // against each other. See TryStoreStatusName.
        private long _stateVersion;

        public CacheHealthCheckPlus(ILogger<CacheHealthCheckPlus>? logger = null)
        {
            _logger = logger ?? NullLogger<CacheHealthCheckPlus>.Instance;
            // OrdinalIgnoreCase throughout, to match the other places a check (or named-aggregate)
            // name is compared case-insensitively - ValidateRegistrations/HealthReport.Entries and
            // AddCheckLinkTo's own name matching. This now also extends to policy lookup
            // (DefaultHealthCheckServicePlus.FindPolicy/ValidateHealthyPolicies/
            // ValidatePolicyUniqueness/ValidatePolicyTargets), which previously compared names
            // case-sensitively - a pre-existing inconsistency that could silently miss a policy for
            // a check whose registered Name differed only in casing from the name passed to
            // AddUnhealthyPolicy/AddDegradedPolicy.
            _statusDeps = new ConcurrentDictionary<string, ItemCacheHealth>(StringComparer.OrdinalIgnoreCase);
            _statusName = new ConcurrentDictionary<string, (HealthStatus Status, long Version)>(StringComparer.OrdinalIgnoreCase);
            _statusFunction = new ConcurrentDictionary<string, StatusNameRegistration>(StringComparer.OrdinalIgnoreCase);
            _dateregister = DateTime.UtcNow;
        }

        public DateTime DateRegister => _dateregister;

        // The delegate a name was registered with, plus the name-inclusion test derived from
        // whatever Predicate accompanied it (null = no filtering, every registered check is
        // eligible) - see AddStatusName's own comment for why both need to travel together.
        private sealed record StatusNameRegistration(Func<HealthReport, HealthStatus> StatusHealthReport, Func<string, bool>? IncludeName);

        // includeName is the name-based translation of whatever HealthCheckOptions.Predicate
        // accompanied this registration (see HealthChecksPlusAppExtension.UseHealthChecksPlus,
        // its only caller) - CacheHealthCheckPlus itself only ever tracks check names, never the
        // real HealthCheckRegistration list a Predicate is evaluated against, so the caller that
        // does hold that list must do the translation and hand over the result.
        //
        // Without this, a Predicate-filtered endpoint (UseHealthChecksPlus(path, options) with
        // options.Predicate set) and Status(options.HealthCheckName) for that same registration
        // could disagree: HealthCheckMiddlewarePlus evaluates options.StatusHealthReport against
        // the Predicate-filtered report it builds for the HTTP response, but UpdateStatusName()/
        // Status(name) used to always feed it the full, unfiltered cache instead - confirmed
        // empirically (a real WebApplication with a Predicate excluding one Unhealthy check: the
        // endpoint reported Healthy, Status(name) for the same registered name reported
        // Unhealthy). includeName closes that gap by scoping the report the SAME way for both.
        public void AddStatusName(HealthCheckPlusOptions options, Func<string, bool>? includeName)
        {
            if (string.IsNullOrEmpty(options.HealthCheckName))
            {
                return;
            }
            // TryAdd makes the check-and-add atomic, instead of a separate ContainsKey then Add -
            // AddStatusName is only ever called sequentially during app startup configuration
            // today, so this TOCTOU was never actually reachable concurrently, but it's no more
            // code to just make it atomic.
            //
            // Scope note on the default delegate (`_ => AggregateStatus(includeName)`, used when
            // StatusHealthReport is omitted): it ignores the HealthReport that UpdateStatusName()/
            // Status(name) pass it and reads live _statusDeps instead, so it can never be staler than the
            // (version, report) pair TryStoreStatusName compares it against - a live read is always
            // at least as fresh as any snapshot. That also means the version-based staleness
            // rejection below is never exercised for this default delegate specifically: a fresher
            // live read can, in principle, still lose to an older stored value that carries a
            // higher version number, and only self-heals on the next UpdateStatusName()/Status(name)
            // call. This is a scope limitation of the versioning scheme, not a live bug - the
            // scenario it exists to fix (SwitchToUnhealthy's own override reverting to a stale
            // value) only applies to a caller-supplied StatusHealthReport that actually derives its
            // result from the report it's given. It still honors includeName, for the same reason
            // the caller-supplied delegate case above it does.
            var registration = new StatusNameRegistration(
                options.StatusHealthReport ?? (_ => AggregateStatus(includeName)),
                includeName);
            if (!_statusFunction.TryAdd(options.HealthCheckName, registration))
            {
                throw new ArgumentException("HealthCheckName already exists");
            }
        }

        public void InitCache(IEnumerable<string> names)
        {
            foreach (var item in names)
            {
                var newItem = new ItemCacheHealth { Name = item, Running = false };
                newItem.SetResult(new HealthCheckResult(HealthStatus.Healthy), _dateregister, TimeSpan.Zero, HealthCheckTrigger.None);
                _statusDeps.TryAdd(item, newItem);
            }
        }

        // See the comment on ItemCacheHealth.Tags for why this is populated separately from
        // InitCache instead of as part of it.
        public void SetTags(string name, IEnumerable<string> tags)
        {
            if (_statusDeps.TryGetValue(name, out var item))
            {
                item.Tags = tags;
            }
        }

        // Regression fix for a real, empirically-reproduced race: two concurrent calls to this
        // method (e.g. one from a routine background cycle, one triggered by SwithState right
        // after a manual override) each capture their own report and invoke their own
        // (consumer-supplied, possibly slow) StatusHealthReport delegate independently, then write
        // _statusName directly with no ordering between the two writes - whichever call's delegate
        // happened to finish LAST won, even if it started first and is working from a report
        // that's now stale. That let a fresh SwitchToUnhealthy get silently overwritten moments
        // later by a slower, in-flight call that captured its report before the override happened.
        //
        // version and report are captured together, under _lock, so they always describe the
        // exact same instant - Update() now also takes _lock around the equivalent write (see its
        // own comment). A first version of this fix read _stateVersion separately, just before
        // calling CreateReport(), with neither step synchronized against Update(): that closed the
        // originally-reported scenario (SwithState's own Update() call always happens-before its
        // own UpdateStatusName() call, so its version can never tie with a stale in-flight one) but
        // left a narrower, empirically-reproduced residual - two callers whose version reads
        // happened to tie (nothing had changed yet) could still end up with reports describing
        // different instants if an Update() landed mid-way through one of their CreateReport()
        // calls, and a tied version doesn't get rejected by TryStoreStatusName's `>` comparison.
        // Locking only the capture - not the delegate invocation below, which is arbitrary
        // consumer code and could be slow - keeps that same guarantee without ever blocking on it.
        //
        // Each registered name gets its OWN report, built from its own IncludeName (see
        // StatusNameRegistration) - two names registered with different Predicates must not see
        // each other's filtered view. All of them are still built inside the same lock as version,
        // so every name's report and the version it's stored against describe the exact same
        // instant, the same guarantee as when this built one shared report for every name.
        // Set for the duration of the delegate-invocation loop below, on whichever thread is
        // running it - not a lock, just a same-thread reentrancy detector. A StatusHealthReport
        // delegate that calls SwitchToUnhealthy/SwitchToDegraded (directly, or indirectly through
        // its own code) would otherwise recurse straight back into this method through SwithState
        // - and again, and again - until an uncatchable StackOverflowException kills the process,
        // with no log, no metric, and no way for any try/catch anywhere to intervene. [ThreadStatic]
        // rather than an instance field deliberately does not block two independent top-level
        // calls to UpdateStatusName() on different threads (e.g. an HTTP request racing a
        // background cycle) - only a call stack genuinely re-entering itself.
        [ThreadStatic]
        private static bool _isUpdatingStatusNames;

        public void UpdateStatusName()
        {
            if (_isUpdatingStatusNames)
            {
                throw new InvalidOperationException(
                    "Invalid usage. A StatusHealthReport delegate (registered via AddStatusName) called " +
                    "SwitchToUnhealthy/SwitchToDegraded, which re-entered UpdateStatusName() on the same " +
                    "call stack - continuing would recurse until a StackOverflowException crashes the " +
                    "process. A StatusHealthReport delegate must not call SwitchToUnhealthy/SwitchToDegraded.");
            }

            long version;
            var reportsByName = new List<(string Name, StatusNameRegistration Registration, HealthReport Report)>();
            lock (_lock)
            {
                version = _stateVersion;
                // Deliberately one BuildReport call per name, even for names that share the same
                // (null) IncludeName - a shared HealthReport instance was tried and reverted.
                // HealthReport.Entries is typed IReadOnlyDictionary<...> but is, at runtime, the
                // exact same mutable Dictionary<...> passed to its constructor - confirmed
                // empirically, no defensive copy. A StatusHealthReport delegate that downcasts and
                // writes to it (even accidentally, e.g. code that assumes it owns a private copy)
                // would silently corrupt every OTHER unfiltered name sharing that same instance -
                // exactly the kind of cross-aggregate leak includeName's own isolation was added to
                // prevent. Each name's own O(checks) report build is the accepted cost instead, on
                // the same "dozens of checks, not thousands" scale already accepted for
                // BuildDueRegistrations' own O(registrations) baseline (see its own comment).
                foreach (var item in _statusFunction)
                {
                    reportsByName.Add((item.Key, item.Value, BuildReport(item.Value.IncludeName, excludeNeverRun: false)));
                }
            }
            _isUpdatingStatusNames = true;
            try
            {
                foreach (var (name, registration, report) in reportsByName)
                {
                    var status = registration.StatusHealthReport.Invoke(report);
                    TryStoreStatusName(name, status, version);
                }
            }
            finally
            {
                _isUpdatingStatusNames = false;
            }
        }

        // Only overwrites the stored (status, version) pair for key if version is not older than
        // whatever is already recorded - see UpdateStatusName's comment for why. A plain
        // ConcurrentDictionary indexer write (as this used to be) has no such check. Returns
        // whichever status ends up authoritative (the caller's own, or a fresher one that was
        // already there) so a caller like Status(name) can return the winning value instead of
        // always its own, possibly-just-rejected computation.
        private HealthStatus TryStoreStatusName(string key, HealthStatus status, long version)
        {
            var updated = (status, version);
            while (true)
            {
                if (_statusName.TryGetValue(key, out var current))
                {
                    if (current.Version > version)
                    {
                        return current.Status;
                    }
                    if (_statusName.TryUpdate(key, updated, current))
                    {
                        return status;
                    }
                    // Another writer landed between the read above and this TryUpdate - retry
                    // against whatever is there now.
                }
                else if (_statusName.TryAdd(key, updated))
                {
                    return status;
                }
                // TryAdd lost a race with another writer that added the key first - retry via the
                // TryGetValue branch above, which will now see it.
            }
        }

        // _statusDeps is only ever empty for a valid (if unusual) configuration - AddHealthChecksPlus()
        // called with zero AddCheckPlus/AddCheckLinkTo registrations - so this must return a sensible
        // value rather than letting Max() throw InvalidOperationException on an empty sequence: there
        // is no "last report" yet, so null.
        public DateTime? LastReport()
        {
            return _statusDeps.IsEmpty ? null : _statusDeps.Values.Max(x => x.DateRef);
        }

        // Each entry reads item.Snapshot exactly once and pulls every field (plus, for the
        // filtered overload below, the Origin check) from that same local value - reading
        // .LastResult/.Duration as separate property accesses (as this used to) could each land
        // on a different generation if a concurrent Update() lands in between, handing back e.g.
        // a new Status paired with an old Description.
        private static HealthReportEntry BuildReportEntry(ItemCacheHealth item, out HealthCheckTrigger origin)
        {
            var snapshot = item.Snapshot;
            origin = snapshot.Origin;
            return new HealthReportEntry(
                snapshot.LastResult.Status,
                snapshot.LastResult.Description,
                snapshot.Duration,
                snapshot.LastResult.Exception,
                snapshot.LastResult.Data,
                item.Tags);
        }

        public HealthReport CreateReport()
        {
            return BuildReport(includeName: null, excludeNeverRun: false);
        }

        // Used by HealthCheckPlusBackGroundService to build the report it publishes: only names
        // satisfying includeName (the configured Predicate) AND that have actually run at least
        // once (Origin != None - InitCache seeds every check as Healthy/Origin=None before it has
        // ever actually run, a deliberate seed documented in the `origin` table in RUNBOOK.md)
        // belong in it. Both checks - "is this eligible" and "has it run" - must be decided from
        // the SAME Snapshot read used to build the entry's data, not two separate calls at two
        // different times: a plain CreateReport() followed later by a separate per-name "has it
        // run" check used to let a check that completed its first real run in between the two
        // calls be *included* (now true) while still carrying the InitCache seed data that the
        // first call had already snapshotted before that first run finished - a phantom Healthy
        // result published for a check that, at publish time, had barely just started reporting
        // real data. This exclusion is the background publisher's own policy, not shared by
        // UpdateStatusName()/Status(name) below (see BuildReport) - those treat a never-run check
        // as Healthy, same as the no-filter CreateReport() above, regardless of whether their own
        // registration carries an IncludeName filter.
        public HealthReport CreateReport(Func<string, bool> includeName)
        {
            return BuildReport(includeName, excludeNeverRun: true);
        }

        // Shared by both CreateReport overloads and by UpdateStatusName()/Status(name)'s own
        // per-registration report building (see StatusNameRegistration.IncludeName) - one place
        // decides how a name filter and the never-run exclusion each apply, instead of three
        // independent copies of this loop that could individually drift.
        private HealthReport BuildReport(Func<string, bool>? includeName, bool excludeNeverRun)
        {
            // OrdinalIgnoreCase to match the equivalent dictionary DefaultHealthCheckServicePlus.
            // CheckHealthPlusAsync builds for its own HealthReport - a HealthReport.Entries lookup
            // should behave the same regardless of which code path produced the report.
            var entries = new Dictionary<string, HealthReportEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in _statusDeps)
            {
                if (includeName != null && !includeName(kvp.Key))
                {
                    continue;
                }
                var entry = BuildReportEntry(kvp.Value, out var origin);
                if (excludeNeverRun && origin == HealthCheckTrigger.None)
                {
                    continue;
                }
                entries[kvp.Key] = entry;
            }
            return new HealthReport(entries, TimeSpan.Zero);
        }

        public HealthStatus Status(string? name = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                return AggregateStatus();
            }
            if (!_statusFunction.TryGetValue(name, out var value))
            {
                throw new ArgumentException("HealthCheckName not exists");
            }
            if (!_statusName.TryGetValue(name, out var cached))
            {
                // Same version+report captured together under _lock as UpdateStatusName, and the
                // same TryStoreStatusName - this is the only other writer of _statusName, and
                // needs the same protection against a slower concurrent caller's stale write
                // landing after. Returns whatever TryStoreStatusName says actually won, not
                // necessarily this call's own computation.
                HealthReport report;
                long version;
                lock (_lock)
                {
                    version = _stateVersion;
                    report = BuildReport(value.IncludeName, excludeNeverRun: false);
                }
                var status = value.StatusHealthReport.Invoke(report);
                return TryStoreStatusName(name, status, version);
            }
            return cached.Status;
        }

        // The default aggregation rule (worst status wins) - used both as the no-name Status()
        // shortcut (includeName always null there - no registration, no Predicate, no filtering)
        // and as AddStatusName's fallback when no custom StatusHealthReport is provided (includeName
        // is that registration's own IncludeName, so a Predicate-filtered registration's default
        // aggregate stays scoped to the same checks its endpoint's HTTP response is, instead of
        // silently reading past the filter back to every check in the cache). Previously expressed
        // independently in three places, one of which (a seed entry keyed by string.Empty,
        // recomputed every UpdateStatusName() cycle) was never actually read by anything.
        //
        // The filtered set can legitimately be empty (AddHealthChecksPlus() with zero AddCheckPlus/
        // AddCheckLinkTo registrations, or every registered check excluded by includeName) - Min()
        // throws InvalidOperationException on an empty sequence, so this must short-circuit rather
        // than let that surface as an unrelated crash from a Status()/AddStatusName call. Healthy
        // matches the native HealthReport.Status's own default for zero entries (confirmed
        // empirically), keeping this aggregate consistent with it for the same vacuous case.
        private HealthStatus AggregateStatus(Func<string, bool>? includeName = null)
        {
            var relevant = includeName == null
                ? _statusDeps.Values
                : _statusDeps.Where(kvp => includeName(kvp.Key)).Select(kvp => kvp.Value).ToList();
            return relevant.Count == 0 ? HealthStatus.Healthy : relevant.Min(x => x.LastResult.Status);
        }

        // Test-only seam: no production code calls this (every real Running transition goes
        // through TryBeginRun/ReleaseRunning/Update/SwithState, which each guard it under _lock).
        // Locked here too, purely so this method can't become the one unguarded way to corrupt
        // Running if a future caller ever reaches it outside a test.
        public void Running(string key, bool value)
        {
            lock (_lock)
            {
                if (_statusDeps.TryGetValue(key, out var item))
                {
                    item.Running = value;
                }
            }
        }

        // Atomically checks "is this check due" (per the caller-supplied predicate, evaluated
        // against the live cache item) and, if so, marks it Running - used by
        // DefaultHealthCheckServicePlus.ScheduleIfDue to close a scheduling race: two concurrent
        // callers (e.g. an HTTP request and a background cycle) could otherwise both read "not
        // running, due" before either one marked Running, both schedule the same check, and run it
        // twice concurrently - with whichever finished second having its result silently dropped by
        // Update() (see UpdateResultDropped/update_result_dropped). Guarded by the same lock
        // SwithState already uses, so the two atomic scheduling decisions in this class (manual
        // override vs. periodic due-check) can't race with each other either.
        public bool TryBeginRun(string key, Func<ItemCacheHealth, bool> isDue)
        {
            lock (_lock)
            {
                if (!_statusDeps.TryGetValue(key, out var item) || item.Running || !isDue(item))
                {
                    return false;
                }

                item.Running = true;
                return true;
            }
        }

        // Releases the Running flag TryBeginRun set, without touching the check's last known
        // result - used when a scheduled execution never actually completed because the ambient
        // cancellationToken fired (an HTTP client disconnected, or a background cycle timed out),
        // NOT because the check itself failed - so every other reader (other requests, background
        // publishers, IStateHealthChecksPlus.Status) keeps seeing the last real result instead of a
        // synthetic one manufactured for an attempt that never ran. DateRef is still advanced to
        // dateRef (normally "now"), exactly like a real Update() would: leaving it untouched would
        // make the check immediately "due" again on every subsequent poll, and a check that keeps
        // getting cancelled (e.g. one that's chronically slower than the background cycle Timeout)
        // would pile up a fresh concurrent execution attempt every cycle with no backoff at all,
        // instead of respecting its own policy period like every other outcome does.
        //
        // Guarded by the same lock as TryBeginRun. This used to clear Running first and only then
        // read-and-rewrite the snapshot, with neither step under any lock: Running is a plain,
        // non-volatile bool with no happens-before edge to TryBeginRun's own read of it, so a
        // concurrent TryBeginRun could legally observe Running already false in the gap between
        // those two statements, run a fresh execution to completion via Update(), and have that
        // fresh result clobbered if this method's own read-then-write straddled it - a genuine
        // data race under the CLR memory model regardless of how narrow the window is in
        // practice. Reading the snapshot, rewriting it, and clearing Running now all happen
        // inside one lock section, so no TryBeginRun can begin a new execution until this entire
        // release has completed - the same publish-before-clear ordering Update() already uses.
        public void ReleaseRunning(string key, DateTime dateRef)
        {
            lock (_lock)
            {
                if (_statusDeps.TryGetValue(key, out var item))
                {
                    var current = item.Snapshot;
                    item.SetResult(current.LastResult, dateRef, current.Duration, current.Origin);
                    item.Running = false;
                }
            }
        }

        public void Update(string key, HealthCheckTrigger healthCheckFrom, HealthCheckResult result, DateTime lastexecute, TimeSpan duration)
        {
            if (!_statusDeps.TryGetValue(key, out var item))
            {
                SafeLog(() => _logger.LogWarning(UpdateDroppedEventId,
                    "The result for health check '{HealthCheckName}' was dropped: no such check is registered in the cache.", key));
                SafeRecordMetric(() => HealthCheckPlusMetrics.RecordAnomaly(AnomalyReason.UpdateResultDropped), key);
                return;
            }

            // The Running check and the write below now share one lock acquisition, not two -
            // TryBeginRun/SwithState only ever claim Running under _lock, so nothing outside this
            // method can flip it between the check and the use in correct, protocol-following
            // usage; this was previously a genuinely unlocked, TOCTOU-shaped read (no live race
            // ever demonstrated - closed in practice by the happens-before edge a check's own
            // Task completing already provides), closed for good the same way as a defense in
            // depth, not because a real exploit was found.
            HealthStatus previousStatus = default;
            var wasRunning = false;
            lock (_lock)
            {
                wasRunning = item.Running;
                if (wasRunning)
                {
                    previousStatus = item.LastResult.Status;

                    // SetResult, clearing Running, and bumping _stateVersion happen atomically
                    // under _lock - not for Running's own sake (SetResult still happens before
                    // Running is cleared either way, the exact ordering ReleaseRunning was
                    // missing, so any later TryBeginRun only ever observes Running=false once this
                    // fresh result is already published), but so UpdateStatusName()/Status(name)
                    // capturing (version, report) together under the same lock (see their own
                    // comments) can never have a real state change land in the middle of that
                    // capture - closing the last piece of the race those methods were fixed for.
                    item.SetResult(result, lastexecute, duration, healthCheckFrom);
                    item.Running = false;
                    _stateVersion++;
                }
            }

            if (!wasRunning)
            {
                // TryBeginRun (see DefaultHealthCheckServicePlus.ScheduleIfDue) and SwithState both
                // claim Running under _lock, and only when it is currently false - so exactly one
                // caller ever owns a given check's execution at a time, and this branch is not
                // reachable through either of them today. It's a defensive safety net, not an
                // active race guard: if Running were ever cleared out from under a real in-flight
                // execution (e.g. through the test-only Running(key,bool) seam below, or a future
                // caller that doesn't follow the TryBeginRun/SwithState ownership protocol),
                // logging and recording an anomaly here beats silently discarding the result with
                // no trace at all.
                SafeLog(() => _logger.LogWarning(UpdateDroppedEventId,
                    "The result for health check '{HealthCheckName}' was dropped: no execution was marked as running for it (likely an overlapping execution already applied its result).", key));
                SafeRecordMetric(() => HealthCheckPlusMetrics.RecordAnomaly(AnomalyReason.UpdateResultDropped), key);
                return;
            }

            // SafeRecordMetric below stays outside the lock: a third-party MeterListener callback
            // runs synchronously on this thread, and holding _lock across an exporter's own code
            // would let a slow or blocking listener stall every other check's TryBeginRun/
            // ReleaseRunning scheduling decision, not just this one's metrics.

            // This is the single point every execution path (foreground/HTTP and background)
            // and the manual SwitchTo override converge on, so it's the right place to emit
            // metrics once instead of duplicating the call at each call site.
            SafeRecordMetric(() =>
            {
                HealthCheckPlusMetrics.RecordStatusTransition(key, previousStatus, result.Status);
                HealthCheckPlusMetrics.RecordCheckExecution(key, result.Status, healthCheckFrom, duration);
            }, key);
        }

        // A MeterListener callback (e.g. a third-party OTel exporter) runs synchronously on this
        // thread, so a bug in it would otherwise propagate out of Update() and, on the HTTP path
        // (DefaultHealthCheckServicePlus.CheckHealthPlusAsync has no try/catch around this call),
        // turn an instrumentation failure into a 500 response on /health. Metrics must never be
        // able to break health evaluation — every metrics call in this class goes through here.
        private void SafeRecordMetric(Action recordMetric, string key)
        {
            try
            {
                recordMetric();
            }
            catch (Exception ex)
            {
                SafeLog(() => _logger.LogWarning(MetricsRecordingErrorEventId, ex,
                    "Recording metrics for health check '{HealthCheckName}' failed; the check result itself was not affected.", key));
            }
        }

        // Mirror image of SafeRecordMetric above: a throwing ILogger sink (a broken third-party
        // logging provider) must never be able to break Update()/SwithState() any more than a
        // broken metrics pipeline can. The failure becomes observable via
        // AnomalyReason.LoggingSinkFailed instead of another log call - see that reason's doc for
        // why (same circularity risk SafeRecordMetric's own catch above already avoids, mirrored).
        //
        // Undefended residual: the RecordAnomaly call in the catch below is itself unguarded, so a
        // second, independent failure recording THAT (a broken MeterListener) still propagates out
        // of SafeLog and, transitively, out of Update()/SwithState(). Closing that fully would need
        // a nested empty catch here, which this codebase's own no-silent-catch doctrine treats as a
        // decision requiring explicit sign-off, not something to add unasked - left as a known gap.
        private void SafeLog(Action logCall) => HealthCheckPlusMetrics.SafeLog(logCall);

        public void SwithState(string key, HealthStatus status)
        {
            var item = GetItemOrThrow(key);
            var beganOverride = false;
            // Read once and reuse for both the equality check above and the Description used
            // below, instead of two separate item.LastResult accesses - not a demonstrated live
            // race today (once beganOverride is true, Running only ever goes false again via
            // this same call's own Update() below - TryBeginRun and this method's own
            // Running-acquire check below are the only places that claim ownership, and both
            // only do so when Running is currently false, under _lock; ReleaseRunning/Update
            // themselves don't check Running before writing, but nothing else calls them for
            // this key while it's held, since no batch will ever include an already-Running
            // check), but reading a mutable snapshot reference twice for one logical decision is
            // exactly the torn-read shape this project already treats as a defect elsewhere (see
            // ItemCacheHealth.Snapshot's own doc) - cheap to close for good rather than rely on
            // that invariant never being relaxed.
            HealthCheckResult lastResult;
            lock (_lock)
            {
                lastResult = item.LastResult;
                if (lastResult.Status == status)
                {
                    return;
                }
                if (!item.Running)
                {
                    item.Running = true;
                    beganOverride = true;
                }
            }

            if (!beganOverride)
            {
                // A scheduled execution (HTTP request or background cycle) is already in flight for
                // this check when the manual override is requested - applying it here would race
                // with that execution's own upcoming Update() call, so it's dropped instead. Left
                // silent before, this is exactly the "no silent catch" pattern this project's own
                // doctrine forbids elsewhere (see docs/architecture/logging.md): a consumer
                // calling SwitchToUnhealthy/SwitchToDegraded would see no
                // exception and reasonably assume the override took effect.
                SafeLog(() => _logger.LogWarning(SwitchToDroppedEventId,
                    "A manual override to '{TargetStatus}' for health check '{HealthCheckName}' was dropped: a scheduled execution is currently running for it and will produce its own result shortly. Retry after it completes if the override is still needed.",
                    status, key));
                SafeRecordMetric(() => HealthCheckPlusMetrics.RecordAnomaly(AnomalyReason.SwitchToDroppedWhileRunning), key);
                return;
            }

            var itemres = new HealthCheckResult(status, lastResult.Description);
            Update(key, HealthCheckTrigger.SwitchTo, itemres, DateTime.UtcNow, TimeSpan.Zero);

            // Update() alone only refreshes Status(null)'s live aggregate. A named aggregate
            // registered via AddStatusName (Status(name)) is otherwise only refreshed by
            // UpdateStatusName(), which the foreground/background execution paths call on every
            // request/cycle - but a manual override happens entirely outside that flow, so without
            // this call a consumer gating traffic on Status("someName") after catching an exception
            // and calling SwitchToUnhealthy/SwitchToDegraded could keep seeing the pre-override
            // status until the next request or background cycle happens to run, however long that
            // takes.
            UpdateStatusName();
        }

        public ItemCacheHealth FullStatus(string keydep)
        {
            return GetItemOrThrow(keydep);
        }

        // FullStatus/StatusResult/SwithState/ConvertToPlus used to hit ConcurrentDictionary's raw
        // indexer for an unknown check name, throwing an unhelpful KeyNotFoundException instead of
        // a clear error naming what went wrong.
        private ItemCacheHealth GetItemOrThrow(string name)
        {
            if (!_statusDeps.TryGetValue(name, out var item))
            {
                throw new ArgumentException($"No health check named '{name}' is registered.", nameof(name));
            }
            return item;
        }

        #region IStateHealthChecksPlus

        public HealthCheckResult StatusResult(string keydep)
        {
            return GetItemOrThrow(keydep).LastResult;
        }

        public void SwitchToUnhealthy(string keydep)
        {
            SwithState(keydep, HealthStatus.Unhealthy);
        }


        public void SwitchToDegraded(string keydep)
        {
            SwithState(keydep, HealthStatus.Degraded);
        }

        public bool TryGetNotHealthy(out IReadOnlyDictionary<string, HealthCheckResult> result)
        {
            return TryGetByStatus(out result, status => status != HealthStatus.Healthy);
        }

        public bool TryGetHealthy(out IReadOnlyDictionary<string, HealthCheckResult> result)
        {
            return TryGetByStatus(out result, status => status == HealthStatus.Healthy);
        }

        public bool TryGetDegraded(out IReadOnlyDictionary<string, HealthCheckResult> result)
        {
            return TryGetByStatus(out result, status => status == HealthStatus.Degraded);
        }

        public bool TryGetUnhealthy(out IReadOnlyDictionary<string, HealthCheckResult> result)
        {
            return TryGetByStatus(out result, status => status == HealthStatus.Unhealthy);
        }

        // Mirrors HealthReportExtensions.TryGetByStatus - same shape, different data source
        // (_statusDeps here vs. a HealthReport's Entries there), previously reimplemented inline
        // four times in this class alone.
        //
        // LastResult is read exactly once per item (into the anonymous type below) and reused
        // for both the predicate check and the stored value - reading it twice (once to filter,
        // once to select, as this used to) could filter on one generation and return another if
        // a concurrent Update() lands in between, e.g. a check going Unhealthy->Healthy right
        // between the two reads would slip an already-Healthy entry into TryGetUnhealthy()'s
        // result.
        private bool TryGetByStatus(out IReadOnlyDictionary<string, HealthCheckResult> result, Func<HealthStatus, bool> predicate)
        {
            var auxresult = _statusDeps
                .Select(kv => (kv.Key, LastResult: kv.Value.LastResult))
                .Where(x => predicate(x.LastResult.Status))
                .ToDictionary(x => x.Key, x => x.LastResult);
            result = auxresult;
            return result.Count > 0;
        }

        // Each entry is a fresh, immutable snapshot (Name + a single Snapshot read), not the
        // live ItemCacheHealth itself - two problems this fixes together: a consumer reading
        // several of the returned properties one at a time (as every "Plus" response writer in
        // HealthCheckPlusOptions does) could otherwise see a mix of fields from before and after
        // a concurrent Update() landing mid-read (the read window for a slow JSON serialization
        // pass over many entries is far wider than a single CreateReport() call); and
        // IDataHealthPlus.Name being settable used to let a consumer mutate the shared cache
        // entry in place just by assigning to it - it now only mutates this caller-owned copy.
        // Materialized eagerly (ToArray), not returned as a lazy Select - every "Plus" response
        // writer in HealthCheckPlusOptions enumerates this while a JSON response is already being
        // serialized to the output stream. A lazy sequence would let GetItemOrThrow's
        // ArgumentException (an entry naming a check no longer tracked in the cache) surface
        // mid-write, after some bytes of a JSON document already went out - an unrecoverable,
        // truncated response. Materializing here means any such failure happens before this method
        // even returns, while it's still a normal, whole exception the caller can act on.
        public IEnumerable<IDataHealthPlus> ConvertToPlus(HealthReport report)
        {
            return report.Entries.Select(x =>
            {
                var item = GetItemOrThrow(x.Key);
                return (IDataHealthPlus)new DataHealthPlusSnapshot(item.Name, item.Snapshot);
            }).ToArray();
        }

        #endregion
    }
}
