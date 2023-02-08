using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;

namespace HealthCheckPlus
{
    /// <summary>
    /// HealthCheckPlus: Public interface for access data <see cref="HealthCheckResult"/> Application and Dependences.
    /// </summary>
    public interface IStateHealthChecksPlus
    {
        /// <summary>
        /// HealthCheckPlus: <see cref="HealthCheckResult"/> data Application.
        /// </summary>
        HealthCheckResult StatusApp { get; }
        /// <summary>
        /// HealthCheckPlus: <see cref="HealthCheckResult"/> data Dependence.
        /// </summary>
        /// <param name="keydep">
        /// HealthCheckPlus: Enum HealthCheck value Dependence
        /// </param>
        HealthCheckResult StatusDep(Enum keydep);
    }
}
