// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlus.Abstractions
{
    /// <summary>
    /// Represents a publisher of <see cref="HealthReport"/> information.
    /// </summary>
    public interface IHealthCheckPlusPublisher : IHealthCheckPublisher
    {
        /// <summary>
        /// Gets or sets the condition to execute the publisher. Default value is null (always run).
        /// </summary>
        Func<HealthReport, bool>? PublisherCondition { get; set; }
    }
}
