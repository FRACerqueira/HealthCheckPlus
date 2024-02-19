// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using System.Collections.Concurrent;
using HealthCheckPlus.options;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlus.Internal
{
    internal class CacheHealthCheckPlus : IStateHealthChecksPlus
    {
        private readonly ConcurrentDictionary<string, ItemCacheHealth> _statusDeps;
        private readonly ConcurrentDictionary<string, HealthStatus> _statusName = [];
        private readonly Dictionary<string,Func<HealthReport, HealthStatus>?> _statusFunction = [];
        private readonly DateTime _dateregister;
        private readonly object _lock = new();
        public CacheHealthCheckPlus()
        {
            _statusDeps = new ConcurrentDictionary<string, ItemCacheHealth>();
            _dateregister = DateTime.Now;
            _statusFunction.Add(string.Empty, (_) => _statusDeps.Values.Min(x => x.Lastresult.Status));
        }

        public DateTime DateRegister => _dateregister;

        public void AddStatusName(HealthCheckPlusOptions options)
        {
            if (string.IsNullOrEmpty(options.HealthCheckName))
            {
                return;
            }
            if (_statusFunction.ContainsKey(options.HealthCheckName) )
            {
                throw new ArgumentException("HealthCheckName already exists");
            }
            _statusFunction.Add(options.HealthCheckName,options.StatusHealthReport);
        }

        public void InitCache<T>()
        {
            var tp = typeof(T);
            var enumValues = Enum.GetValues(tp);
            for (int i = 0; i < enumValues.Length; i++)
            {
                _statusDeps.TryAdd(enumValues!.GetValue(i)!.ToString()!,
                    new()
                    {
                        Name = enumValues!.GetValue(i)!.ToString()!,
                        Duration = TimeSpan.Zero,
                        Dateref = _dateregister,
                        Running = false,
                        Origin = HealthCheckTrigger.None,
                        Lastresult = new HealthCheckResult(HealthStatus.Healthy)
                    });
            }
        }

        public void UpdateStatusName()
        {
            var report = CreateReport();
            foreach (var item in _statusFunction)
            {
                _statusName[item.Key] = item.Value!.Invoke(report)!;
            }
        }

        public DateTime? LastReport()
        {
            return _statusDeps.Values.Max(x => x.Dateref);
        }

        public HealthReport CreateReport()
        {
            var entries = new Dictionary<string, HealthReportEntry>();
            var index = 0;
            foreach (var sta in _statusDeps.Values)
            {
                entries[sta.Name] = new HealthReportEntry(sta.Lastresult.Status, null, TimeSpan.Zero, null, null);
                index++;
            }
            return new HealthReport(entries, TimeSpan.Zero);
        }

        public HealthStatus Status(string? name = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                return _statusDeps.Values.Min(x => x.Lastresult.Status);
            }
            if (!_statusFunction.TryGetValue(name, out Func<HealthReport, HealthStatus>? value))
            {
                throw new ArgumentException("HealthCheckName not exists");
            }
            if (_statusName.TryGetValue(name, out var status))
            { 
                return status;
            }

            _statusName[name] = value!.Invoke(CreateReport())!;
            return _statusName[name];
        }

        public void Running(string key, bool value)
        {
            _statusDeps[key].Running = value;
        }

        public void Update(string key, HealthCheckTrigger healthCheckFrom, HealthCheckResult result, DateTime lastexecute, TimeSpan duration)
        {
            if (!_statusDeps[key].Running)
            {
                return;
            }
            var item = _statusDeps[key];
            item.Lastresult = result;
            item.Dateref = lastexecute;
            item.Duration = duration;
            item.Origin = healthCheckFrom;
            item.Running = false;
            _statusDeps[key] = item;
        }

        public void SwithState(string key, HealthStatus status)
        {
            lock (_lock)
            {
                if (_statusDeps[key].Running || _statusDeps[key].Lastresult.Status == status)
                {
                    return;
                }
                Running(key, true);
            }
            var itemres = new HealthCheckResult(status, _statusDeps[key].Lastresult.Description);
            Update(key, HealthCheckTrigger.SwithTo, itemres, DateTime.Now, TimeSpan.Zero);
        }

        #region IStateHealthChecksPlus

        public ItemCacheHealth FullStatus(string keydep)
        {
            return _statusDeps[keydep];
        }

        public HealthCheckResult StatusDep(string keydep)
        {
            return _statusDeps[keydep].Lastresult;
        }

        public void SwithToUnhealthy(string keydep)
        {
            SwithState(keydep, HealthStatus.Unhealthy);
        }

        public void SwithToDegraded(string keydep)
        {
            SwithState(keydep, HealthStatus.Degraded);
        }

        public bool TryGetNotHealthy(out IEnumerable<HealthCheckResult> result)
        {
            result = _statusDeps
                .Where(x => x.Value.Lastresult.Status != HealthStatus.Healthy)
                .Select(x => x.Value.Lastresult);
            return result.Any();
        }

        public bool TryGetHealthy(out IEnumerable<HealthCheckResult> result)
        {
            result = _statusDeps
                .Where(x => x.Value.Lastresult.Status == HealthStatus.Healthy)
                .Select(x => x.Value.Lastresult);
            return result.Any();
        }

        public bool TryGetDegraded(out IEnumerable<HealthCheckResult> result)
        {
            result = _statusDeps
                .Where(x => x.Value.Lastresult.Status == HealthStatus.Degraded)
                .Select(x => x.Value.Lastresult);
            return result.Any();
        }

        public bool TryGetUnhealthy(out IEnumerable<HealthCheckResult> result)
        {
            result = _statusDeps
                .Where(x => x.Value.Lastresult.Status == HealthStatus.Unhealthy)
                .Select(x => x.Value.Lastresult);
            return result.Any();
        }

        public IEnumerable<IDataHealthPlus> ConvertToPlus(HealthReport report)
        {
            return report.Entries.Select(x => _statusDeps[x.Key]);
        }

        #endregion
    }
}
