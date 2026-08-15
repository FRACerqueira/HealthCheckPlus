using HealthCheckPlus.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlusDemoBackgroudService
{
    // A publisher is where you'd forward the aggregate report to wherever your team actually
    // watches health (a dashboard, an alerting webhook, a metrics backend). This one just logs a
    // one-line summary, so running this sample makes the effect of AfterIdleCount/WhenReportChange
    // (configured on AddBackgroundPolicy in Program.cs) visible in the console output.
    public class SamplePublishHealth(ILogger<SamplePublishHealth> logger) : IHealthCheckPlusPublisher
    {
        // A custom per-publisher gate, evaluated after the background service's own
        // AfterIdleCount/WhenReportChange filters (configured in Program.cs) already passed - only
        // publish when Redis specifically isn't Healthy. Program.cs points Redis at a placeholder
        // connection string, so in this sample it's Unhealthy by default and this fires often;
        // point it at a real instance to see the difference.
        public Func<HealthReport, bool>? PublisherCondition { get; set; } = report =>
            !report.Entries.TryGetValue("Redis", out var redis) || redis.Status != HealthStatus.Healthy;

        public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
        {
            logger.LogInformation(
                "HealthCheckPlus publish: overall={OverallStatus}, entries=[{Entries}]",
                report.Status,
                string.Join(", ", report.Entries.Select(e => $"{e.Key}={e.Value.Status}")));

            return Task.CompletedTask;
        }
    }
}
