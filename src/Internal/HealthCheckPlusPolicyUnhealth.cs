// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using System;

namespace HealthCheckPlus.Internal
{
    internal class HealthCheckPlusUnhealthyPolicy : IHealthCheckPlusUnhealthyPolicy
    {
        public HealthCheckPlusUnhealthyPolicy(TimeSpan policyTime, string policyNameDep)
        {
            PolicyTime = policyTime;
            PolicyNameDep = policyNameDep;
        }

        public TimeSpan PolicyTime { get; }
        public string PolicyNameDep { get; }
    }
}
