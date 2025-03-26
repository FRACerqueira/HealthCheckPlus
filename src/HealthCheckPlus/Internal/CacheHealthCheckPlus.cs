// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Abstractions;
using System.Collections.Concurrent;
using HealthCheckPlus.options;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlus.Internal
{
    internal class CacheHealthCheckPlus : IStateHealthChecksPlus
    {
        private readonly ConcurrentDictionary<string, ItemCacheHealth> _statusDeps;
        private readonly ConcurrentDictionary<string, HealthStatus> _statusName;
        private readonly Dictionary<string, Func<HealthReport, HealthStatus>?> _statusFunction;
        private readonly DateTime _dateregister;
#pragma warning disable IDE0330
        private readonly object _lock = new();
#pragma warning restore IDE0330

        public CacheHealthCheckPlus()
        {
            _statusDeps = new ConcurrentDictionary<string, ItemCacheHealth>();
            _statusName = new ConcurrentDictionary<string, HealthStatus>();
            _statusFunction = [];
            _dateregister = DateTime.Now;
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
            if (_statusDeps.TryGetValue(key, out var item) && item.Running)
            {
                item.LastResult = result;
                item.DateRef = lastexecute;
                item.Duration = duration;
                item.Origin = healthCheckFrom;
                item.Running = false;
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
            Update(key, HealthCheckTrigger.SwitchTo, itemres, DateTime.Now, TimeSpan.Zero);
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
