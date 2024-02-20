// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlus
{
    /// <summary>
    /// The Extensions for HealthReport
    /// </summary>
    public static class HealthReportExtensions
    {
        /// <summary>
        /// The last <see cref="HealthCheckResult"/> data for HealthCheck.
        /// </summary>
        /// <param name="report">The <see cref="HealthReport"/>.</param>
        /// <param name="keydep">
        /// The name dependence.
        /// </param>
        public static HealthStatus StatusResult(this HealthReport report, string keydep)
        {
            var aux = report.Entries
               .Where(x => x.Key == keydep);
            if (!aux.Any())
            {
                throw new ArgumentException($"The {nameof(keydep)} must not found.", nameof(keydep));
            }
            return aux
                .Select(X => X.Value.Status)
                .FirstOrDefault();
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
        /// <returns>True if found, oyherwise false.</returns>
        public static bool TryGetNotHealthy(this HealthReport report, out Dictionary<string, HealthCheckResult> result)
        {
            result = GetEntriesStatus(report, x => x.Status != HealthStatus.Healthy);
            return result.Count != 0;
        }

        /// <summary>
        /// Try get all healthy status.
        /// </summary>
        /// <param name="report">The <see cref="HealthReport"/>.</param>
        /// <param name="result">the Dictionary with all HealthCheck Result with healthy status</param>
        /// <returns>True if found, oyherwise false.</returns>
        public static bool TryGetHealthy(this HealthReport report, out Dictionary<string, HealthCheckResult> result)
        {
            result = GetEntriesStatus(report, x => x.Status == HealthStatus.Healthy);
            return result.Count != 0;
        }

        /// <summary>
        /// Try get all degraded status.
        /// </summary>
        /// <param name="report">The <see cref="HealthReport"/>.</param>
        /// <param name="result">the Dictionary with all HealthCheck Result with degraded status</param>
        /// <returns>True if found, oyherwise false.</returns>
        public static bool TryGetDegraded(this HealthReport report, out Dictionary<string, HealthCheckResult> result)
        {
            result = GetEntriesStatus(report, x => x.Status == HealthStatus.Degraded);
            return result.Count != 0;
        }

        /// <summary>
        /// Try get all unhealthy status.
        /// </summary>
        /// <param name="report">The <see cref="HealthReport"/>.</param>
        /// <param name="result">the Dictionary with all HealthCheck Result with unhealthy status</param>
        /// <returns>True if found, oyherwise false.</returns>
        public static bool TryGetUnhealthy(this HealthReport report, out Dictionary<string, HealthCheckResult> result)
        {
            result = GetEntriesStatus(report,x => x.Status == HealthStatus.Unhealthy);
            return result.Count != 0;
        }

        private static Dictionary<string, HealthCheckResult> GetEntriesStatus(HealthReport report, Predicate<HealthReportEntry> predicate)
        {
            Dictionary<string, HealthCheckResult>  result = [];
            foreach (var item in report.Entries.Where(x => predicate(x.Value)))
            {
                result.Add(item.Key, new HealthCheckResult(item.Value.Status, item.Value.Description, item.Value.Exception, item.Value.Data));
            }
            return result;
        }
    }
}
