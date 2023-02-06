using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlus.Internal
{
    internal class DataResutStatus
    {
        public string Name { get; set; }
        public HealthStatus Status { get; set; }
        public string? Error { get; set; }
    }
}
