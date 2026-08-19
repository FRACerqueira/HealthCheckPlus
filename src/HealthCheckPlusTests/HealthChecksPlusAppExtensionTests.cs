// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlusTests
{
    // Regression test: UseHealthChecksPlus called before AddHealthChecksPlus must throw a clear,
    // well-formed error naming both methods.
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
