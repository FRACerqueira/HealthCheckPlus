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

            throw new InvalidOperationException(
                $"Expected {expectedRegistrationDescription} to be an instance of {typeof(T).Name}, but it was " +
                $"{value.GetType().FullName}. This usually means something replaced or decorated that registration " +
                "after AddHealthChecksPlus() ran, or it was resolved from a service provider where " +
                "AddHealthChecksPlus() was never called.");
        }
    }
}
