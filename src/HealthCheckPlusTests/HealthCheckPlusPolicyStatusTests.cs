// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Internal.Policies;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlusTests
{
    public class HealthCheckPlusPolicyStatusTests
    {
        [Fact]
        public void HealthCheckPlusPolicyStatus_Should_Set_Properties_Correctly()
        {
            // Arrange
            var expectedStatus = HealthStatus.Healthy;
            var expectedDelay = TimeSpan.FromSeconds(30);
            var expectedPeriod = TimeSpan.FromMinutes(5);
            var expectedNameDep = "TestPolicy";

            // Act
            var policyStatus = new HealthCheckPlusPolicyStatus(
                expectedStatus,
                expectedDelay,
                expectedPeriod,
                expectedNameDep
            );

            // Assert
            Assert.Equal(expectedStatus, policyStatus.PolicyForStatus);
            Assert.Equal(expectedDelay, policyStatus.PolicyDelay);
            Assert.Equal(expectedPeriod, policyStatus.PolicyPeriod);
            Assert.Equal(expectedNameDep, policyStatus.PolicyNameDep);
        }

        [Fact]
        public void HealthCheckPlusPolicyStatus_Should_Allow_Null_Delay_And_Period()
        {
            // Arrange
            var expectedStatus = HealthStatus.Degraded;
            TimeSpan? expectedDelay = null;
            TimeSpan? expectedPeriod = null;
            var expectedNameDep = "TestPolicyWithNulls";

            // Act
            var policyStatus = new HealthCheckPlusPolicyStatus(
                expectedStatus,
                expectedDelay,
                expectedPeriod,
                expectedNameDep
            );

            // Assert
            Assert.Equal(expectedStatus, policyStatus.PolicyForStatus);
            Assert.Null(policyStatus.PolicyDelay);
            Assert.Null(policyStatus.PolicyPeriod);
            Assert.Equal(expectedNameDep, policyStatus.PolicyNameDep);
        }
    }
}
