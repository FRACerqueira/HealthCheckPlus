// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using System.Collections.Concurrent;

namespace HealthCheckPlus.Internal
{
    // Per-IServiceCollection registration-time state for HealthChecksPlusExtension - keeps
    // multiple hosts built in the same process (WebApplicationFactory, .NET Aspire, parallel
    // tests) each on their own isolated state, instead of leaking the "AddHealthChecksPlus was
    // called" flag and adopted external check instances across unrelated containers.
    internal sealed class HealthChecksPlusRegistrationState
    {
        public bool AddedHealthChecksPlus { get; set; }

        // Lazy<T>, not the wrapper type directly: ConcurrentDictionary.GetOrAdd's valueFactory has
        // no once-only guarantee under contention (two threads racing to add the same key can both
        // run it), so caching a bare WrapperBaseHealthCheckPlus would let two concurrent callers
        // (e.g. an HTTP request and a background cycle both finding the check "due" for its first
        // run at the same time) each construct a real underlying check instance, with the loser
        // silently discarded and never disposed. Lazy<T>'s default thread-safety mode
        // (ExecutionAndPublication) guarantees the inner factory runs exactly once even when
        // GetOrAdd's own factory (which just constructs the Lazy<T> wrapper, a cheap no-side-effect
        // operation) runs more than once.
        public ConcurrentDictionary<string, Lazy<WrapperBaseHealthCheckPlus>> ExternalCheck { get; } = new();
    }
}
