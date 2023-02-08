using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Concurrent;

namespace HealthCheckPlus.Internal
{
    internal class StateHealthChecksPlus<T> : IStateHealthChecksPlus, IStateHealthCheckPlusInternal where T : Enum
    {
        private readonly object root = new();
        private readonly ConcurrentDictionary<T, HealthCheckResult> _statusDeps;
        private readonly ConcurrentDictionary<T, (DateTime? Lastexecute, bool Running)> _lastExecute;
        private readonly ConcurrentDictionary<T, (TimeSpan delay, TimeSpan interval)> _options;
        private HealthCheckResult _StatusApp;

        public StateHealthChecksPlus()
        {
            _StatusApp = HealthCheckResult.Healthy();
            _statusDeps = new ConcurrentDictionary<T, HealthCheckResult>();
            _lastExecute = new ConcurrentDictionary<T, (DateTime? Lastexecute, bool Running)>();
            _options = new ConcurrentDictionary<T, (TimeSpan delay, TimeSpan interval)>();
            var tp = typeof(T);
            var enumValues = Enum.GetValues(tp);
            for (int i = 0; i < enumValues.Length; i++)
            {
                _options.TryAdd(
                    (T)Enum.Parse(tp, enumValues!.GetValue(i)!.ToString()!),
                    (TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)));
                _lastExecute.TryAdd(
                    (T)Enum.Parse(tp, enumValues!.GetValue(i)!.ToString()!),
                    (null, false));
                _statusDeps.TryAdd(
                    (T)Enum.Parse(tp, enumValues!.GetValue(i)!.ToString()!),
                    HealthCheckResult.Healthy());
            }
        }

        public TimeSpan Delay(string keydep)
        {
            var tp = typeof(T);
            if (!Enum.TryParse(tp, keydep, out var key))
            {
                throw new ArgumentException($"value is invalid for type {tp.Name}", nameof(keydep));
            }
            return _options[(T)key!].delay;
        }

        public TimeSpan Interval(string keydep)
        {
            var tp = typeof(T);
            if (!Enum.TryParse(tp, keydep, out var key))
            {
                throw new ArgumentException($"value is invalid for type {tp.Name}", nameof(keydep));
            }
            return _options[(T)key!].interval;
        }

        public void SetDelayInternal(string keydep, TimeSpan delay, TimeSpan interval)
        {
            var tp = typeof(T);
            if (!Enum.TryParse(tp, keydep, out var key))
            {
                throw new ArgumentException($"value is invalid for type {tp.Name}", nameof(keydep));
            }
            (TimeSpan delay, TimeSpan interval) item = _options[(T)key!];
            item.delay = delay;
            item.interval = interval;
            _options[(T)key!] = item;
        }

        public DateTime? LastCheck(string keydep)
        {
            var tp = typeof(T);
            if (!Enum.TryParse(tp, keydep, out var key))
            {
                throw new ArgumentException($"value is invalid for type {tp.Name}", nameof(keydep));
            }
            return _lastExecute[(T)key!].Lastexecute;
        }

        public bool IsRunning(string keydep)
        {
            var tp = typeof(T);
            if (!Enum.TryParse(tp, keydep, out var key))
            {
                throw new ArgumentException($"value is invalid for type {tp.Name}", nameof(keydep));
            }
            return _lastExecute[(T)key!].Running;
        }
        public void UpdateLastCheck(string keydep, DateTime value)
        {
            var tp = typeof(T);
            if (!Enum.TryParse(tp, keydep, out var key))
            {
                throw new ArgumentException($"value is invalid for type {tp.Name}", nameof(keydep));
            }
            (DateTime? Lastexecute, bool Running) aux = _lastExecute[(T)key!];
            aux.Lastexecute = value;
            _lastExecute[(T)key!] = aux;
        }

        public void SetIsRunning(string keydep, bool value)
        {
            var tp = typeof(T);
            if (!Enum.TryParse(tp, keydep, out var key))
            {
                throw new ArgumentException($"value is invalid for type {tp.Name}", nameof(keydep));
            }
            (DateTime? Lastexecute, bool Running) aux = _lastExecute[(T)key!];
            aux.Running = value;
            _lastExecute[(T)key!] = aux;
        }

        public HealthCheckResult StatusApp
        {
            get
            {
                return _StatusApp;
            }
            set
            {
                lock (root)
                {
                    _StatusApp = value;
                }
            }
        }

        public HealthCheckResult StatusDep(Enum keydep)
        {
            var tp = typeof(T);
            if (!tp.Equals(keydep.GetType()))
            {
                throw new ArgumentException($"value is invalid for type {tp.Name}", nameof(keydep));
            }
            return _statusDeps[(T)keydep];
        }

        public HealthCheckResult StatusDep(string keydep)
        {
            var tp = typeof(T);
            if (!Enum.TryParse(tp, keydep, out var key))
            {
                throw new ArgumentException($"value is invalid for type {tp.Name}", nameof(keydep));
            }
            return _statusDeps[(T)key!];
        }

        public void UpdateStatusDep(string keydep, HealthCheckResult value)
        {
            var tp = typeof(T);
            if (!Enum.TryParse(tp, keydep, out var key))
            {
                throw new ArgumentException($"value is invalid for type {tp.Name}", nameof(keydep));
            }
            _statusDeps[(T)key!] = value;
        }
    }
}
