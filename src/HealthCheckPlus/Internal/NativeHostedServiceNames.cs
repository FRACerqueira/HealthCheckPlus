// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

namespace HealthCheckPlus.Internal
{
    // Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckPublisherHostedService is internal
    // sealed with no distinguishing public marker, so matching it (to remove it in
    // HealthChecksPlusExtension.AddBackgroundPolicy, and to detect it coming back in
    // HealthCheckPlusBackGroundService.StartAsync) has no supported contract to depend on beyond
    // its fully qualified type name. One shared constant instead of the same string literal
    // duplicated at both call sites - if a future .NET version renames or moves that type, both
    // the removal and the fail-fast that depends on it stop matching simultaneously, in exactly
    // the same way, rather than drifting apart from two independent copies.
    internal static class NativeHostedServiceNames
    {
        public const string HealthCheckPublisherHostedService = "Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckPublisherHostedService";
    }
}
