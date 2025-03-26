// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlus.Internal.Policies
{
    internal record HealthCheckPlusPolicyStatus(
            HealthStatus PolicyForStatus,
            TimeSpan? PolicyDelay,
            TimeSpan? PolicyPeriod,
            string PolicyNameDep
        ) : IHealthCheckPlusPolicyStatus;
}
