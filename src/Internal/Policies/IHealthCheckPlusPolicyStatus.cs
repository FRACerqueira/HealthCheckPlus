// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlus.Internal.Policies
{
    internal interface IHealthCheckPlusPolicyStatus
    {
        HealthStatus PolicyForStatus { get; }
        TimeSpan? PolicyPeriod { get; }
        TimeSpan? PolicyDelay { get; }
        string PolicyNameDep { get; }
    }
}
