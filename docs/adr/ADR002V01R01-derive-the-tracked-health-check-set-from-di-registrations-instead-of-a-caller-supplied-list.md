<!-- Do not remove this comment, lines and table (1-12) -->
|Adr-Plus Fields|Values Migrated |
|--|--|
|File title md|Derive the tracked health check set from DI registrations instead of a caller-supplied list|
|Version|01|
|Revision|01|
|Scope|HealthCheckPlus|
|Domain|PublicApi|
|Created|Proposed (2026-08-19)|
|Changed||
|Superseded||
<!-- Do not remove this comment, lines and table (1-12) -->
---
# Derive the tracked health check set from DI registrations instead of a caller-supplied list

## Deciders

* Deciders: Fernando Cerqueira (maintainer)

Technical Story: v4.0.0 hardening cycle — see `CHANGELOG.md`'s `## V4.0.0` entry: "**Breaking**: `AddHealthChecksPlus()` no longer takes a `names` parameter."

## Context and Problem Statement

`AddHealthChecksPlus(names)` required the caller to pass the list of check names the library should track, separately from the actual `AddCheckPlus`/`AddCheckLinkTo`/native `AddCheck` registrations that create those checks. Nothing enforced that the two stayed in sync — a check added without updating `names`, a typo, or a stale entry left behind after removing a check, would all silently drift the tracked set away from what was actually registered, with no error until the mismatch caused a downstream failure.

## Decision Drivers

* A separately-maintained list is an entire class of drift bug (list vs. real registrations) that has no reason to exist once ASP.NET Core's own `HealthCheckServiceOptions.Registrations` is available as the single source of truth.
* Consumers should not have to keep two things in sync that describe the same underlying fact (which checks exist).
* The library already fails fast at startup for missing Healthy policies (see `ADR004`); deriving the tracked set from registrations keeps that same "the registration list is authoritative" stance consistent.

## Considered Options

* Keep the `names` parameter (status quo).
* Derive the tracked set directly from `IOptions<HealthCheckServiceOptions>.Value.Registrations` at first resolution.
* Keep `names` but add a startup validation that cross-checks it against the real registrations.

## Decision Outcome

Chosen option: "Derive the tracked set directly from `IOptions<HealthCheckServiceOptions>.Value.Registrations`", because it eliminates the drift class entirely rather than just detecting it — there is no longer a second list that could ever disagree with the real registrations.

### Positive Consequences

* Impossible for the tracked-check set to diverge from what's actually registered — there is only one list now.
* One fewer parameter for every consumer to pass and keep updated as checks are added or removed.

### Negative Consequences

* **Breaking change**: any consumer calling `AddHealthChecksPlus(names)` on 3.0.1 must update the call site to `AddHealthChecksPlus()` when upgrading.
* A check registered only through a native `IHealthChecksBuilder` extension, with no `AddCheckPlus`/`AddCheckLinkTo` Healthy policy of its own, still fails fast at startup (see `ADR004`) — this decision is about *which checks get tracked*, not a way to skip that separate requirement.

## Pros and Cons of the Options

### Keep the `names` parameter

* Good, because no breaking change for existing 3.0.1 consumers.
* Bad, because the drift risk this decision exists to close remains open indefinitely.

### Derive from `Registrations` directly

* Good, because the drift class is eliminated by construction, not just detected.
* Bad, because it's a breaking signature change every 3.0.1 consumer must adopt.

### Keep `names`, add startup validation

* Good, because no breaking change to the method signature.
* Bad, because it only turns silent drift into a startup error — the redundant list, and the maintenance burden of keeping it in sync, still exists.

## Links

* Related to [ADR004](./ADR004V01R01-fail-fast-at-startup-on-registration-and-configuration-inconsistencies.md) — both treat the real DI registrations as the single source of truth, enforced at startup.