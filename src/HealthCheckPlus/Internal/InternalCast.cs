// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

namespace HealthCheckPlus.Internal
{
    // Every "Plus" component that consumes the native HealthCheckService/IStateHealthChecksPlus
    // abstractions actually expects the concrete type AddHealthChecksPlus() registers underneath
    // them (DefaultHealthCheckServicePlus/CacheHealthCheckPlus respectively). A raw C# cast
    // failing at one of those call sites means something replaced or decorated that registration
    // after AddHealthChecksPlus() ran, or the service was resolved from a container where
    // AddHealthChecksPlus() was never called at all - a configuration error, not something any of
    // those call sites can recover from. A bare `(T)value` cast surfaces that as a generic
    // InvalidCastException naming only the two type names involved; this gives the same failure a
    // message that also names what registration is actually at fault and why.
    internal static class InternalCast
    {
        public static T To<T>(object value, string expectedRegistrationDescription) where T : class
        {
            if (value is T typed)
            {
                return typed;
            }

            // `value is T typed` is false for a null value too (a pattern match never matches
            // null), so this branch must not assume value is non-null just because the cast
            // failed - value.GetType() below would otherwise throw an unrelated
            // NullReferenceException instead of the clear message this method exists to give.
            // Currently unreachable from any of this class's own call sites (all five pass a
            // GetRequiredService<T>() result, which throws its own exception rather than
            // returning null), but a defensive check like this one should never depend on every
            // future caller upholding that.
            var actualTypeDescription = value?.GetType().FullName ?? "null";
            throw new InvalidOperationException(
                $"Expected {expectedRegistrationDescription} to be an instance of {typeof(T).Name}, but it was " +
                $"{actualTypeDescription}. This usually means something replaced or decorated that registration " +
                "after AddHealthChecksPlus() ran, or it was resolved from a service provider where " +
                "AddHealthChecksPlus() was never called.");
        }
    }
}
