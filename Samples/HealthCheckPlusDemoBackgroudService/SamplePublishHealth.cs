using HealthCheckPlus.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlusDemoBackgroudService
{
    public class SamplePublishHealth : IHealthCheckPlusPublisher
    {
        public Func<HealthReport, bool>? PublisherCondition { get; set; }  = (_) => true;

        public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
