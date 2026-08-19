<!-- Do not remove this comment, lines and table (1-12) -->
|Adr-Plus Fields|Values Migrated |
|--|--|
|File title md|Fail fast at startup on registration and configuration inconsistencies|
|Version|01|
|Revision|01|
|Scope|HealthCheckPlus|
|Domain|Validation|
|Created|Proposed (2026-08-19)|
|Changed||
|Superseded||
<!-- Do not remove this comment, lines and table (1-12) -->
---
# Fail fast at startup on registration and configuration inconsistencies

## Deciders

* Deciders: Fernando Cerqueira (maintainer)

Technical Story: v4.0.0 hardening cycle, original audit + eighth/ninth audit rounds — see `CHANGELOG.md`'s `## V4.0.0` entries for `ValidateHealthyPolicies`, `ValidatePolicyTargets`, `AddBackgroundPolicy`'s double-call guard, and the native-publisher-resurrection guard.

## Context and Problem Statement

Several misconfigurations were previously only discoverable at runtime, or not at all: a health check registered with no Healthy policy caused a `NullReferenceException` the first time `/health` was hit; a policy naming a check that was never registered (a typo) silently registered a policy that could never match anything; calling `AddBackgroundPolicy` twice silently started two independent background loops; and the native `HealthCheckPublisherHostedService` reappearing after `AddBackgroundPolicy` removed it (via a later `AddHealthChecks()` call) silently duplicated or bypassed publisher dispatch. Each of these is a configuration mistake that a consumer would only discover much later, if at all, and usually in production.

## Decision Drivers

* A health-check library's core purpose is telling the truth about system health; a misconfiguration that degrades or silently misbehaves at runtime undermines that purpose directly.
* A startup-time error naming the exact problem is far cheaper to act on than a runtime failure discovered under production traffic.
* The library has no compatibility commitment to preserve across this major version (v4.0.0), so a breaking fail-fast change is an acceptable cost where it closes a real correctness gap.

## Considered Options

* Fail fast at startup with a clear, specific exception naming the problem.
* Fall back to a sensible default and log a warning, without failing the host.
* Leave the behavior as-is and document the pitfall.

## Decision Outcome

Chosen option: "Fail fast at startup", because a library whose entire purpose is reporting on system health should not itself degrade silently when misconfigured — an explicit, immediate startup failure is strictly more honest and more actionable than a delayed or masked one.

### Positive Consequences

* Every one of the four scenarios above now produces a clear `InvalidOperationException`/`ArgumentException` at startup naming the check/policy/registration at fault, instead of a confusing runtime symptom.
* The pattern is now a consistent, recognizable stance across the library (see `docs/POINTS_OF_ATTENTION.md`'s "Setting it up" section) rather than four independent, differently-behaved special cases.

### Negative Consequences

* **Breaking for prior versions on every one of these four points**: an app that happened to work despite one of these misconfigurations (e.g. calling `AddHealthChecks()` again after `AddBackgroundPolicy()` with a publisher registered) will now refuse to start until fixed.
* A resurrected native publisher service with **no** publisher registered is deliberately treated as harmless and does *not* fail fast — a narrower carve-out that itself required judgment about where the line between "real risk" and "false positive" sits.

## Pros and Cons of the Options

### Fail fast at startup

* Good, because the problem surfaces immediately, with a specific, actionable message.
* Bad, because it's breaking for any consumer whose app happened to run despite the misconfiguration.

### Default + warning log

* Good, because non-breaking — existing misconfigured apps keep running.
* Bad, because it perpetuates exactly the silent-degradation failure mode this decision exists to close; a warning log is easy to miss in production.

### Document only

* Good, because zero implementation cost, no breaking change.
* Bad, because it does nothing for a consumer who hasn't read the docs — which is precisely how each of these four problems went unnoticed in prior versions.

## Links

* Related to [ADR002](./ADR002V01R01-derive-the-tracked-health-check-set-from-di-registrations-instead-of-a-caller-supplied-list.md) — both treat the real, current DI registrations as the one authoritative source of truth to validate against.