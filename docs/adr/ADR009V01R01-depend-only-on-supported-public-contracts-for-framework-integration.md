<!-- Do not remove this comment, lines and table (1-12) -->
|Adr-Plus Fields|Values Migrated |
|--|--|
|File title md|Depend only on supported public contracts for framework integration|
|Version|01|
|Revision|01|
|Scope|HealthCheckPlus|
|Domain|IntegrationBoundary|
|Created|Proposed (2026-08-19)|
|Changed||
|Superseded||
<!-- Do not remove this comment, lines and table (1-12) -->
---
# Depend only on supported public contracts for framework integration

## Deciders

* Deciders: Fernando Cerqueira (maintainer)

Technical Story: v4.0.0 hardening cycle, original audit (`AddCheckLinkTo` rewrite) + ninth audit round (`NativeHostedServiceNames`) — see `docs/ARCHITECTURE.md`'s "Product positioning" section and `CHANGELOG.md`'s `## V4.0.0` entries.

## Context and Problem Statement

HealthCheckPlus wraps and extends ASP.NET Core's native health-check system rather than replacing it, which means it constantly has to integrate with types and behaviors it doesn't own. Two integration points originally reached past the framework's supported public surface into internal implementation details: `AddCheckLinkTo` (adopting an external check) originally reflected into an internal `ConfigureNamedOptions` type instead of using the public `IServiceCollection.Configure<HealthCheckServiceOptions>` pipeline; and detecting/removing the native background publisher service depends on that service's internal type name (`HealthCheckPublisherHostedService`), which carries no public, supported contract at all. Reaching past a framework's public surface risks breaking silently on a framework update that renames or restructures the internal detail being depended on.

## Decision Drivers

* "Prefer matching the native type/registration surface exactly over introducing HealthCheckPlus-specific replacements" is already this library's stated positioning (`docs/ARCHITECTURE.md`) — reflecting into internal types contradicts that stance.
* Where a public alternative genuinely exists, using it removes an entire class of fragility risk at zero ongoing cost.
* Where no public alternative exists at all, the dependency on an internal detail is unavoidable — but it should be isolated to one place and its risk written down explicitly, rather than left as an implicit assumption a future maintainer might not notice.

## Considered Options

* Keep reflecting into internal framework types wherever convenient.
* Replace every internal-type dependency with its public equivalent, and where none exists, isolate and document the one unavoidable dependency explicitly.
* Fork or vendor the specific native behavior needed, avoiding any dependency on the native type at all.

## Decision Outcome

Chosen option: "Use the public equivalent everywhere one exists; isolate and document the rest", because it eliminates the fragility risk everywhere it's actually avoidable, and turns the one place it isn't (the native publisher service's internal type name) into a single, named, documented risk instead of a scattered, implicit one.

### Positive Consequences

* `AddCheckLinkTo` now integrates through `IServiceCollection.Configure<HealthCheckServiceOptions>` — the same public, documented Options pipeline the original registration used — instead of reflecting into `ConfigureNamedOptions`.
* The one unavoidable internal-type dependency (the native publisher service's type name) is now centralized in a single shared constant (`NativeHostedServiceNames`) instead of duplicated as a literal string across multiple call sites, and the residual risk is written down explicitly in `docs/POINTS_OF_ATTENTION.md`'s "Known, accepted edge cases."
* Third-party `IHealthChecksBuilder` extensions and the native DI surface keep working unmodified — a direct benefit of matching the native surface instead of introducing HealthCheckPlus-specific replacements.

### Negative Consequences

* The one residual dependency (the native publisher service's internal type name) remains a real, accepted fragility risk: a future .NET release that renames or restructures that type could silently re-enable double publishing, surfacing as duplicated dispatches rather than a crash.
* There is no way to fully eliminate this residual risk without forking or reimplementing native behavior this library deliberately chooses not to replace (see the "no HealthCheckPlus-specific replacements" stance above).

## Pros and Cons of the Options

### Keep reflecting into internal types wherever convenient

* Good, because it can be more direct/simpler to write in the moment.
* Bad, because it stacks fragility risk across every integration point, with no isolation or documentation of where the risk actually lives.

### Public equivalent everywhere possible; isolate and document the rest

* Good, because it removes the risk everywhere avoidable and confines what's left to one named, documented place.
* Bad, because one genuine residual risk still remains, with no fully risk-free alternative available.

### Fork/vendor the native behavior

* Good, because it would remove the dependency on the native type entirely.
* Bad, because it means reimplementing and maintaining framework behavior this library's own positioning explicitly avoids — a much larger maintenance burden than the risk it would close.

## Links

* Related to [ADR008](./ADR008V01R01-depend-on-the-standalone-health-checks-abstractions-package-and-declare-a-real-nu-get-dependency-between-the-two-packages.md) — both decisions shape how HealthCheckPlus integrates with the surrounding .NET ecosystem.