using HealthCheckPlus;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlusDemoBackgroudService
{
    public class SamplePublishHealth : IHealthCheckPublisher, IHealthCheckPlusPublisher
    {
        public Func<HealthReport, bool> PublisherCondition => (_) => true;
        public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
