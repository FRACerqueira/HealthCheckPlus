// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Abstractions;
using System.Collections.Concurrent;
using HealthCheckPlus.options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HealthCheckPlus.Internal
{
    internal class CacheHealthCheckPlus : IStateHealthChecksPlus
    {
        private static readonly EventId MetricsRecordingErrorEventId = new(100, "HealthCheckPlusMetricsRecordingError");
        private static readonly EventId UpdateDroppedEventId = new(101, "HealthCheckPlusUpdateDropped");

        private readonly ConcurrentDictionary<string, ItemCacheHealth> _statusDeps;
        private readonly ConcurrentDictionary<string, HealthStatus> _statusName;
        private readonly Dictionary<string, Func<HealthReport, HealthStatus>?> _statusFunction;
        private readonly DateTime _dateregister;
        private readonly ILogger<CacheHealthCheckPlus> _logger;
#pragma warning disable IDE0330
        private readonly object _lock = new();
#pragma warning restore IDE0330

        public CacheHealthCheckPlus(ILogger<CacheHealthCheckPlus>? logger = null)
        {
            _logger = logger ?? NullLogger<CacheHealthCheckPlus>.Instance;
            // OrdinalIgnoreCase to match the other places a check name is compared case-insensitively
            // - ValidateRegistrations/HealthReport.Entries (OrdinalIgnoreCase) and AddCheckLinkTo's
            // own name matching (CurrentCultureIgnoreCase). Note this does NOT extend to policy
            // lookup (FindPolicy/ValidateHealthyPolicies/ValidatePolicyUniqueness), which still
            // compares names ordinally/case-sensitively - a pre-existing inconsistency, not fixed
            // here, that can silently miss a policy for a check whose registered Name differs only in
            // casing from the name passed to AddUnhealthyPolicy/AddDegradedPolicy. A case-sensitive
            // cache here would additionally let a name in AddHealthChecksPlus's `names` list that
            // differs only in casing from its registration's Name be rejected as a phantom/missing
            // entry by the fail-fast validations, even though HealthReport.Entries and
            // AddCheckLinkTo already treat them as the same check.
            _statusDeps = new ConcurrentDictionary<string, ItemCacheHealth>(StringComparer.OrdinalIgnoreCase);
            _statusName = new ConcurrentDictionary<string, HealthStatus>();
            _statusFunction = [];
            _dateregister = DateTime.UtcNow;
        }

        public DateTime DateRegister => _dateregister;

        public void AddStatusName(HealthCheckPlusOptions options)
        {
            if (string.IsNullOrEmpty(options.HealthCheckName))
            {
                return;
            }
            if (_statusFunction.ContainsKey(options.HealthCheckName))
            {
                throw new ArgumentException("HealthCheckName already exists");
            }
            _statusFunction.Add(options.HealthCheckName, options.StatusHealthReport ?? (_ => AggregateStatus()));
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

        public void UpdateStatusName()
        {
            var report = CreateReport();
            foreach (var item in _statusFunction)
            {
                _statusName[item.Key] = item.Value!.Invoke(report);
            }
        }

        public DateTime? LastReport()
        {
            return _statusDeps.Values.Max(x => x.DateRef);
        }

        public HealthReport CreateReport()
        {
            // OrdinalIgnoreCase to match the equivalent dictionary DefaultHealthCheckServicePlus.
            // CheckHealthPlusAsync builds for its own HealthReport - a HealthReport.Entries lookup
            // should behave the same regardless of which code path produced the report.
            //
            // Each entry reads kvp.Value.Snapshot exactly once and pulls every field from that
            // same local value - reading .LastResult/.Duration as separate property accesses
            // here (as this used to) could each land on a different generation if a concurrent
            // Update() lands in between, handing back e.g. a new Status paired with an old
            // Description.
            var entries = _statusDeps.ToDictionary(
                kvp => kvp.Key,
                kvp =>
                {
                    var snapshot = kvp.Value.Snapshot;
                    return new HealthReportEntry(
                        snapshot.LastResult.Status,
                        snapshot.LastResult.Description,
                        snapshot.Duration,
                        snapshot.LastResult.Exception,
                        snapshot.LastResult.Data,
                        kvp.Value.Tags);
                },
                StringComparer.OrdinalIgnoreCase
            );
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
            if (!_statusName.TryGetValue(name, out var status))
            {
                status = value!.Invoke(CreateReport());
                _statusName[name] = status;
            }
            return status;
        }

        // The default aggregation rule (worst status wins) - used both as the no-name Status()
        // shortcut and as AddStatusName's fallback when no custom StatusHealthReport is provided.
        // Previously expressed independently in three places, one of which (a seed entry keyed by
        // string.Empty, recomputed every UpdateStatusName() cycle) was never actually read by
        // anything.
        private HealthStatus AggregateStatus()
        {
            return _statusDeps.Values.Min(x => x.LastResult.Status);
        }

        public void Running(string key, bool value)
        {
            if (_statusDeps.TryGetValue(key, out var item))
            {
                item.Running = value;
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
        public void ReleaseRunning(string key, DateTime dateRef)
        {
            if (_statusDeps.TryGetValue(key, out var item))
            {
                item.Running = false;
                // Advance DateRef alone, preserving the rest of the last real result - read the
                // other three fields from a single Snapshot rather than three separate property
                // reads, then write them all back through SetResult as one atomic swap.
                var current = item.Snapshot;
                item.SetResult(current.LastResult, dateRef, current.Duration, current.Origin);
            }
        }

        public void Update(string key, HealthCheckTrigger healthCheckFrom, HealthCheckResult result, DateTime lastexecute, TimeSpan duration)
        {
            if (!_statusDeps.TryGetValue(key, out var item))
            {
                _logger.LogWarning(UpdateDroppedEventId,
                    "The result for health check '{HealthCheckName}' was dropped: no such check is registered in the cache.", key);
                SafeRecordMetric(() => HealthCheckPlusMetrics.RecordAnomaly(AnomalyReason.UpdateResultDropped), key);
                return;
            }

            if (!item.Running)
            {
                // Reachable under overlapping executions of the same check (e.g. an HTTP request
                // and a background cycle both deciding the check is "due" at the same time — a
                // known, separately-tracked scheduling race in ScheduleIfDue, not fixed here): the
                // first execution to finish clears Running and applies its result; a second,
                // overlapping execution finishing afterwards finds Running already false and its
                // result (and metrics) would previously be dropped with no trace at all.
                _logger.LogWarning(UpdateDroppedEventId,
                    "The result for health check '{HealthCheckName}' was dropped: no execution was marked as running for it (likely an overlapping execution already applied its result).", key);
                SafeRecordMetric(() => HealthCheckPlusMetrics.RecordAnomaly(AnomalyReason.UpdateResultDropped), key);
                return;
            }

            var previousStatus = item.LastResult.Status;

            item.SetResult(result, lastexecute, duration, healthCheckFrom);
            item.Running = false;

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
                _logger.LogWarning(MetricsRecordingErrorEventId, ex,
                    "Recording metrics for health check '{HealthCheckName}' failed; the check result itself was not affected.", key);
            }
        }

        public void SwithState(string key, HealthStatus status)
        {
            var item = GetItemOrThrow(key);
            lock (_lock)
            {
                if (item.Running || item.LastResult.Status == status)
                {
                    return;
                }
                item.Running = true;
            }
            var itemres = new HealthCheckResult(status, item.LastResult.Description);
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
        public IEnumerable<IDataHealthPlus> ConvertToPlus(HealthReport report)
        {
            return report.Entries.Select(x =>
            {
                var item = GetItemOrThrow(x.Key);
                return (IDataHealthPlus)new DataHealthPlusSnapshot(item.Name, item.Snapshot);
            });
        }

        #endregion
    }
}
