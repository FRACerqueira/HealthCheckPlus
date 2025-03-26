// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlus.Internal
{
    internal class ItemCacheHealth : IDataHealthPlus
    {
        public string Name { get; set; } = string.Empty;

        public TimeSpan Duration { get; set; }

        public DateTime DateRef { get; set; }

        public HealthCheckResult LastResult { get; set; }

        public HealthCheckTrigger Origin { get; set; }

        public bool Running { get; set; }
    }
}
