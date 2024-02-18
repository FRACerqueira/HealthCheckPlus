// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

namespace HealthCheckPlus
{
    /// <summary>
    /// Enumerator values for origin of last status result.
    /// </summary>
    public enum HealthCheckTrigger

    {
        /// <summary>
        /// Initial status
        /// </summary>
        None,
        /// <summary>
        /// From SwithTo command
        /// </summary>
        SwithTo,
        /// <summary>
        /// From url request
        /// </summary>
        UrlRequest,
        /// <summary>
        /// From HealthCheckPlus background service
        /// </summary>
        BackGround,
        /// <summary>
        /// From default .net core
        /// </summary>
        Default
    }
}
