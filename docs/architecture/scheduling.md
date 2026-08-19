# Scheduling and the `Running` flag

[← Back to Architecture overview](../ARCHITECTURE.md)

`ScheduleIfDue` decides, for one check, whether enough time has passed since its last run (`DateRef` + the resolved policy's Delay/Period, compared to `DateTime.UtcNow`) and — if so — marks it `Running` so a second concurrent caller doesn't also schedule it.

The due-check and the Running-flag write used to be two separate, unsynchronized steps. Two callers evaluating the same check at nearly the same instant (an HTTP request and a background cycle, or two concurrent HTTP requests) could both observe "not running, due" before either one had a chance to mark it, both schedule the same check, and run it concurrently — with whichever finished last having its result silently discarded by `CacheHealthCheckPlus.Update` (which requires `Running == true` to accept a result).

**Point of attention**: `BuildDueRegistrations` resolves a policy and evaluates `ScheduleIfDue` for *every* registration on *every* cycle/request, even when nothing is actually due to run - the per-cycle cost is proportional to the number of registrations regardless of how many (if any) are due. `FindPolicy` itself is O(1) (a `Dictionary<(NormalizedName, HealthStatus), HealthCheckPlusPolicyStatus>`, `_policyIndex`, built once in the constructor after `ValidatePolicyUniqueness` guarantees no key collisions - it used to be a linear scan through every policy, making the per-cycle cost proportional to registrations × policies, effectively quadratic since policy count grows with registration count). The remaining O(registrations) baseline - touching every registration once per cycle even when none are due - is a deliberate simplicity/performance tradeoff, not an oversight, and modest at the scale this library documents (dozens of checks, not thousands - see [`POINTS_OF_ATTENTION.md`](../POINTS_OF_ATTENTION.md#setting-it-up)); eliminating it would need a fundamentally different scheduler (e.g. a min-heap keyed by next-due-time) so only registrations actually near due are touched, which isn't justified at that scale.

`CacheHealthCheckPlus.TryBeginRun(key, isDue)` closes this by making the check and the mark one atomic operation, guarded by a lock:

```csharp
public bool TryBeginRun(string key, Func<ItemCacheHealth, bool> isDue)
{
    lock (_lock)
    {
        if (!_statusDeps.TryGetValue(key, out var item) || item.Running || !isDue(item))
        {
            return false;
        }
        item.Running = true;
        return true;
    }
}
```

`isDue` is evaluated *inside* the lock, against the live cache item, so there is no window between "check" and "mark" for another caller to slip through. This is a coarse-grained (single, class-wide) lock rather than one lock per check — deliberately, since a per-key lock table would add complexity without a measurable benefit at the scale this library documents (dozens of checks, not thousands). **Point of attention**: this lock's scope grew past scheduling decisions alone - `Update()` also takes it to publish a result (see [State](./state.md)), and `UpdateStatusName()`/`Status(name)`'s lazy path take it on every HTTP request, and on every background cycle that actually has a due registration to run, to capture a consistent `(version, report)` pair. No consumer-supplied code (a check's own execution, a `StatusHealthReport`/`Predicate` delegate) ever runs while it's held, so correctness isn't at risk, but it's no longer accurate to describe every acquisition of this lock as "infrequent" or "not a hot path".

**Point of attention**: `UpdateStatusName()` builds one `HealthReport` per registered name inside this lock - O(names × checks) rather than the single shared report every name read before `includeName` existed. A tempting fix (share one built report across every name whose `IncludeName` is `null`, since they'd read identical content) was tried and reverted: `HealthReport.Entries` is typed `IReadOnlyDictionary<...>` but is, at runtime, the exact same mutable `Dictionary<...>` its constructor was handed (confirmed empirically - no defensive copy), so sharing one instance across names would let a single misbehaving `StatusHealthReport` delegate (one that downcasts and writes to it, even by accident) silently corrupt every other unfiltered name sharing that instance - exactly the cross-aggregate leak `includeName`'s own isolation exists to prevent. The accepted cost instead is one full report build per name, on the same "dozens of checks, not thousands" scale already accepted for `BuildDueRegistrations`' own O(registrations) baseline above - the number of distinct registered names is itself typically small (one per named aggregate a consumer actually registers), so this is a small constant multiplied onto an already-modest base cost, not a new order of magnitude.

**Point of attention**: once `TryBeginRun` marks a check `Running`, only `Update`/`ReleaseRunning` (called from `ApplyBatchResults`, or from the guards described below) can clear it again - so every step between the two, for the whole batch marked in one `BuildDueRegistrations` call, must guarantee that release happens even on failure, or the check stays `Running` forever (excluded from every future schedule, with no recovery short of a process restart). Each stage guards its own failure modes: `CheckHealthPlusAsync` wraps its `Log.HealthCheckProcessingBegin` call and `StartBatch` in its own try/catch (a broken `ILogger` sink used to throw from completely outside any release path); `BackGroudCheckHealthPlusAsync` wraps `StartBatch` the same way, but has no logging call to guard there. `BuildDueRegistrations`'s own loop has the same guard for a registration marked `Running` earlier in the same loop, in case a later one's policy resolution throws. `ApplyBatchResults` itself guards per item: classifying a task, logging an aborted check, and updating the cache all run inside a per-iteration try/catch, so one item's failure (e.g. a throwing logger on the `AmbientCancellation` path) still releases that item and lets the loop continue to the rest of the batch instead of leaving every later item stuck `Running`; any exceptions caught this way are collected and rethrown together as an `AggregateException` once the whole batch has been finalized.

All scheduling comparisons use `DateTime.UtcNow`, never local time — **point of attention**: a host running with a non-UTC system clock (or one that observes a DST transition) must not see check periods drift or double-fire; comparing against UTC consistently avoids that class of bug entirely.
