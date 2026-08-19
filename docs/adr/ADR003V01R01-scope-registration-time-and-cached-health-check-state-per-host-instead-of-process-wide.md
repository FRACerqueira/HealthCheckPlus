<!-- Do not remove this comment, lines and table (1-12) -->
|Adr-Plus Fields|Values Migrated |
|--|--|
|File title md|Scope registration-time and cached health check state per host instead of process-wide|
|Version|01|
|Revision|01|
|Scope|HealthCheckPlus|
|Domain|StateManagement|
|Created|Proposed (2026-08-19)|
|Changed|Accepted (2026-08-19)|
|Superseded||
<!-- Do not remove this comment, lines and table (1-12) -->
---
# Scope registration-time and cached health check state per host instead of process-wide

## Deciders

* Deciders: Fernando Cerqueira (maintainer)

Technical Story: v4.0.0 hardening cycle, baseline audit High finding — see `CHANGELOG.md`'s `## V4.0.0` entry: "Health check state is now isolated per host, so multiple hosts running in the same process (integration tests, .NET Aspire) no longer share cached results."

## Context and Problem Statement

Registration-time flags and the cache of last-known check results were held in `static` fields, shared by every `IHealthChecksBuilder`/`IServiceCollection` in the process. Any scenario building more than one host in the same process — `WebApplicationFactory`-style integration tests, .NET Aspire, parallel test runs — had its health-check state leak across hosts: a check registered in one host, or its cached status, could be visible to (and corrupted by) a completely unrelated host in the same process.

## Decision Drivers

* Multiple hosts per process is a normal, supported .NET pattern (integration tests, Aspire) that this library has to work correctly under, not just tolerate.
* State that's conceptually "per application instance" should be owned by that instance's own DI container, not by static process memory.
* The fix needs to compose with the rest of the library's design (per-host `IServiceCollection`, `AddCheckLinkTo`'s adopted-check cache) without introducing a new global coordination point.

## Considered Options

* Keep static fields, document the multi-host limitation.
* Move state into a new type (`HealthChecksPlusRegistrationState`) registered as an instance per `IServiceCollection`, resolved instead of read from statics.
* Key every static field by a process-unique host identifier (a dictionary keyed by something like an `IServiceCollection` hash).

## Decision Outcome

Chosen option: "Move state into a per-`IServiceCollection` instance", because it's the idiomatic .NET DI answer to "this state belongs to one application instance" — it needs no artificial keying scheme and gets host isolation for free from the container itself.

### Positive Consequences

* Multiple hosts in the same process are now fully isolated: a check registered in one can never leak its cached status, or its adopted external check instance, into another — verified with a real two-host-in-one-process integration test.
* No new global accessor or coordination point was introduced; each host resolves its own state from its own container.

### Negative Consequences

* Code that needs to read this state (a custom middleware, a background job) must resolve `IStateHealthChecksPlus` from that same host's DI container — there is no global accessor, which is a deliberate trade-off (see `docs/architecture/state.md`), not an oversight.
* `HealthChecksPlusRegistrationState` is registered as a ready-made instance, which .NET DI does not auto-dispose — the adopted-check-instance disposal responsibility had to be re-homed onto `DefaultHealthCheckServicePlus` (a container-managed singleton) instead, a secondary consequence of this choice.

## Pros and Cons of the Options

### Keep static fields, document the limitation

* Good, because zero implementation cost.
* Bad, because it leaves a real, silent correctness bug in a normal, supported .NET usage pattern (integration tests, Aspire) — not a theoretical edge case.

### Per-`IServiceCollection` instance state

* Good, because host isolation falls out of the DI container's own scoping rules, with no new coordination mechanism.
* Bad, because instance-registered state isn't auto-disposed by the container, requiring a deliberate secondary decision about who disposes what.

### Static fields keyed by a host identifier

* Good, because it avoids touching the registration/resolution call sites as much.
* Bad, because it reinvents what DI scoping already provides, and needs its own cleanup logic for hosts that are torn down (a leak risk in exactly the parallel-test-run scenario this decision is meant to fix).

## Links

* Related to [ADR002](./ADR002V01R01-derive-the-tracked-health-check-set-from-di-registrations-instead-of-a-caller-supplied-list.md) — both decisions treat the DI container as the authoritative source for what belongs to a given host.
