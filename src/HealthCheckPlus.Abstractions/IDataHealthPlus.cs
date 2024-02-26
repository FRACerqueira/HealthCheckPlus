// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlus.Abstractions
{
    /// <summary>
    /// Represents data from the last Health Check performed
    /// </summary>
    public interface IDataHealthPlus
    {
        /// <summary>
        /// Name of health check
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The time the health check took to execute.
        /// </summary>
        public TimeSpan Duration { get; }
        /// <summary>
        /// The date reference of last execute.
        /// </summary>
        public DateTime Dateref { get; }
        /// <summary>
        /// The result, see <see cref="HealthCheckResult"/>.
        /// </summary>
        public HealthCheckResult Lastresult { get; }
        /// <summary>
        /// The result source 
        /// </summary>
        public HealthCheckTrigger Origin { get; }
    }
}
