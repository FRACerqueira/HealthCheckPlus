// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlusTests
{
    // Regression coverage for the "period must be at least one second" rule shared via
    // PeriodValidation across AddUnhealthyPolicy/AddDegradedPolicy/AddCheckPlus/AddCheckLinkTo -
    // each used to reimplement this check independently, with no test exercising the exception
    // path at any of these four call sites.
    public class HealthChecksPlusExtensionValidationTests
    {
        private sealed class AlwaysHealthyCheck : IHealthCheck
        {
            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(HealthCheckResult.Healthy());
            }
        }

        private static IHealthChecksBuilder BuildBuilder()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            return services.AddHealthChecksPlus(["Test1"]);
        }

        [Fact]
        public void AddUnhealthyPolicy_ShouldRejectPeriodBelowOneSecond()
        {
            var ihb = BuildBuilder();

            Assert.Throws<ArgumentException>(() => ihb.AddUnhealthyPolicy("Test1", TimeSpan.FromMilliseconds(500)));
        }

        [Fact]
        public void AddDegradedPolicy_ShouldRejectPeriodBelowOneSecond()
        {
            var ihb = BuildBuilder();

            Assert.Throws<ArgumentException>(() => ihb.AddDegradedPolicy("Test1", TimeSpan.FromMilliseconds(500)));
        }

        [Fact]
        public void AddCheckPlus_ShouldRejectPeriodBelowOneSecond_WhenProvided()
        {
            var ihb = BuildBuilder();

            Assert.Throws<ArgumentException>(() => ihb.AddCheckPlus<AlwaysHealthyCheck>("Test1", period: TimeSpan.FromMilliseconds(500)));
        }

        [Fact]
        public void AddCheckPlus_ShouldAllowOmittedPeriod()
        {
            var ihb = BuildBuilder();

            ihb.AddCheckPlus<AlwaysHealthyCheck>("Test1");
            // No exception thrown is the assertion - period is optional and must not be validated
            // when absent.
        }

        [Fact]
        public void AddCheckLinkTo_ShouldRejectPeriodBelowOneSecond_WhenProvided()
        {
            var ihb = BuildBuilder();

            Assert.Throws<ArgumentException>(() => ihb.AddCheckLinkTo("Test1", "Original", period: TimeSpan.FromMilliseconds(500)));
        }

        [Fact]
        public void AddCheckLinkTo_ShouldAllowOmittedPeriod()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var ihb = services.AddHealthChecksPlus(["Adopted"]);
            ihb.Add(new HealthCheckRegistration("Original", _ => new AlwaysHealthyCheck(), null, null));

            ihb.AddCheckLinkTo("Adopted", "Original");
            // No exception thrown is the assertion.
        }
    }
}
