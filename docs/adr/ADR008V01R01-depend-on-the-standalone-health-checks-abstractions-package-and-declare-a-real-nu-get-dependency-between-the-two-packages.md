<!-- Do not remove this comment, lines and table (1-12) -->
|Adr-Plus Fields|Values Migrated |
|--|--|
|File title md|Depend on the standalone HealthChecks Abstractions package and declare a real NuGet dependency between the two packages|
|Version|01|
|Revision|01|
|Scope|HealthCheckPlus;HealthCheckPlus.Abstractions|
|Domain|Packaging|
|Created|Proposed (2026-08-19)|
|Changed|Accepted (2026-08-19)|
|Superseded||
<!-- Do not remove this comment, lines and table (1-12) -->
---
# Depend on the standalone HealthChecks Abstractions package and declare a real NuGet dependency between the two packages

## Deciders

* Deciders: Fernando Cerqueira (maintainer)

Technical Story: v4.0.0 hardening cycle — see `CHANGELOG.md`'s `## V4.0.0` entries for the `FrameworkReference`→`PackageReference` swap (verified via a real `dotnet pack` + a scratch console app) and the M1 fix ("the main NuGet package embedded a private copy of `HealthCheckPlus.Abstractions.dll` while declaring no dependency on the `HealthCheckPlus.Abstractions` package").

## Context and Problem Statement

Two independent packaging problems affected the relationship between `HealthCheckPlus` and `HealthCheckPlus.Abstractions`: (1) `HealthCheckPlus.Abstractions.csproj` carried a `<FrameworkReference Include="Microsoft.AspNetCore.App" />` despite never using an ASP.NET-Core-specific type — forcing the full ASP.NET Core shared runtime onto any consumer who only wanted the abstractions (e.g. to reference them from a worker service or a slim `dotnet/runtime` image), contradicting the package's own stated positioning; (2) the main `HealthCheckPlus` package embedded a private copy of `HealthCheckPlus.Abstractions.dll` inside its own `.nupkg` while declaring zero NuGet dependency on that package — so a consumer who installed both packages (the documented, recommended shape for a project that only needs the abstractions elsewhere) risked loading two independent copies of the same types.

## Decision Drivers

* A package's declared dependencies should reflect what it actually uses — `Microsoft.AspNetCore.App` was never justified by any real usage in `HealthCheckPlus.Abstractions`.
* Two independently-loaded copies of the same types (from an embedded DLL plus a separately-installed package) is a correctness risk (type-identity mismatches), not just a packaging inefficiency.
* Both problems concern the same underlying question — what `HealthCheckPlus.Abstractions` actually is and how the main package relates to it — so they were decided and verified together.

## Considered Options

* Leave both as-is (status quo).
* Fix each problem narrowly: swap the `FrameworkReference` for the specific standalone package actually needed; replace the embedded-DLL packaging with a normal package dependency.
* Merge `HealthCheckPlus.Abstractions` back into the main package entirely, removing the two-package split altogether.

## Decision Outcome

Chosen option: "Fix each problem narrowly", because both fixes are independently well-scoped, low-risk, and verifiable (a real `dotnet pack` plus a scratch console app confirmed the `runtimeconfig.json` no longer lists `Microsoft.AspNetCore.App`; unzipping the packed `.nupkg` confirmed the dependency is now declared, not embedded) — collapsing the two-package split entirely would have been a much larger, unrelated change to the project's public packaging shape.

### Positive Consequences

* `HealthCheckPlus.Abstractions` can now be referenced from a project that never touches ASP.NET Core (a worker service, a slim runtime image) without dragging in the shared framework — verified end-to-end, not just at the `.csproj` level.
* Installing both packages together no longer risks loading two independent copies of the same types — the main package now declares a real dependency instead of embedding a private copy.

### Negative Consequences

* **Breaking for one specific upgrade path**: because the two packages were independent before this fix, a project referencing both that upgrades only `HealthCheckPlus` to 4.0.0 without also upgrading `HealthCheckPlus.Abstractions` to 4.0.0 will fail to restore (`NU1605`, package downgrade detected) — both must be upgraded together.
* The two packages are now more tightly coupled at the dependency-graph level than before, a deliberate trade-off against the type-identity risk the embedded-DLL approach carried.

## Pros and Cons of the Options

### Leave both as-is

* Good, because zero implementation cost, no breaking change.
* Bad, because both problems persist: the shared-framework requirement stays wrong for the package's own stated audience, and the type-identity risk from the embedded DLL stays live for anyone installing both packages.

### Fix each problem narrowly

* Good, because each fix is independently verifiable and scoped to the actual defect, without touching the two-package structure itself.
* Bad, because it introduces one specific breaking upgrade path (`NU1605` if only one package is bumped) that didn't exist before.

### Merge the two packages back together

* Good, because it would eliminate this entire class of two-package-relationship problem at the root.
* Bad, because it's a much larger, unrelated change to the project's public packaging shape — reversing a prior, intentional design decision (isolating public interfaces/classes into their own assembly) well beyond the scope of the two defects being fixed here.

## Links

* Related to [ADR009](./ADR009V01R01-depend-only-on-supported-public-contracts-for-framework-integration.md) — both concern how HealthCheckPlus should depend on and integrate with the .NET ecosystem around it.
