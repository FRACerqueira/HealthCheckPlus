# Changelog

All notable changes to HealthCheckPlus are documented here.

## V4.0.0

- **Breaking**: `AddHealthChecksPlus()` no longer takes a `names` parameter. The set of tracked health checks is now taken directly from whatever ends up registered via `AddCheckPlus`/`AddCheckLinkTo`/any native `IHealthChecksBuilder` extension - there is no separate list to keep in sync, and no way for it to drift from the real registrations.
- Fixed the Degraded status policy being ignored when resolving the next check interval on HTTP health check requests.
- Health checks registered without an associated policy now fail fast at startup with a clear error naming the check, instead of a delayed runtime exception.
- Health check state is now isolated per host, so multiple hosts running in the same process (integration tests, .NET Aspire) no longer share cached results.
- Added native metrics instrumentation (`System.Diagnostics.Metrics`) for check executions, status transitions, and publisher invocations — no new package dependency.
- The background service now keeps running checks and publishing even if a publisher throws, including if the configured `Predicate` itself throws while building the report to hand to publishers (logged as `HealthCheckPlusBackGroundPublishReportBuildError`, recorded as a `publish_report_build_failed` anomaly).
- Fixed `SwitchToUnhealthy`/`SwitchToDegraded` not refreshing a named status aggregate (`Status(name)`, registered via `AddStatusName`) - a manual override could go unreflected in that aggregate until the next request or background cycle happened to run.
- Fixed the main `HealthCheckPlus` NuGet package embedding a private copy of `HealthCheckPlus.Abstractions.dll` while declaring no dependency on the `HealthCheckPlus.Abstractions` package - installing both (as recommended for projects that only need the abstractions) could load two independent copies of the same types. The main package now declares a normal package dependency instead.
- Fixed a cached check result's `Status`/`Description`/`Duration`/`Origin` being torn under concurrent updates - a reader could observe a mix of an old and a new result (e.g. a new `Status` paired with the previous `Description`). These now live in one immutable snapshot swapped atomically, and every "Plus" response writer now serializes a frozen copy instead of the live cache entry.
- All scheduling now compares against UTC time, avoiding drift on non-UTC hosts.
- A throwing `IHealthCheckPlusPublisher.PublisherCondition` is now attributed to that exact publisher (`HealthCheckPublisherError`, and the `error` result on `healthcheckplus.publisher.invocations`) instead of only surfacing as the generic cycle-level failure with no indication of which publisher or why.
- `AddUnhealthyPolicy`/`AddDegradedPolicy`/`AddCheckPlus`/`AddCheckLinkTo` now fail fast at startup if a policy names a health check that was never actually registered (a typo), instead of silently registering a policy that could never match any real check.
- Fixed `PublishingOptions.Enabled`'s XML doc, which claimed a setter that doesn't exist and the wrong default for how it's actually configured via `AddBackgroundPolicy`.
- A health check that hasn't run even once yet is no longer published as a genuine Healthy result by the background service - it's excluded from what gets hashed (`WhenReportChange`) and handed to publishers until it actually runs, the same way a `Predicate`-excluded check already was.
- Fixed `AddCheckPlus`/`AddCheckLinkTo`'s `delay`/`period` XML docs, which incorrectly described `IHealthCheckPublisher` timing instead of the check's own scheduling.
- `SwitchToUnhealthy`/`SwitchToDegraded` no longer drop a manual override silently when the check is currently running - it's now logged (`HealthCheckPlusSwitchToDropped`) and recorded as a `switchto_dropped_while_running` anomaly.
- Policy lookup (`AddUnhealthyPolicy`/`AddDegradedPolicy`/`AddCheckPlus`/`AddCheckLinkTo`, and the startup validations that check them) now compares check names case-insensitively, matching how the rest of the library already treats them - a policy could previously go unmatched against its own check over a casing difference alone.
- `IStateHealthChecksPlus`'s XML docs now document the `ArgumentException`s its members can throw for an unregistered name.
- `IStateHealthChecksPlus.ConvertToPlus` is now evaluated eagerly instead of lazily, so a failure (an entry naming a check no longer tracked) surfaces immediately rather than mid-write of an already-started JSON response.
- Internal casts that assume a native `HealthCheckService`/`IStateHealthChecksPlus` registration is actually the concrete type `AddHealthChecksPlus()` registers now fail with a clear message naming the registration and the actual type found, instead of a generic `InvalidCastException`, if something replaced or decorated that registration.
- Policy lookup (resolving which `HealthCheckPlusPolicyStatus` applies to a check) is now O(1) instead of a linear scan through every registered policy - the per-cycle/per-request cost of resolving policies for every registration was effectively quadratic in the number of checks, now linear.
- See [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md) and [`docs/RUNBOOK.md`](./docs/RUNBOOK.md) for the full design and operational reference.

## V3.0.1

- Added support for .Net10
- Sanitization of references

## V3.0.0

- Added support for .Net9
- Removed support for .Net6, .Net7
- Removed commands with enum for list of HealthCheck's
- Some property names have been refactored for readability or syntax errors.
- Optimized several parts of the code to improve performance
- Fixed publisher improper execution bug when set to only execute when there are changes
- Documentation updated

## V2.0.1

- Created dependency isolation package: HealthCheckPlus.Abstractions
    - Now all public interfaces and classes are isolated in another assembly
