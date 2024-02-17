using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlusDemoBackgroudService
{
    public class HcTeste1 : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(HealthCheckResult.Healthy($"teste1"));
        }
    }
}
