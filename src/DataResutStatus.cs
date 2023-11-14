// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using System;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlus
{
    /// <summary>
    /// Represents the HealthCheck result data.
    /// </summary>
    public readonly struct DataResutStatus
    {
        /// <summary>
        /// Create DataResutStatus.
        /// <br>Do not use this constructor!</br>
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public DataResutStatus()
        {
            throw new NotImplementedException();                
        }

        /// <summary>
        /// Create DataResutStatus.
        /// </summary>
        /// <param name="name">The name of HealthCheck.</param>
        /// <param name="description">The description.</param>
        /// <param name="status">The <see cref="HealthStatus"/>.</param>
        /// <param name="error">The <see cref="Exception"/>.</param>
        /// <param name="date">The date of result data.</param>
        /// <param name="elapsedTime">The elapsed time of HealthCheck.</param>

        public DataResutStatus(string name, string? description, HealthStatus status, Exception? error, DateTime? date, TimeSpan? elapsedTime)
        {
            Name = name;
            Description = description;
            Status = status;
            Error = error;
            Date = date;
            ElapsedTime = elapsedTime;
        }

        /// <summary>
        /// The name of HealthCheck.
        /// </summary>
        public string Name { get;  }

        /// <summary>
        /// The <see cref="HealthStatus"/>.
        /// </summary>
        public HealthStatus Status { get; }

        /// <summary>
        /// The <see cref="Exception"/>.
        /// </summary>
        public Exception? Error { get; }

        /// <summary>
        /// The description.
        /// </summary>
        public string? Description { get;  }

        /// <summary>
        /// The date of result data.
        /// </summary>
        public DateTime? Date { get; }

        /// <summary>
        /// The elapsed time of HealthCheck.
        /// </summary>
        public TimeSpan? ElapsedTime { get; }

    }
}
