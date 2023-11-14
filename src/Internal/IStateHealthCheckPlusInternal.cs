// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;

namespace HealthCheckPlus.Internal
{
    internal interface IStateHealthCheckPlusInternal
    {
        DateTime? LastCheck(string keydep);
        TimeSpan? LastDuration(string keydep);

        void UpdateLastCheck(string keydep, DateTime value, TimeSpan? duration);
        HealthCheckResult StatusDep(string keydep);
        void UpdateStatusDep(string keydep, HealthCheckResult value);
        HealthCheckResult StatusApp { get; set; }
        void SetDelayInternal(string keydep, TimeSpan delay, TimeSpan interval);
        TimeSpan Delay(string keydep);
        TimeSpan Interval(string keydep);
        bool IsRunning(string keydep);
        void SetIsRunning(string keydep, bool value);
        DateTime DateRegister { get; }
    }
}
