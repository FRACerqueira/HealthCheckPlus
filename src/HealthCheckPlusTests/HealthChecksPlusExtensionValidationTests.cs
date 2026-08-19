// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Abstractions;
using HealthCheckPlus.Internal;
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
            return services.AddHealthChecksPlus();
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

        // Regression tests: a null/empty namedep (or, for AddCheckLinkTo, name) used to be accepted
        // here with no validation at all - AddUnhealthyPolicy/AddDegradedPolicy stored it as-is and
        // only failed later, with a confusing NullReferenceException from
        // DefaultHealthCheckServicePlus's constructor (ValidatePolicyUniqueness/the policy index both
        // call .ToUpperInvariant() on it) far away from the actual mistake; AddCheckLinkTo failed
        // immediately with a NullReferenceException from namedep.Equals(name, ...) instead of a
        // clear ArgumentException naming the actual bad argument.
        [Fact]
        public void AddUnhealthyPolicy_ShouldRejectNullOrEmptyNamedep()
        {
            var ihb = BuildBuilder();

            Assert.ThrowsAny<ArgumentException>(() => ihb.AddUnhealthyPolicy(null!, TimeSpan.FromSeconds(1)));
            Assert.Throws<ArgumentException>(() => ihb.AddUnhealthyPolicy("", TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public void AddDegradedPolicy_ShouldRejectNullOrEmptyNamedep()
        {
            var ihb = BuildBuilder();

            Assert.ThrowsAny<ArgumentException>(() => ihb.AddDegradedPolicy(null!, TimeSpan.FromSeconds(1)));
            Assert.Throws<ArgumentException>(() => ihb.AddDegradedPolicy("", TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public void AddCheckPlus_ShouldRejectNullOrEmptyNamedep()
        {
            var ihb = BuildBuilder();

            Assert.ThrowsAny<ArgumentException>(() => ihb.AddCheckPlus<AlwaysHealthyCheck>(null!));
            Assert.Throws<ArgumentException>(() => ihb.AddCheckPlus<AlwaysHealthyCheck>(""));
        }

        [Fact]
        public void AddCheckLinkTo_ShouldRejectNullOrEmptyNamedepOrName()
        {
            var ihb = BuildBuilder();

            Assert.ThrowsAny<ArgumentException>(() => ihb.AddCheckLinkTo(null!, "Original"));
            Assert.Throws<ArgumentException>(() => ihb.AddCheckLinkTo("", "Original"));
            Assert.ThrowsAny<ArgumentException>(() => ihb.AddCheckLinkTo("Adopted", null!));
            Assert.Throws<ArgumentException>(() => ihb.AddCheckLinkTo("Adopted", ""));
        }

        // Regression test for the breaking change that removed AddHealthChecksPlus's `names`
        // parameter: the cache must now be seeded purely from whatever ends up registered via
        // AddCheckPlus/AddCheckLinkTo/native AddCheck, with no separate list to keep in sync. This
        // seeding happens lazily, inside the IStateHealthChecksPlus factory, the first time it's
        // resolved - by which point every registration made anywhere during startup already
        // exists in IOptions<HealthCheckServiceOptions>.Value.Registrations.
        [Fact]
        public void AddHealthChecksPlus_ShouldSeedCacheFromActualRegistrations_WithNoNamesListRequired()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var ihb = services.AddHealthChecksPlus();
            ihb.AddCheckPlus<AlwaysHealthyCheck>("Test1");
            ihb.AddCheckPlus<AlwaysHealthyCheck>("Test2");

            using var provider = services.BuildServiceProvider();
            var state = (CacheHealthCheckPlus)provider.GetRequiredService<IStateHealthChecksPlus>();

            Assert.Equal(HealthStatus.Healthy, state.FullStatus("Test1").LastResult.Status);
            Assert.Equal(HealthStatus.Healthy, state.FullStatus("Test2").LastResult.Status);
        }

        // Regression test: AddHostedService accumulates rather than replaces, so a second
        // AddBackgroundPolicy call used to silently register a second HealthCheckPlusBackGroundService
        // instance - two independent background loops running concurrently, each executing checks and
        // dispatching publishers on its own schedule, with nothing indicating the mistake.
        [Fact]
        public void AddBackgroundPolicy_ShouldRejectASecondCall()
        {
            var ihb = BuildBuilder();
            ihb.AddBackgroundPolicy();

            Assert.Throws<InvalidOperationException>(() => ihb.AddBackgroundPolicy());
        }

        [Fact]
        public void AddCheckLinkTo_ShouldAllowOmittedPeriod()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var ihb = services.AddHealthChecksPlus();
            ihb.Add(new HealthCheckRegistration("Original", _ => new AlwaysHealthyCheck(), null, null));

            ihb.AddCheckLinkTo("Adopted", "Original");
            // No exception thrown is the assertion.
        }
    }
}
