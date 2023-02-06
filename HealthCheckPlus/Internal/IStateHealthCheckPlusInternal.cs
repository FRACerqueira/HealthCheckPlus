using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;

namespace HealthCheckPlus.Internal
{
    internal interface IStateHealthCheckPlusInternal
    {
        DateTime? LastCheck(string keydep);
        void UpdateLastCheck(string keydep, DateTime value);
        HealthCheckResult StatusDep(string keydep);
        void UpdateStatusDep(string keydep, HealthCheckResult value);
        HealthCheckResult StatusApp { get; set; }
        void SetDelayInternal(string keydep, TimeSpan delay, TimeSpan interval);
        TimeSpan Delay(string keydep);
        TimeSpan Interval(string keydep);
        bool IsRunning(string keydep);
        void SetIsRunning(string keydep, bool value);
    }
}
