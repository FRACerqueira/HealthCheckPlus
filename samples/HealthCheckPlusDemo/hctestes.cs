using HealthCheckPlus;
using Microsoft.Extensions.Diagnostics.HealthChecks;


namespace HealthCheckPlusDemo
{
    public class HcTeste1 : BaseHealthCheckPlus
    {
        public HcTeste1(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        public override async Task<HealthCheckResult> DoHealthCheck(HealthCheckContext context, CancellationToken cancellationToken)
        {
            return await Task.FromResult(HealthCheckResult.Healthy($"teste1"));
        }
    }
    public class HcTeste2 : BaseHealthCheckPlus
    {
        public HcTeste2(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        public override async Task<HealthCheckResult> DoHealthCheck(HealthCheckContext context, CancellationToken cancellationToken)
        {
            return await Task.FromResult(new HealthCheckResult(context.Registration.FailureStatus, "teste2"));
        }
    }
}
