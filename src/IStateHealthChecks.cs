// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Generic;

namespace HealthCheckPlus
{
    /// <summary>
    /// Represents the commands of the HealthChecksPlus for access data
    /// </summary>
    public interface IStateHealthChecksPlus
    {
        /// <summary>
        /// The last <see cref="HealthCheckResult"/> data for application.
        /// </summary>
        HealthCheckResult StatusApp { get; }
        /// <summary>
        /// The last <see cref="HealthCheckResult"/> data for dependence.
        /// </summary>
        /// <param name="keydep">
        /// The Enum value dependence.
        /// </param>
        HealthCheckResult StatusDep(Enum keydep);

        /// <summary>
        /// Swith state to unhealthy.
        /// </summary>
        /// <param name="keydep">
        /// The Enum value dependence.
        /// </param>
        void SwithToUnhealthy(Enum keydep);

        /// <summary>
        /// Swith state to Degraded.
        /// </summary>
        /// <param name="keydep">
        /// The Enum value dependence.
        /// </param>
        void SwithToDegraded(Enum keydep);


        /// <summary>
        /// Try get all not healthy status.
        /// </summary>
        /// <param name="result"><see cref="DataResutStatus"/> healthy.</param>
        /// <returns>True if found, oyherwise false.</returns>
        bool TryGetNotHealthy(out IEnumerable<DataResutStatus> result);

        /// <summary>
        /// Try get all healthy status.
        /// </summary>
        /// <param name="result"><see cref="DataResutStatus"/> healthy.</param>
        /// <returns>True if found, oyherwise false.</returns>
        bool TryGetHealthy(out IEnumerable<DataResutStatus> result);

        /// <summary>
        /// Try get all degraded status.
        /// </summary>
        /// <param name="result"><see cref="DataResutStatus"/> degrated.</param>
        /// <returns>True if found, oyherwise false.</returns>
        bool TryGetDegraded(out IEnumerable<DataResutStatus> result);

        /// <summary>
        /// Try get all unhealthy status.
        /// </summary>
        /// <param name="result"><see cref="DataResutStatus"/> unhealthy.</param>
        /// <returns>True if found, oyherwise false.</returns>
        bool TryGetUnhealthy(out IEnumerable<DataResutStatus> result);

    }
}
