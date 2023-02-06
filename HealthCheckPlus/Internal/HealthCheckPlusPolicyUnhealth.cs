using System;

namespace HealthCheckPlus.Internal
{
    internal interface IHealthCheckPlusPolicyUnhealth
    {
        TimeSpan? PolicyTime { get; }
        string PolicyNameDep { get; }
    }

    internal class HealthCheckPlusPolicyUnhealth: IHealthCheckPlusPolicyUnhealth
    {
        public HealthCheckPlusPolicyUnhealth(TimeSpan? policyTime, string policyNameDep)
        {
            PolicyTime= policyTime;
            PolicyNameDep= policyNameDep;
        }

        public TimeSpan? PolicyTime { get; }
        public string PolicyNameDep { get; }
    }
}
