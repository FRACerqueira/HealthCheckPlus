<!-- Do not remove this comment, lines and table (1-12) -->
|Adr-Plus Fields|Values Migrated |
|--|--|
|File title md|Represent a health check's multi-field result as an atomically-swapped immutable snapshot|
|Version|01|
|Revision|01|
|Scope|HealthCheckPlus|
|Domain|StateManagement|
|Created|Proposed (2026-08-19)|
|Changed|Accepted (2026-08-19)|
|Superseded||
<!-- Do not remove this comment, lines and table (1-12) -->
---
# Represent a health check's multi-field result as an atomically-swapped immutable snapshot

## Deciders

* Deciders: Fernando Cerqueira (maintainer)

Technical Story: v4.0.0 hardening cycle, M2 (baseline audit) — see `CHANGELOG.md`'s `## V4.0.0` entry and commit `17ac7bf` "Fix torn reads of a check's cached result under concurrent Update() calls" (empirically confirmed: 17.3% torn reads before the fix, 0 after, in a 500k-iteration stress test).

## Context and Problem Statement

`ItemCacheHealth`'s `LastResult`/`DateRef`/`Duration`/`Origin` were four independently-mutable fields. A reader could observe them mid-write from a concurrent `Update()` call — e.g. a new `Status` paired with the *previous* `Description` — because nothing made the four fields change together as one atomic unit. This is a genuine, empirically-confirmed data race (torn reads), not a theoretical one.

## Decision Drivers

* A health check's result is conceptually one fact (what happened, when, how long it took, what triggered it) — readers must never see a partially-updated mix of an old and a new observation.
* The fix must not require every read site to take a lock, since reads (via `IStateHealthChecksPlus`, the HTTP endpoint, `UpdateStatusName()`) are far more frequent than writes and must stay cheap.
* `IDataHealthPlus`/`ConvertToPlus` hand this data out to consumers directly — the fix has to close the tearing risk for external readers too, not just internal ones.

## Considered Options

* Take a lock around every read of these four fields, not just writes.
* Bundle the four fields into one immutable record (`CheckResultSnapshot`), swapped by a single reference assignment.
* Make each individual field volatile/`Interlocked`, without bundling them together.

## Decision Outcome

Chosen option: "Bundle into one immutable snapshot, swapped atomically", because a single reference assignment is inherently atomic in .NET — a reader either sees the fully-old snapshot or the fully-new one, never a mix, and reads need no lock at all.

### Positive Consequences

* Torn reads are eliminated by construction: empirically confirmed 0 torn reads (down from 17.3%) in a 500k-iteration stress test after the fix.
* Reads stay lock-free and cheap — the atomicity comes from the reference swap itself, not from serializing readers against writers.
* `IDataHealthPlus`/`ConvertToPlus` (external-facing) now hand out frozen `DataHealthPlusSnapshot` copies instead of live, still-mutating cache objects — closing the same tearing risk for consumers reading through the public interface.

### Negative Consequences

* Every write now allocates a new snapshot object instead of mutating four fields in place — a small, deliberate allocation cost accepted in exchange for eliminating the lock-free read tearing.
* Consumers holding onto an old `IDataHealthPlus` snapshot will not see subsequent updates through that same reference — expected given immutability, but a behavior change from the previous live-object semantics.

## Pros and Cons of the Options

### Lock around every read

* Good, because straightforward to reason about — a lock trivially prevents tearing.
* Bad, because it puts every read (far more frequent than writes) behind contention with writers, working against a cache whose entire point is being cheap to read.

### Immutable snapshot, atomic swap

* Good, because reads stay lock-free while tearing is eliminated by construction.
* Bad, because it costs one allocation per write instead of four in-place field mutations.

### Individually volatile/`Interlocked` fields

* Good, because each individual field's own read/write becomes safe.
* Bad, because it does nothing for the actual problem — the four fields updating *together* as one fact — a reader could still see three fields already updated and the fourth still stale, just with each individual field no longer torn internally.

## Links

* Related to [ADR003](./ADR003V01R01-scope-registration-time-and-cached-health-check-state-per-host-instead-of-process-wide.md) — both decisions harden the same cached-state model this library's read paths depend on.
