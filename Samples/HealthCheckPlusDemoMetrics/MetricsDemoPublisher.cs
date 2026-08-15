using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlusDemoMetrics
{
    // A minimal publisher so healthcheckplus.publisher.invocations/duration also have something to
    // show - see docs/RUNBOOK.md for what each publisher.result value means.
    public class MetricsDemoPublisher(ILogger<MetricsDemoPublisher> logger) : IHealthCheckPublisher
    {
        public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
        {
            logger.LogInformation("Publish: overall={OverallStatus}", report.Status);
            return Task.CompletedTask;
        }
    }
}
