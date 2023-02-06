using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;

namespace HealthCheckPlus
{
    public interface IStateHealthChecksPlus
    {
        HealthCheckResult StatusApp { get; }
        HealthCheckResult StatusDep(Enum keydep);
    }
}
