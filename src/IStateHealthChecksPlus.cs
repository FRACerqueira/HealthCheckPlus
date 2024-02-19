// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.options;
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
        HealthCheckResult StatusDep(Enum keydep);

        /// <summary>
        /// The last <see cref="HealthCheckResult"/> data for HealthCheck.
        /// </summary>
        /// <param name="keydep">
        /// The name dependence.
        /// </param>
        HealthCheckResult StatusDep(string keydep);

        /// <summary>
        /// Gets a <see cref="HealthStatus"/> representing the aggregate status of all the health checks.
        /// </summary>
        /// <param name="name">name of url request(<see cref="HealthCheckPlusOptions.HealthCheckName"/>).</param>
        /// <returns></returns>
        HealthStatus Status(string? name = null);

        /// <summary>
        /// Swith state to unhealthy.
        /// </summary>
        /// <param name="keydep">
        /// The enum value dependence.
        /// </param>
        void SwithToUnhealthy(Enum keydep);

        /// <summary>
        /// Swith state to unhealthy.
        /// </summary>
        /// <param name="keydep">
        /// The name dependence.
        /// </param>
        void SwithToUnhealthy(string keydep);

        /// <summary>
        /// Swith state to Degraded.
        /// </summary>
        /// <param name="keydep">
        /// The Enum value dependence.
        /// </param>
        void SwithToDegraded(Enum keydep);

        /// <summary>
        /// Swith state to Degraded.
        /// </summary>
        /// <param name="keydep">
        /// The name dependence.
        /// </param>
        void SwithToDegraded(string keydep);


        /// <summary>
        /// Try get all not healthy status.
        /// </summary>
        /// <param name="result">the Dictionary with all HealthCheck Result with not healthy status</param>
        /// <returns>True if found, oyherwise false.</returns>
        bool TryGetNotHealthy(out Dictionary<string,HealthCheckResult> result);

        /// <summary>
        /// Try get all healthy status.
        /// </summary>
        /// <param name="result">the Dictionary with all HealthCheck Result with healthy status</param>
        /// <returns>True if found, oyherwise false.</returns>
        bool TryGetHealthy(out Dictionary<string, HealthCheckResult> result);

        /// <summary>
        /// Try get all degraded status.
        /// </summary>
        /// <param name="result">the Dictionary with all HealthCheck Result with degraded status</param>
        /// <returns>True if found, oyherwise false.</returns>
        bool TryGetDegraded(out Dictionary<string, HealthCheckResult> result);

        /// <summary>
        /// Try get all unhealthy status.
        /// </summary>
        /// <param name="result">the Dictionary with all HealthCheck Result with unhealthy status</param>
        /// <returns>True if found, oyherwise false.</returns>
        bool TryGetUnhealthy(out Dictionary<string, HealthCheckResult> result);

        /// <summary>
        /// Convert <see cref="HealthReport"/> to <see cref="IDataHealthPlus"/>
        /// </summary>
        /// <param name="report">The report</param>
        /// <returns><see cref="IEnumerable{IDataHealthPlus}"/></returns>
        IEnumerable<IDataHealthPlus> ConvertToPlus(HealthReport report);

    }
}
