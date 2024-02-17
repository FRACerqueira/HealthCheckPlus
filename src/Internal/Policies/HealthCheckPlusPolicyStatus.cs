// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlus.Internal.Policies
{
    internal class HealthCheckPlusPolicyStatus(HealthStatus status, TimeSpan? policyDelay,TimeSpan? policyTime, string policyNameDep) : IHealthCheckPlusPolicyStatus
    {
        public HealthStatus PolicyForStatus => status;
        public TimeSpan? PolicyPeriod { get; } = policyTime;
        public TimeSpan? PolicyDelay { get; } = policyDelay;
        public string PolicyNameDep { get; } = policyNameDep;
    }
}
