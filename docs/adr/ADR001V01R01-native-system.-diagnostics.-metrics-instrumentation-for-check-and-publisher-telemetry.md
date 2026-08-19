<!-- Do not remove this comment, lines and table (1-12) -->
|Adr-Plus Fields|Values Migrated |
|--|--|
|File title md|Native System.Diagnostics.Metrics instrumentation for check and publisher telemetry|
|Version|01|
|Revision|01|
|Scope|HealthCheckPlus|
|Domain|Observability|
|Created|Proposed (2026-08-19)|
|Changed|Accepted (2026-08-19)|
|Superseded||
<!-- Do not remove this comment, lines and table (1-12) -->
---
# Native System.Diagnostics.Metrics instrumentation for check and publisher telemetry

## Deciders

* Deciders: Fernando Cerqueira (maintainer)

Technical Story: v4.0.0 hardening cycle, baseline audit (2026-08-18) — see `CHANGELOG.md`'s `## V4.0.0` entry "Added native metrics instrumentation (`System.Diagnostics.Metrics`) for check executions, status transitions, and publisher invocations".

## Context and Problem Statement

Consumers running HealthCheckPlus in production have no built-in way to observe check/publisher behavior over time (execution counts, durations, status-transition rates, publisher outcomes) without instrumenting their own code around every call site. The library needed a way to expose this telemetry natively, without forcing a specific observability vendor or SDK on every consumer, and without adding a dependency the main package didn't already carry.

## Decision Drivers

* The project's own no-third-party-NuGet-dependency rule for the main package (see `CONTRIBUTING.md`) rules out pulling in a full OpenTelemetry SDK just to emit metrics.
* Metrics recording must never be able to break health evaluation itself — a broken exporter/listener is a common, real failure mode this design has to survive.
* Tag cardinality must stay predictable for whoever exports these metrics (Prometheus, OTel, App Insights, …) — free-form strings (exception messages, arbitrary text) would make that impossible to bound.

## Considered Options

* Native `System.Diagnostics.Metrics` (`Meter`/`Instrument`), no SDK dependency.
* Depend on the OpenTelemetry SDK directly and emit through it.
* Ship no built-in metrics; leave instrumentation entirely to the consumer.

## Decision Outcome

Chosen option: "Native `System.Diagnostics.Metrics`", because it's already part of the BCL (`System.Diagnostics.DiagnosticSource`), any exporter that listens on a `Meter` can consume it (OTel, Prometheus, App Insights, or a plain `MeterListener`), and it adds zero new package dependency — consistent with the project's existing packaging stance.

### Positive Consequences

* Any exporter ecosystem can consume `HealthCheckPlusMetrics`'s `"HealthCheckPlus"` meter without this library taking a stance on which one a consumer uses.
* Metric recording is wrapped in its own guard (`SafeRecordMetric`) so a broken `MeterListener` can never propagate into a check result or an HTTP response — see the companion resilience ADR (`ADR005`).
* Tag values are restricted to closed, known sets (check names, publisher type names, status/origin enums) by design, keeping cardinality bounded for any exporter.

### Negative Consequences

* The instrument names and tag vocabulary become a public contract the moment this ships — a later rename or shape change is a breaking change for anyone alerting on them, same as a public API signature.
* Consumers who want richer semantic conventions (e.g. full OTel resource/attribute conventions) still need their own exporter/mapping layer on top; this library only emits the raw instruments.

## Pros and Cons of the Options

### Native `System.Diagnostics.Metrics`

* Good, because zero new package dependency for the main package.
* Good, because exporter-agnostic — consumable by OTel, Prometheus, App Insights, or a bare `MeterListener`.
* Bad, because it's a lower-level API than a full observability SDK — no built-in semantic conventions, resource attributes, or exemplars.

### Depend on the OpenTelemetry SDK directly

* Good, because richer, more standardized semantic conventions out of the box.
* Bad, because it violates the project's no-third-party-dependency rule for the main package and forces that dependency on every consumer, even ones using a different stack.

### No built-in metrics

* Good, because simplest possible library surface — nothing to maintain as a public contract.
* Bad, because every consumer would have to hand-instrument the same execution/status-transition/publisher-outcome events themselves, with no shared vocabulary across HealthCheckPlus deployments.

## Links

* Related to [ADR005](./ADR005V01R01-guard-logging,-metrics,-and-background-cycle-delegates-so-a-single-failure-can-never-break-health-evaluation-or-the-background-loop.md) — metric recording is guarded by the same defensive doctrine this ADR establishes for logging.
