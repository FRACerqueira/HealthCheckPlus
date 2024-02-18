using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlusDemo
{
    public class HcTeste2 : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HealthCheckResult(context.Registration.FailureStatus, "teste2"));
        }
    }
}
