// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlus.Abstractions
{
    /// <summary>
    /// Represents data from the last Health Check performed.
    /// </summary>
    public interface IDataHealthPlus
    {
        /// <summary>
        /// Gets/Set the name of the health check.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Gets the time the health check took to execute.
        /// </summary>
        TimeSpan Duration { get; }

        /// <summary>
        /// Gets the date reference of the last execution.
        /// </summary>
        DateTime DateRef { get; }

        /// <summary>
        /// Gets the result of the last health check, see <see cref="HealthCheckResult"/>.
        /// </summary>
        HealthCheckResult LastResult { get; }

        /// <summary>
        /// Gets the origin of the health check result.
        /// </summary>
        HealthCheckTrigger Origin { get; }
    }
}
