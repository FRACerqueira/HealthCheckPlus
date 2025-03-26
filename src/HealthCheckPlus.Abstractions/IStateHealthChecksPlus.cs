// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlus.Abstractions
{
    /// <summary>
    /// Represents the commands of the HealthChecksPlus for access data.
    /// </summary>
    public interface IStateHealthChecksPlus
    {
        /// <summary>
        /// Gets the last <see cref="HealthCheckResult"/> data for a health check.
        /// </summary>
        /// <param name="keydep">The name dependence.</param>
        /// <returns>The last <see cref="HealthCheckResult"/>.</returns>
        HealthCheckResult StatusResult(string keydep);

        /// <summary>
        /// Gets a <see cref="HealthStatus"/> representing the aggregate status of all the health checks.
        /// </summary>
        /// <param name="name">The name for the URL request.</param>
        /// <returns>The aggregate <see cref="HealthStatus"/>.</returns>
        HealthStatus Status(string? name = null);

        /// <summary>
        /// Switches the state to unhealthy.
        /// </summary>
        /// <param name="keydep">The name dependence.</param>
        void SwitchToUnhealthy(string keydep);

        /// <summary>
        /// Switches the state to degraded.
        /// </summary>
        /// <param name="keydep">The name dependence.</param>
        void SwitchToDegraded(string keydep);

        /// <summary>
        /// Tries to get all not healthy statuses.
        /// </summary>
        /// <param name="result">The dictionary with all <see cref="HealthCheckResult"/> with not healthy status.</param>
        /// <returns>True if found, otherwise false.</returns>
        bool TryGetNotHealthy(out IReadOnlyDictionary<string, HealthCheckResult> result);

        /// <summary>
        /// Tries to get all healthy statuses.
        /// </summary>
        /// <param name="result">The dictionary with all <see cref="HealthCheckResult"/> with healthy status.</param>
        /// <returns>True if found, otherwise false.</returns>
        bool TryGetHealthy(out IReadOnlyDictionary<string, HealthCheckResult> result);

        /// <summary>
        /// Tries to get all degraded statuses.
        /// </summary>
        /// <param name="result">The dictionary with all <see cref="HealthCheckResult"/> with degraded status.</param>
        /// <returns>True if found, otherwise false.</returns>
        bool TryGetDegraded(out IReadOnlyDictionary<string, HealthCheckResult> result);

        /// <summary>
        /// Tries to get all unhealthy statuses.
        /// </summary>
        /// <param name="result">The dictionary with all <see cref="HealthCheckResult"/> with unhealthy status.</param>
        /// <returns>True if found, otherwise false.</returns>
        bool TryGetUnhealthy(out IReadOnlyDictionary<string, HealthCheckResult> result);

        /// <summary>
        /// Converts a <see cref="HealthReport"/> to <see cref="IDataHealthPlus"/>.
        /// </summary>
        /// <param name="report">The health report.</param>
        /// <returns>An enumerable of <see cref="IDataHealthPlus"/>.</returns>
        IEnumerable<IDataHealthPlus> ConvertToPlus(HealthReport report);
    }
}
