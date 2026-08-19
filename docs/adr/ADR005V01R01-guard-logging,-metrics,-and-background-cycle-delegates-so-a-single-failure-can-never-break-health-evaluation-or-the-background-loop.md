<!-- Do not remove this comment, lines and table (1-12) -->
|Adr-Plus Fields|Values Migrated |
|--|--|
|File title md|Guard logging, metrics, and background-cycle delegates so a single failure can never break health evaluation or the background loop|
|Version|01|
|Revision|01|
|Scope|HealthCheckPlus|
|Domain|Resilience|
|Created|Proposed (2026-08-19)|
|Changed|Accepted (2026-08-19)|
|Superseded||
<!-- Do not remove this comment, lines and table (1-12) -->
---
# Guard logging, metrics, and background-cycle delegates so a single failure can never break health evaluation or the background loop

## Deciders

* Deciders: Fernando Cerqueira (maintainer)

Technical Story: v4.0.0 hardening cycle, multiple audit rounds — see `docs/architecture/logging.md` and `docs/architecture/publishing.md` for the full catalog this decision produced, and `CHANGELOG.md`'s `## V4.0.0` entries for `SafeLog`, the background-loop publisher/predicate try/catch guards, and the `HealthCheckPlusBackGroundLoopFaulted` signal.

## Context and Problem Statement

Two independent classes of infrastructure that consumers plug into this library — logging sinks/metrics listeners on one side, and consumer-supplied delegates (`IHealthCheckPublisher`, `PublisherCondition`, `Predicate`) on the other — could each break things this library must never let break: a throwing `ILogger` sink or `Meter`/`MeterListener` could kill the background loop, break `Dispose`/`DisposeAsync`/`StopAsync`, turn a successful check into a 500 on `/health`, or mask a real exception with its own; a single throwing publisher, `PublisherCondition`, or `Predicate` used to be able to kill the entire background loop (checks *and* publishing) for the rest of the process's life, with no crash and no log. Both are the same underlying problem: code this library doesn't control must never be able to bring down code it does.

## Decision Drivers

* Health evaluation and the background polling/publishing loop are the library's core purpose — neither may ever be sacrificed to a failure in a secondary concern (observability) or in consumer-supplied extension code.
* A defensive `try/catch` that swallows an exception with zero log and zero metric is treated as a defect in this codebase, not a stylistic choice (see `CONTRIBUTING.md`) — every guard needs its own signal, with one deliberate exception (shutdown-time `OperationCanceledException`).
* The guard for a logging failure specifically cannot itself log the failure (circular), so it needs its own channel (a metric) to stay observable without risking the same failure again.

## Considered Options

* Wrap every logging call, metrics call, and consumer-delegate invocation site in its own guard, with its own signal.
* Wrap only the outermost entry points (e.g. the background loop's top-level catch) and let inner failures propagate up to that one guard.
* Leave these call sites unguarded and document the risk instead.

## Decision Outcome

Chosen option: "Guard every call site individually, with its own signal", because a single outer guard can't distinguish which check, which publisher, or which logging call actually failed — per-site guards preserve both the isolation (one failure doesn't take down unrelated work) and the diagnosability (the specific failure is still visible).

### Positive Consequences

* A broken third-party `ILoggerProvider` or `MeterListener` can no longer kill the background loop, break `Dispose`/`DisposeAsync`/`StopAsync`, or turn a successful check into a 500 — it now only produces a `logging_sink_failed`/metrics-recording-error signal.
* A single publisher, `PublisherCondition`, or `Predicate` that throws is now isolated to its own cycle/dispatch instead of permanently killing the whole background loop — verified with dedicated regression tests for each guarded site.
* `HealthCheckPlusBackGroundLoopFaulted` (Critical) now exists as the one signal that distinguishes "the loop ran and then died" from "the loop never started," closing a prior silent-disappearance failure mode.

### Negative Consequences

* One residual, explicitly accepted gap remains: if the `ILogger` sink **and** the `Meter`/`MeterListener` fail at the exact same moment, that combined failure can still propagate out of `Dispose`/`DisposeAsync`/`StopAsync` — kept as a documented limitation rather than adding a third channel (e.g. `Console.Error`), a deliberate decision not to keep adding guard layers indefinitely.
* One call is a knowing, permanent exception: `RunCheckAsync`'s `_logger.BeginScope(...)` returns an `IDisposable` inside a `using` statement, which can't be wrapped in a guarded lambda the way a plain `void` log call can — a throwing `BeginScope` still surfaces as a real exception on that one check's execution.

## Pros and Cons of the Options

### Per-site guards with their own signal

* Good, because failures stay isolated to the specific site that broke, and stay observable via a dedicated log/metric.
* Bad, because it's more code to write and maintain than a single outer guard — this is a real, accepted maintenance cost (28 call sites guarded in one audit round alone).

### One outer guard at the top level

* Good, because far less code to write.
* Bad, because it can't tell you *which* check, publisher, or logging call actually failed, and a failure deep in one item's processing can still abort everything else in the same batch/cycle before the outer guard catches it.

### Document, don't guard

* Good, because zero implementation cost.
* Bad, because it does nothing to actually prevent the failure mode — a broken third-party sink or a misbehaving publisher would still take down the whole loop in production, documentation notwithstanding.

## Links

* Related to [ADR001](./ADR001V01R01-native-system.-diagnostics.-metrics-instrumentation-for-check-and-publisher-telemetry.md) — the metrics side of this same guarding doctrine (`SafeRecordMetric`).
