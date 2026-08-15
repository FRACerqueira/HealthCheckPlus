# Changelog

All notable changes to HealthCheckPlus are documented here.

## V4.0.0

- Fixed the Degraded status policy being ignored when resolving the next check interval on HTTP health check requests.
- Health checks registered without an associated policy now fail fast at startup with a clear error naming the check, instead of a delayed runtime exception.
- Health check state is now isolated per host, so multiple hosts running in the same process (integration tests, .NET Aspire) no longer share cached results.
- Added native metrics instrumentation (`System.Diagnostics.Metrics`) for check executions, status transitions, and publisher invocations — no new package dependency.
- The background service now keeps running checks and publishing even if a publisher throws.
- All scheduling now compares against UTC time, avoiding drift on non-UTC hosts.
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
