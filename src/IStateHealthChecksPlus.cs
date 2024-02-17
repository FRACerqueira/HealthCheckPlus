// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlus
{
    /// <summary>
    /// Represents the commands of the HealthChecksPlus for access data
    /// </summary>
    public interface IStateHealthChecksPlus
    {
        /// <summary>
        /// The last <see cref="HealthCheckResult"/> data for HealthCheck.
        /// </summary>
        /// <param name="keydep">
        /// The Enum value dependence.
        /// </param>
        HealthCheckResult StatusDep(string keydep);

        /// <summary>
        /// Gets a <see cref="HealthStatus"/> representing the aggregate status of all the health checks.
        /// </summary>
        HealthStatus Status(string? name = null);

        /// <summary>
        /// Swith state to unhealthy.
        /// </summary>
        /// <param name="keydep">
        /// The name value dependence.
        /// </param>
        void SwithToUnhealthy(string keydep);

        /// <summary>
        /// Swith state to Degraded.
        /// </summary>
        /// <param name="keydep">
        /// The name value dependence.
        /// </param>
        void SwithToDegraded(string keydep);


        /// <summary>
        /// Try get all not healthy status.
        /// </summary>
        /// <param name="result"><see cref="IEnumerable{HealthCheckResult}"/> healthy.</param>
        /// <returns>True if found, oyherwise false.</returns>
        bool TryGetNotHealthy(out IEnumerable<HealthCheckResult> result);

        /// <summary>
        /// Try get all healthy status.
        /// </summary>
        /// <param name="result"><see cref="IEnumerable{HealthCheckResult}"/> healthy.</param>
        /// <returns>True if found, oyherwise false.</returns>
        bool TryGetHealthy(out IEnumerable<HealthCheckResult> result);

        /// <summary>
        /// Try get all degraded status.
        /// </summary>
        /// <param name="result"><see cref="IEnumerable{HealthCheckResult}"/> degrated.</param>
        /// <returns>True if found, oyherwise false.</returns>
        bool TryGetDegraded(out IEnumerable<HealthCheckResult> result);

        /// <summary>
        /// Try get all unhealthy status.
        /// </summary>
        /// <param name="result"><see cref="IEnumerable{HealthCheckResult}"/> unhealthy.</param>
        /// <returns>True if found, oyherwise false.</returns>
        bool TryGetUnhealthy(out IEnumerable<HealthCheckResult> result);

        /// <summary>
        /// Convert <see cref="HealthReport"/> to <see cref="IDataHealthPlus"/>
        /// </summary>
        /// <param name="report">The report</param>
        /// <returns><see cref="IEnumerable{IDataHealthPlus}"/></returns>
        IEnumerable<IDataHealthPlus> ConvertToPlus(HealthReport report);

    }
}
