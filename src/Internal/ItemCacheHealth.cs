// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlus.Internal
{
    internal class ItemCacheHealth : IDataHealthPlus
    {
        public string Name { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public DateTime Dateref { get; set; }
        public HealthCheckResult Lastresult { get; set; }
        public HealthCheckTrigger Origin { get; set; }
        public bool Running { get; set; }
    }
}
