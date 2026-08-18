// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using System.Threading;

namespace HealthCheckPlus.Internal
{
    // The "at least one second" and "not infinite" rules below used to be reimplemented
    // independently at every property setter of HealthCheckPlusBackGroundOptions and every
    // period-accepting method of HealthChecksPlusExtension - nine call sites enforcing what is
    // conceptually one rule, with no compiler or test signal tying them together if one of them
    // drifted from the rest.
    internal static class PeriodValidation
    {
        private static readonly TimeSpan Minimum = TimeSpan.FromSeconds(1);

        public static void EnsureAtLeastOneSecond(TimeSpan value, string propertyName, string paramName)
        {
            if (value < Minimum)
            {
                throw new ArgumentException($"The {propertyName} must be greater than or equal to one second.", paramName);
            }
        }

        public static void EnsureNotInfinite(TimeSpan value, string propertyName, string paramName)
        {
            if (value == Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentException($"The {propertyName} must not be infinite.", paramName);
            }
        }
    }
}
