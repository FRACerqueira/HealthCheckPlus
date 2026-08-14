// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlusTests
{
    // Regression test for the action plan (doc/plano-acao-healthcheckplus.md), step P0.7.
    // Low finding from the audit (doc/healthcheckplus-audit.html): the error thrown when
    // UseHealthChecksPlus is called before AddHealthChecks had a malformed message
    // ("Unable Find AddHealthChecks)" — stray parenthesis, missing words).
    public class HealthChecksPlusAppExtensionTests
    {
        [Fact]
        public void UseHealthChecksPlus_ShouldThrowClearException_WhenAddHealthChecksNotCalled()
        {
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();
            var app = new ApplicationBuilder(provider);

            var ex = Assert.Throws<InvalidOperationException>(() => app.UseHealthChecksPlus("/health"));

            Assert.Contains(nameof(HealthCheckServiceCollectionExtensions.AddHealthChecks), ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("Unable Find", ex.Message, StringComparison.Ordinal);
        }
    }
}
