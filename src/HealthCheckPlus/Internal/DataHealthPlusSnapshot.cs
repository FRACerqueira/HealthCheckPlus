// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlus.Internal
{
    // What CacheHealthCheckPlus.ConvertToPlus actually hands out - a frozen, point-in-time copy
    // of one ItemCacheHealth, not the live cache entry itself. Name keeps a public setter only
    // because IDataHealthPlus declares one; assigning to it here mutates nothing but this
    // caller-owned instance.
    internal sealed class DataHealthPlusSnapshot : IDataHealthPlus
    {
        public string Name { get; set; }

        public TimeSpan Duration { get; }

        public DateTime DateRef { get; }

        public HealthCheckResult LastResult { get; }

        public HealthCheckTrigger Origin { get; }

        public DataHealthPlusSnapshot(string name, CheckResultSnapshot snapshot)
        {
            Name = name;
            Duration = snapshot.Duration;
            DateRef = snapshot.DateRef;
            LastResult = snapshot.LastResult;
            Origin = snapshot.Origin;
        }
    }
}
