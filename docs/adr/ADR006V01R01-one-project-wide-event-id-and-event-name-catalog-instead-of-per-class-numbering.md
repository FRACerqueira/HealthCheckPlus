<!-- Do not remove this comment, lines and table (1-12) -->
|Adr-Plus Fields|Values Migrated |
|--|--|
|File title md|One project-wide EventId and EventName catalog instead of per-class numbering|
|Version|01|
|Revision|01|
|Scope|HealthCheckPlus|
|Domain|LoggingConvention|
|Created|Proposed (2026-08-19)|
|Changed||
|Superseded||
<!-- Do not remove this comment, lines and table (1-12) -->
---
# One project-wide EventId and EventName catalog instead of per-class numbering

## Deciders

* Deciders: Fernando Cerqueira (maintainer)

Technical Story: v4.0.0 hardening cycle, fourth audit round — see `CHANGELOG.md`'s `## V4.0.0` entry: "**Breaking**: all internal log `EventId` numbers are now assigned from one project-wide catalog instead of one independent, restarting-from-100 range per class."

## Context and Problem Statement

Each of the three classes with their own logging (`CacheHealthCheckPlus`, `DefaultHealthCheckServicePlus`, `HealthCheckPlusBackGroundService`) numbered its own `EventId`s independently, each restarting from the same base value. Two of these classes share the same logger category, and their independently-numbered ids collided at least twice during this same hardening cycle — once caught by an audit round, and once (self-inflicted) reintroduced while fixing the first collision, because nothing forced a check against the *other* classes' numbers when picking a new one.

## Decision Drivers

* A numeric `EventId` collision between two semantically unrelated log entries makes filtering/alerting on a specific id ambiguous — exactly the failure this decision needs to close for good, not just patch the two known instances.
* The fix needs to be structurally impossible to violate again, not just a documented convention a future edit could still get wrong (which is exactly what happened the first time this was "fixed").
* `EventName` (not the numeric id) is what actually identifies a log entry for anyone filtering by name rather than number, so preserving those strings across the renumbering matters more than preserving the numbers.

## Considered Options

* Manually renumber the two colliding ranges and document a convention for future additions.
* Centralize every `EventId`/`EventName` across the whole project into one shared catalog class.
* Give each class its own dedicated logger category, so numeric collisions across classes become harmless.

## Decision Outcome

Chosen option: "One project-wide catalog", because a shared, single enumeration is the only option where a new id physically cannot collide with an existing one without the compiler-visible duplicate-constant conflict that a scattered, per-class scheme could never surface — it fixes the class of bug, not just the two known instances.

### Positive Consequences

* Both known collisions (and any latent ones the audit hadn't yet found) are closed by construction — a new log entry added anywhere in the project must pick an id from the one shared catalog, where a duplicate is immediately visible.
* `EventName` strings are unchanged, so any consumer filtering/alerting by name is unaffected by this change.

### Negative Consequences

* **Breaking**: most log entries' numeric `EventId` changed as part of the renumbering — a consumer filtering or alerting on a specific numeric id (rather than its `EventName`) needs to update those filters.
* All three classes' logging now depends on one shared file, a minor coupling cost compared to fully independent per-class numbering — accepted because the alternative (independent numbering) is exactly what produced the collisions this decision closes.

## Pros and Cons of the Options

### Manual renumbering + documented convention

* Good, because minimal structural change.
* Bad, because it depends entirely on every future contributor remembering and following the convention — which is precisely how the second, self-inflicted collision happened during this very cycle.

### One project-wide catalog

* Good, because a duplicate id becomes a compiler-visible conflict, not a runtime-only, easy-to-miss collision.
* Bad, because every log entry's numeric id changes at once — a real, one-time breaking cost for any consumer filtering on numbers.

### Separate logger categories per class

* Good, because collisions across classes become harmless (a duplicate id in different categories doesn't ambiguously overlap).
* Bad, because it doesn't prevent a collision *within* a category shared by two classes — which is exactly the scenario that caused both real collisions this cycle found — and adds a new configuration surface (per-class categories) consumers would need to know about.

## Links

* Related to [ADR005](./ADR005V01R01-guard-logging,-metrics,-and-background-cycle-delegates-so-a-single-failure-can-never-break-health-evaluation-or-the-background-loop.md) — both decisions harden the same logging infrastructure this library depends on for diagnosability.