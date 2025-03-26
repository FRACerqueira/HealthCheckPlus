// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

namespace HealthCheckPlus.Abstractions
{
    /// <summary>
    /// Enumerator values for the origin of the last status result.
    /// </summary>
    public enum HealthCheckTrigger
    {
        /// <summary>
        /// Initial status.
        /// </summary>
        None,

        /// <summary>
        /// From SwitchTo command.
        /// </summary>
        SwitchTo,

        /// <summary>
        /// From URL request.
        /// </summary>
        UrlRequest,

        /// <summary>
        /// From HealthCheckPlus background service.
        /// </summary>
        Background,

        /// <summary>
        /// From default .NET Core.
        /// </summary>
        Default
    }
}
