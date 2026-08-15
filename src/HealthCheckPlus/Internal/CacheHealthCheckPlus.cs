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
            _statusDeps = new ConcurrentDictionary<string, ItemCacheHealth>();
            _statusName = new ConcurrentDictionary<string, HealthStatus>();
            _statusFunction = [];
            _dateregister = DateTime.UtcNow;
            _statusFunction.Add(string.Empty, (_) => _statusDeps.Values.Min(x => x.LastResult.Status));
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
            _statusFunction.Add(options.HealthCheckName, options.StatusHealthReport ?? (_ => _statusDeps.Values.Min(x => x.LastResult.Status)));
        }

        public void InitCache(IEnumerable<string> names)
        {
            foreach (var item in names)
            {
                _statusDeps.TryAdd(item, new ItemCacheHealth
                {
                    Name = item,
                    Duration = TimeSpan.Zero,
                    DateRef = _dateregister,
                    Running = false,
                    Origin = HealthCheckTrigger.None,
                    LastResult = new HealthCheckResult(HealthStatus.Healthy)
                });
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
            var entries = _statusDeps.ToDictionary(
                kvp => kvp.Key,
                kvp => new HealthReportEntry(kvp.Value.LastResult.Status, null, TimeSpan.Zero, null, null)
            );
            return new HealthReport(entries, TimeSpan.Zero);
        }

        public HealthStatus Status(string? name = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                return _statusDeps.Values.Min(x => x.LastResult.Status);
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

        public void Running(string key, bool value)
        {
            if (_statusDeps.TryGetValue(key, out var item))
            {
                item.Running = value;
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

            item.LastResult = result;
            item.DateRef = lastexecute;
            item.Duration = duration;
            item.Origin = healthCheckFrom;
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
            lock (_lock)
            {
                if (_statusDeps.TryGetValue(key, out var item) && (item.Running || item.LastResult.Status == status))
                {
                    return;
                }
                Running(key, true);
            }
            var itemres = new HealthCheckResult(status, _statusDeps[key].LastResult.Description);
            Update(key, HealthCheckTrigger.SwitchTo, itemres, DateTime.UtcNow, TimeSpan.Zero);
        }

        public ItemCacheHealth FullStatus(string keydep)
        {
            return _statusDeps[keydep];
        }

        #region IStateHealthChecksPlus

        public HealthCheckResult StatusResult(string keydep)
        {
            return _statusDeps[keydep].LastResult;
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
            var auxresult = _statusDeps
                .Where(kv => kv.Value.LastResult.Status != HealthStatus.Healthy)
                .ToDictionary(kv => kv.Key, kv => kv.Value.LastResult);
            result = auxresult;
            return result.Count > 0;
        }

        public bool TryGetHealthy(out IReadOnlyDictionary<string, HealthCheckResult> result)
        {
            var auxresult = _statusDeps
                .Where(kv => kv.Value.LastResult.Status == HealthStatus.Healthy)
                .ToDictionary(kv => kv.Key, kv => kv.Value.LastResult);
            result = auxresult;
            return result.Count > 0;
        }

        public bool TryGetDegraded(out IReadOnlyDictionary<string, HealthCheckResult> result)
        {
            var auxresult = _statusDeps
                .Where(kv => kv.Value.LastResult.Status == HealthStatus.Degraded)
                .ToDictionary(kv => kv.Key, kv => kv.Value.LastResult);
            result = auxresult;
            return result.Count > 0;
        }

        public bool TryGetUnhealthy(out IReadOnlyDictionary<string, HealthCheckResult> result)
        {
            var auxresult = _statusDeps
                .Where(kv => kv.Value.LastResult.Status == HealthStatus.Unhealthy)
                .ToDictionary(kv => kv.Key, kv => kv.Value.LastResult);
            result = auxresult;
            return result.Count > 0;
        }

        public IEnumerable<IDataHealthPlus> ConvertToPlus(HealthReport report)
        {
            return report.Entries.Select(x => _statusDeps[x.Key]);
        }

        #endregion
    }
}
