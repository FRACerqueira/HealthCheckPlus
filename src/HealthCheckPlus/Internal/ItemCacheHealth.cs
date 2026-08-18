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

        private CheckResultSnapshot _snapshot = new(default, default, default, HealthCheckTrigger.None);

        public TimeSpan Duration => _snapshot.Duration;

        public DateTime DateRef => _snapshot.DateRef;

        public HealthCheckResult LastResult => _snapshot.LastResult;

        public HealthCheckTrigger Origin => _snapshot.Origin;

        // A caller that needs more than one of the four properties above together (e.g.
        // building a report entry, which needs LastResult and Duration in the same breath) must
        // read this ONCE and pull every field from the same local value - reading the properties
        // above separately, one statement at a time, defeats the whole point of bundling them:
        // a concurrent SetResult() between two of those separate reads would still hand back a
        // mix of an old and a new generation, even though neither individual property read was
        // itself torn.
        public CheckResultSnapshot Snapshot => _snapshot;

        public bool Running { get; set; }

        // Populated separately from InitCache (see CacheHealthCheckPlus.SetTags) - the
        // registrations that carry a check's Tags aren't known yet when InitCache runs (it only
        // has the plain `names` list passed to AddHealthChecksPlus). Used by CreateReport() so a
        // callback consuming the report through a named status (IStateHealthChecksPlus.Status)
        // sees the same Tags a callback consuming it through the HTTP endpoint's own
        // StatusHealthReport sees.
        public IEnumerable<string> Tags { get; set; } = [];

        // The only way to change LastResult/DateRef/Duration/Origin - always together, as a
        // single atomic reference swap. See CheckResultSnapshot for why this matters.
        public void SetResult(HealthCheckResult lastResult, DateTime dateRef, TimeSpan duration, HealthCheckTrigger origin)
        {
            _snapshot = new CheckResultSnapshot(lastResult, dateRef, duration, origin);
        }
    }
}
