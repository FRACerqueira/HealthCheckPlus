using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlus
{
    /// <summary>
    /// Represents a publisher of <see cref="HealthReport"/> information.
    /// </summary>
    public interface IHealthCheckPlusPublisher
    {
        /// <summary>
        /// The Publisher Condition to execute. Default value is null (always run)
        /// </summary>
        public Func<HealthReport, bool> PublisherCondition { get; }
    }
}
