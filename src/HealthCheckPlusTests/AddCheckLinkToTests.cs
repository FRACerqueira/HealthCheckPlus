// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace HealthCheckPlusTests
{
    // Integration tests for the action plan (doc/plano-acao-healthcheckplus.md), step P2.4.
    // Covers the AddCheckLinkTo mechanism that replaced the reflective bridge into internal
    // ASP.NET Core option types (Fase 2 — architectural priority #1 from the audit,
    // doc/healthcheckplus-audit.html).
    public class AddCheckLinkToTests
    {
        private sealed class AlwaysHealthyCheck : IHealthCheck
        {
            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(HealthCheckResult.Healthy());
            }
        }

        [Fact]
        public void AddCheckLinkTo_ShouldReplaceOriginalRegistration_WithTheWrappedName()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var ihb = services.AddHealthChecksPlus(["Adopted"]);
            ihb.Add(new HealthCheckRegistration("Original", _ => new AlwaysHealthyCheck(), null, null));
            ihb.AddCheckLinkTo("Adopted", "Original");

            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

            Assert.Contains(options.Registrations, r => r.Name == "Adopted");
            Assert.DoesNotContain(options.Registrations, r => r.Name == "Original");
        }

        [Fact]
        public void AddCheckLinkTo_ShouldThrowClearException_WhenNamedCheckWasNeverRegistered()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var ihb = services.AddHealthChecksPlus(["Adopted"]);
            ihb.AddCheckLinkTo("Adopted", "DoesNotExist");

            using var provider = services.BuildServiceProvider();

            var ex = Assert.Throws<InvalidOperationException>(
                () => provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value);
            Assert.Contains("DoesNotExist", ex.Message, StringComparison.Ordinal);
        }
    }
}
