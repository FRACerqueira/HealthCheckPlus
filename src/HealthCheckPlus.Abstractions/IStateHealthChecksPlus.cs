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
        /// <exception cref="ArgumentException"><paramref name="keydep"/> is not a registered health check name.</exception>
        HealthCheckResult StatusResult(string keydep);

        /// <summary>
        /// Gets a <see cref="HealthStatus"/> representing the aggregate status of all the health checks.
        /// </summary>
        /// <param name="name">The named aggregate to look up - the value passed as <c>HealthCheckName</c> to a <c>HealthCheckPlusOptions</c> used with <c>UseHealthChecksPlus(path, options)</c> - or <c>null</c>/empty for the default aggregate over every tracked check.</param>
        /// <returns>The aggregate <see cref="HealthStatus"/>.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is a non-empty string that was never registered as a <c>HealthCheckPlusOptions.HealthCheckName</c>. A <c>null</c> or empty <paramref name="name"/> never throws - both return the same aggregate as the parameterless call.</exception>
        HealthStatus Status(string? name = null);

        /// <summary>
        /// Switches the state to unhealthy.
        /// </summary>
        /// <param name="keydep">The name dependence.</param>
        /// <exception cref="ArgumentException"><paramref name="keydep"/> is not a registered health check name.</exception>
        void SwitchToUnhealthy(string keydep);

        /// <summary>
        /// Switches the state to degraded.
        /// </summary>
        /// <param name="keydep">The name dependence.</param>
        /// <exception cref="ArgumentException"><paramref name="keydep"/> is not a registered health check name.</exception>
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
        /// <returns>An enumerable of <see cref="IDataHealthPlus"/>. Fully evaluated eagerly, not lazily,
        /// so any exception below surfaces immediately rather than mid-enumeration.</returns>
        /// <exception cref="ArgumentException"><paramref name="report"/> contains an entry for a name that
        /// isn't (or is no longer) a registered health check.</exception>
        IEnumerable<IDataHealthPlus> ConvertToPlus(HealthReport report);
    }
}
