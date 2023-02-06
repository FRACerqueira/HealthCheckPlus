using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace HealthCheckPlus.Internal
{
    internal sealed class HealthCheckPlusOptions
    {
        public string NameCheckApp { get; set; }
        public HealthStatus StatusFail { get; set; }
        public LogLevel LogLevelCheckHealthy { get; set; } = LogLevel.Information;
        public LogLevel LogLevelCheckDegraded { get; set; } = LogLevel.Warning;
        public LogLevel LogLevelCheckUnhealthy { get; set; } = LogLevel.Error;
    }
}
