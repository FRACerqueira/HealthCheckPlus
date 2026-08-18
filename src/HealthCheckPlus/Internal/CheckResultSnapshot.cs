// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlus.Internal
{
    // LastResult/DateRef/Duration/Origin always describe one execution attempt together and
    // must be read/written as a single unit. They used to be four independent mutable
    // properties on ItemCacheHealth, written one assignment at a time with no synchronization -
    // a concurrent reader landing between two of those writes could observe an inconsistent mix
    // (e.g. the new Status paired with the old Description, or a Duration that doesn't match
    // either). Bundling them into one immutable record, swapped with a single reference
    // assignment, eliminates that: a .NET reference read/write is never torn, so any reader sees
    // either the fully-old or the fully-new snapshot, never a mix - no lock required on the read
    // side. The write side needs no lock either: CacheHealthCheckPlus's Running flag (set by
    // TryBeginRun/SwithState) already guarantees only one execution owns a given check between
    // its start and the matching ItemCacheHealth.SetResult call, so writers for the same key
    // never race each other.
    internal sealed record CheckResultSnapshot(HealthCheckResult LastResult, DateTime DateRef, TimeSpan Duration, HealthCheckTrigger Origin);
}
