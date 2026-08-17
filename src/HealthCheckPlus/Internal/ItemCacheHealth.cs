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

        // Populated separately from InitCache (see CacheHealthCheckPlus.SetTags) - the
        // registrations that carry a check's Tags aren't known yet when InitCache runs (it only
        // has the plain `names` list passed to AddHealthChecksPlus). Used by CreateReport() so a
        // callback consuming the report through a named status (IStateHealthChecksPlus.Status)
        // sees the same Tags a callback consuming it through the HTTP endpoint's own
        // StatusHealthReport sees.
        public IEnumerable<string> Tags { get; set; } = [];
    }
}
