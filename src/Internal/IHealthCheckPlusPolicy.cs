// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using System;

namespace HealthCheckPlus.Internal
{
    internal interface IHealthCheckPlusPolicy
    {
        TimeSpan PolicyTime { get; }
        string PolicyNameDep { get; }
    }
}
