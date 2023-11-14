// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using System;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace HealthCheckPlus.Internal
{
    internal sealed class HealthCheckPlusOptions
    {
        public string NameCheckApp { get; set; }
        public HealthStatus StatusFail { get; set; }
        public Func<IStateHealthChecksPlus, HealthStatus> FuncStatusFail { get; set; }
        public string? CategoryForLogger { get; set; }
        public Action<ILogger, DataResutStatus> ActionForLog { get; set; }

    }
}
