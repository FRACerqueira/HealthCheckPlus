# Points of Attention

A short, plain-language list of the things worth knowing **before** you build on HealthCheckPlus - not how it works internally (see [`ARCHITECTURE.md`](./ARCHITECTURE.md) for that), and not how to diagnose it after something's gone wrong in production (see [`RUNBOOK.md`](./RUNBOOK.md) for that). This page is the middle ground: decisions you might otherwise only discover by hitting them.

Each item below is a real, current behavior of the library, not a hypothetical - if something here ever looks wrong, trust the code and the linked doc over this page and let the maintainers know.

## Setting it up

**Every health check needs a Healthy policy, even ones you didn't register through this library.** If you add a check through a third-party package's own extension (e.g. `AddRedis(...)`) and never give it a policy via `AddCheckPlus`/`AddCheckLinkTo`, your app will refuse to start. This is deliberate - a clear startup error naming the check is far easier to act on than a runtime failure the first time someone hits `/health`. The same fail-fast applies in reverse: a policy that names a check that was never registered (a typo, or a casing mismatch) is also caught at startup.

**Call `AddBackgroundPolicy()` after you've registered every health check, not before.** If something else calls the native ASP.NET Core `AddHealthChecks()` again after `AddBackgroundPolicy()` - your own startup code, most commonly - and you also have a publisher registered, the app will refuse to start with a clear error. Getting the order right avoids this entirely; see [`ARCHITECTURE.md`](./ARCHITECTURE.md#publishing) if you want the mechanics.

**This library is built and tested for dozens of health checks, not thousands.** Several internal costs scale with your total number of registered checks (not just how many are actually due to run), which is a deliberate simplicity trade-off at the scale this project targets. If your app registers checks in the thousands, talk to the maintainers before relying on it at that scale.

## Publishing results

**Publishing is off by default, even if you register a publisher.** Adding an `IHealthCheckPublisher` and calling `AddBackgroundPolicy()` alone does not dispatch it - you also need to explicitly turn on `PublishingOptions.Enabled` (assigning a new `PublishingOptions { ... }` does this). There's no log or metric for "a publisher is configured but nothing is telling it to run," so this is easy to miss silently. See the [README](../README.md#usage) for a working example.

**A single permanently-broken publisher costs more than just its own failures.** `WhenReportChange` only stops re-dispatching once a cycle's publisher round finishes without any of them throwing. If one publisher is stuck failing, every other publisher - including healthy ones - gets re-invoked with the same unchanged report every idle cycle, not just the broken one. This is intentional (a transient failure must never silently swallow a real, unpublished change), but it means a broken publisher is worth fixing promptly, not just tolerating.

## Reading and reacting to status

**Never call `SwitchToUnhealthy`/`SwitchToDegraded` from inside a `StatusHealthReport` delegate.** Doing so will make your app crash with a clear exception rather than silently misbehaving - the delegate runs as part of computing that same status, and calling back into it recurses without end. If you need a delegate to react to a bad status, do the reacting somewhere else (a publisher, a background job, application code that already has the report in hand) and only compute a status inside `StatusHealthReport` itself.

**`HealthReport.TotalDuration` doesn't mean the same thing everywhere you might read it.** From the HTTP endpoint, it's the real time spent running that request's checks. From a named aggregate (`Status(name)`, or a `StatusHealthReport` invoked via a background cycle or a manual override), it's always zero - there's no batch execution to time, just an instant read of already-cached results. If your own logic inspects `TotalDuration` to decide something, make sure you know which of the two you're actually looking at; don't build alerting or business logic on it from a named-aggregate context.

**If you run multiple hosts in the same process (integration tests, .NET Aspire, or similar), each one has its own, fully isolated health state.** There is no global accessor - resolve `IStateHealthChecksPlus` from the same host's own DI container you're already working with. This is a feature (no cross-host leakage), but it means "resolve it from anywhere" isn't a valid pattern if you have more than one host.

## Known, accepted edge cases

These are documented limitations the maintainers have consciously chosen not to close, because doing so would trade a real, common-case benefit for protection against an extremely narrow failure mode. They're listed here for transparency, not because you're likely to hit them.

- If your own custom `ILogger` provider throws specifically while starting a logging scope (not while writing a log line), that one health check's execution can surface as a real exception instead of being safely absorbed like every other logging failure in this library.
- In the (very unlikely) case where your logging provider **and** your metrics listener both fail at the exact same moment, that combined failure can still escape cleanly and propagate out of shutdown, instead of being fully swallowed.
- The mechanism that stops ASP.NET Core's own native background publisher service from running alongside this library's own one depends on that native service's internal type name, which has no public, supported contract. A future .NET release that renames or restructures it could silently re-enable double publishing - this would surface as duplicated publisher dispatches, not a crash, and is called out here so it isn't mysterious if it ever happens.
