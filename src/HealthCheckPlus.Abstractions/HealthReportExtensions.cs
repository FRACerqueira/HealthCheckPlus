// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlus.Abstractions
{
    /// <summary>
    /// The Extensions for HealthReport
    /// </summary>
    public static class HealthReportExtensions
    {
        /// <summary>
        /// The last <see cref="HealthCheckResult"/> data for HealthCheck. IF not found return Unhealthy.
        /// </summary>
        /// <param name="report">The <see cref="HealthReport"/>.</param>
        /// <param name="keydep">The name dependence.</param>
        public static HealthStatus StatusResult(this HealthReport report, string keydep)
        {
            if (report.Entries.TryGetValue(keydep, out var entry))
            {
                return entry.Status;
            }
            return HealthStatus.Unhealthy; // Default to Unhealthy if not found
        }

        /// <summary>
        /// The last <see cref="HealthCheckResult"/> data for HealthCheck.
        /// </summary>
        /// <param name="report">The <see cref="HealthReport"/>.</param>
        /// <param name="keydep">
        /// The Enum value dependence.
        /// </param>
        public static HealthStatus StatusResult(this HealthReport report, Enum keydep)
        {
            return StatusResult(report, keydep.ToString());
        }

        /// <summary>
        /// Try get all not healthy status.
        /// </summary>
        /// <param name="report">The <see cref="HealthReport"/>.</param>
        /// <param name="result">the Dictionary with all HealthCheck Result with not healthy status</param>
        /// <returns>True if found, otherwise false.</returns>
        public static bool TryGetNotHealthy(this HealthReport report, out IReadOnlyDictionary<string, HealthCheckResult> result)
        {
            return TryGetByStatus(report, out result, status => status != HealthStatus.Healthy);
        }

        /// <summary>
        /// Try get all healthy status.
        /// </summary>
        /// <param name="report">The <see cref="HealthReport"/>.</param>
        /// <param name="result">the Dictionary with all HealthCheck Result with healthy status</param>
        /// <returns>True if found, otherwise false.</returns>
        public static bool TryGetHealthy(this HealthReport report, out IReadOnlyDictionary<string, HealthCheckResult> result)
        {
            return TryGetByStatus(report, out result, status => status == HealthStatus.Healthy);
        }

        /// <summary>
        /// Try get all degraded status.
        /// </summary>
        /// <param name="report">The <see cref="HealthReport"/>.</param>
        /// <param name="result">the Dictionary with all HealthCheck Result with degraded status</param>
        /// <returns>True if found, otherwise false.</returns>
        public static bool TryGetDegraded(this HealthReport report, out IReadOnlyDictionary<string, HealthCheckResult> result)
        {
            return TryGetByStatus(report, out result, status => status == HealthStatus.Degraded);
        }

        /// <summary>
        /// Try get all unhealthy status.
        /// </summary>
        /// <param name="report">The <see cref="HealthReport"/>.</param>
        /// <param name="result">the Dictionary with all HealthCheck Result with unhealthy status</param>
        /// <returns>True if found, otherwise false.</returns>
        public static bool TryGetUnhealthy(this HealthReport report, out IReadOnlyDictionary<string, HealthCheckResult> result)
        {
            return TryGetByStatus(report, out result, status => status == HealthStatus.Unhealthy);
        }

        private static bool TryGetByStatus(HealthReport report, out IReadOnlyDictionary<string, HealthCheckResult> result, Func<HealthStatus, bool> predicate)
        {
            result = (IReadOnlyDictionary<string, HealthCheckResult>)report.Entries.Where(entry => predicate(entry.Value.Status))
                .ToDictionary(entry => entry.Key, entry => new HealthCheckResult(entry.Value.Status,entry.Value.Description, entry.Value.Exception,entry.Value.Data));
            return result.Count !=0;
        }
    }
}
