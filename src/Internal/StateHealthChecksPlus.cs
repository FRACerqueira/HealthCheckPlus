// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;

namespace HealthCheckPlus.Internal
{
    internal class StateHealthChecksPlus<T> : IStateHealthChecksPlus, IStateHealthCheckPlusInternal where T : Enum
    {
        private readonly object root = new();
        private readonly ConcurrentDictionary<T, HealthCheckResult> _statusDeps;
        private readonly ConcurrentDictionary<T, (DateTime? Lastexecute, TimeSpan? Duration, bool Running)> _lastExecute;
        private readonly ConcurrentDictionary<T, (TimeSpan delay, TimeSpan interval)> _options;
        private HealthCheckResult _StatusApp;
        private readonly IOptions<HealthCheckPlusOptions> _defaultoptions;
        private readonly DateTime _dateregister;

        public StateHealthChecksPlus(IOptions<HealthCheckPlusOptions> options)
        {
            _defaultoptions = options;
            _StatusApp = HealthCheckResult.Healthy();
            _statusDeps = new ConcurrentDictionary<T, HealthCheckResult>();
            _lastExecute = new ConcurrentDictionary<T, (DateTime? Lastexecute,TimeSpan? Duration, bool Running)>();
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
                    (null,null, false));
                _statusDeps.TryAdd(
                    (T)Enum.Parse(tp, enumValues!.GetValue(i)!.ToString()!),
                    HealthCheckResult.Healthy());
            }
            _dateregister = DateTime.Now;
        }

        public DateTime DateRegister => _dateregister;

        public TimeSpan Delay(string keydep)
        {
            lock (root)
            {
                var tp = typeof(T);
                if (!Enum.TryParse(tp, keydep, out var key))
                {
                    throw new ArgumentException($"value is invalid for type {tp.Name}", nameof(keydep));
                }
                return _options[(T)key!].delay;
            }
        }

        public TimeSpan Interval(string keydep)
        {
            lock (root)
            {
                var tp = typeof(T);
                if (!Enum.TryParse(tp, keydep, out var key))
                {
                    throw new ArgumentException($"value is invalid for type {tp.Name}", nameof(keydep));
                }
                return _options[(T)key!].interval;
            }
        }

        public void SetDelayInternal(string keydep, TimeSpan delay, TimeSpan interval)
        {
            lock (root)
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
        }

        public DateTime? LastCheck(string keydep)
        {
            lock (root)
            {
                var tp = typeof(T);
                if (!Enum.TryParse(tp, keydep, out var key))
                {
                    throw new ArgumentException($"value is invalid for type {tp.Name}", nameof(keydep));
                }
                return _lastExecute[(T)key!].Lastexecute;
            }
        }

        public TimeSpan? LastDuration(string keydep)
        {
            lock (root)
            {
                var tp = typeof(T);
                if (!Enum.TryParse(tp, keydep, out var key))
                {
                    throw new ArgumentException($"value is invalid for type {tp.Name}", nameof(keydep));
                }
                return _lastExecute[(T)key!].Duration;
            }
        }

        public bool IsRunning(string keydep)
        {
            lock (root)
            {
                var tp = typeof(T);
                if (!Enum.TryParse(tp, keydep, out var key))
                {
                    throw new ArgumentException($"value is invalid for type {tp.Name}", nameof(keydep));
                }
                return _lastExecute[(T)key!].Running;
            }
        }
        public void UpdateLastCheck(string keydep, DateTime value, TimeSpan? duration)
        {
            lock (root)
            {
                var tp = typeof(T);
                if (!Enum.TryParse(tp, keydep, out var key))
                {
                    throw new ArgumentException($"value is invalid for type {tp.Name}", nameof(keydep));
                }
                (DateTime? Lastexecute ,TimeSpan? Duration, bool Running) aux = _lastExecute[(T)key!];
                aux.Lastexecute = value;
                aux.Duration = duration;
                _lastExecute[(T)key!] = aux;
            }
        }

        public void SetIsRunning(string keydep, bool value)
        {
            lock (root)
            {
                var tp = typeof(T);
                if (!Enum.TryParse(tp, keydep, out var key))
                {
                    throw new ArgumentException($"value is invalid for type {tp.Name}", nameof(keydep));
                }
                (DateTime? Lastexecute, TimeSpan? Duration, bool Running) aux = _lastExecute[(T)key!];
                aux.Running = value;
                _lastExecute[(T)key!] = aux;
            }
        }

        public HealthCheckResult StatusApp
        {
            get
            {
                lock (root)
                {
                    return _StatusApp;
                }
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
            lock (root) 
            {
                var tp = typeof(T);
                if (!tp.Equals(keydep.GetType()))
                {
                    throw new ArgumentException($"value is invalid for type {tp.Name}", nameof(keydep));
                }
                return _statusDeps[(T)keydep!];
            }
        }

        public HealthCheckResult StatusDep(string keydep)
        {
            lock (root)
            {
                var tp = typeof(T);
                if (!Enum.TryParse(tp, keydep, out var key))
                {
                    throw new ArgumentException($"value is invalid for type {tp.Name}", nameof(keydep));
                }
                return _statusDeps[(T)key!];
            }
        }

        public void UpdateStatusDep(string keydep, HealthCheckResult value)
        {
            lock (root)
            {
                var tp = typeof(T);
                if (!Enum.TryParse(tp, keydep, out var key))
                {
                    throw new ArgumentException($"value is invalid for type {tp.Name}", nameof(keydep));
                }
                _statusDeps[(T)key!] = value;
            }
        }

        public void SwithToUnhealthy(Enum keydep)
        {
            SwithState(keydep, HealthStatus.Unhealthy);
        }

        public void SwithToDegraded(Enum keydep)
        {
            SwithState(keydep, HealthStatus.Degraded);
        }


        private void SwithState(Enum keydep, HealthStatus status)
        {
            var tp = typeof(T);
            if (!tp.Equals(keydep.GetType()))
            {
                throw new ArgumentException($"value is invalid for type {tp.Name}", nameof(keydep));
            }
            if (IsRunning(keydep.ToString()) || _statusDeps[(T)keydep!].Status == status)
            {
                return;
            }
            lock (root)
            {
                SetIsRunning(keydep.ToString(), true);
                try
                {
                    var data = _statusDeps[(T)keydep!].Data;
                    UpdateStatusDep(keydep.ToString(), new HealthCheckResult(status, null, null, data));
                    UpdateLastCheck(keydep.ToString(), DateTime.Now, null);
                    if (_defaultoptions.Value.FuncStatusFail != null)
                    {
                        var sta = _defaultoptions.Value.FuncStatusFail.Invoke(this);
                        _StatusApp = new HealthCheckResult(sta, _StatusApp.Description, null, _StatusApp.Data);
                    }
                    else
                    {
                        _StatusApp = new HealthCheckResult(_defaultoptions!.Value.StatusFail, _StatusApp.Description, null, _StatusApp.Data);
                    }
                }
                finally
                {
                    SetIsRunning(keydep.ToString(), false);
                }
            }
        }
    }
}
