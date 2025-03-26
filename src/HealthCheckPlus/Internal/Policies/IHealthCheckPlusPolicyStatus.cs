// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlus.Internal.Policies
{
    /// <summary>
    /// Represents the status of a health check policy.
    /// </summary>
    internal interface IHealthCheckPlusPolicyStatus
    {
        /// <summary>
        /// Gets the health status associated with the policy.
        /// </summary>
        HealthStatus PolicyForStatus { get; }

        /// <summary>
        /// Gets the period for which the policy is valid.
        /// </summary>
        TimeSpan? PolicyPeriod { get; }

        /// <summary>
        /// Gets the delay before the policy is applied.
        /// </summary>
        TimeSpan? PolicyDelay { get; }

        /// <summary>
        /// Gets the name of the policy dependency.
        /// </summary>
        string PolicyNameDep { get; }
    }
}
