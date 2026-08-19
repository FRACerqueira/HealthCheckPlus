# HealthCheckPlus Operational Runbook

This is a reference for operators and on-call engineers running a service that uses HealthCheckPlus: what the health endpoint's response actually means, what happens when a check misbehaves, and which logs/metrics to check first when something looks wrong. For how the library is built internally, see [`ARCHITECTURE.md`](./ARCHITECTURE.md).

## Reading a health check response

A `UseHealthChecksPlus` endpoint returns one of the standard ASP.NET Core status codes by default: **200** for `Healthy`/`Degraded`, **503** for `Unhealthy`. If nothing else is configured, the response body is the aggregate status as plain text (e.g. `Healthy`) — the native `HealthCheckOptions.ResponseWriter` default (`WriteMinimalPlaintext`), not an empty body. A `ResponseWriter` (set via `HealthCheckPlusOptions`) adds a JSON body instead; all of the built-in templates share the same top-level shape:

```json
{
  "status": "Healthy",
  "entries": [
    { "name": "Redis", "status": "Healthy" }
  ]
}
```

| Template | Adds to each entry | Use when |
|---|---|---|
| `WriteShortDetails` | — (name/status only) | You only need to know *which* check is unhealthy, not why. |
| `WriteDetailsWithoutException` | `description`, `duration` | You want the check's own message, without stack traces in the response body. |
| `WriteDetailsWithException` | `description`, `duration`, `exception` | Debugging in an environment where exposing exception details in the response is acceptable (internal-only endpoints — avoid on a public-facing one). |
| `...Plus` variants of any of the above | `dateRef`, `origin` (see below) | You need to know how *stale* a cached result is, or what triggered it — since HealthCheckPlus doesn't necessarily re-run every check on every request (see below). |

**Point of attention**: because results are cached and re-run according to each check's policy, `status`/`description` in the response can reflect a result from several seconds (or minutes) ago, not the instant the request arrived. If you need to know exactly how old a result is, use a `...Plus` response writer and read `dateRef` — **with one caveat**: `dateRef` means "the last time this check's schedule slot was settled", not strictly "the last time it produced a result". Normally the two coincide, but after an aborted attempt (see [When a check times out or fails](#when-a-check-times-out-or-fails)) `dateRef` advances to the abort time while `status`/`description`/`duration`/`origin` still reflect the last *completed* run — so a `...Plus` response can show a recent `dateRef` paired with an older result. Cross-check `origin` and, if precision matters, the `healthcheckplus.check.executions` metric's own timestamp rather than relying on `dateRef` alone to mean "this is when that status was produced".

## The `origin` field

Every cached result records what triggered it — surfaced as `origin` in the `...Plus` response templates, and as the `healthcheckplus.check.origin` tag on the `healthcheckplus.check.executions`/`healthcheckplus.check.duration` metrics:

| Origin | Meaning |
|---|---|
| `None` | Initial value, before the check has ever run. A check still at `None` is invisible to the background service's publishers - it's excluded from what gets hashed (`WhenReportChange`) and handed to `IHealthCheckPublisher`s until it actually runs at least once, so it's never mistaken for a genuine Healthy observation. |
| `SwitchTo` | Set by application code via `IStateHealthChecksPlus.SwitchToUnhealthy`/`SwitchToDegraded` — no check code actually ran; something in the application detected a failure directly (e.g. a caught exception from a dependency call) and forced the status. |
| `UrlRequest` | Produced by a request to a `UseHealthChecksPlus` endpoint. |
| `Background` | Produced by the background polling service (only present if background polling is enabled). |
| `Default` | Produced by a direct call to the native `HealthCheckService.CheckHealthAsync` (e.g. resolved from DI and called directly, bypassing the HTTP endpoint). |

A result with `origin: SwitchTo` staying in place for a long time is expected — it stays until either a scheduled poll runs again and overwrites it, or application code switches it back. It is **not** evidence the polling engine is stuck.

## When a check times out or fails

Two independent timeouts exist, and they apply at different scopes:

- **Per-check timeout** (`AddCheckPlus`/`AddCheckLinkTo`'s `timeout` parameter, or the native `HealthCheckRegistration.Timeout`): if the check itself doesn't complete in time, its result becomes `FailureStatus` (`Unhealthy` unless configured otherwise) with `description: "A timeout occurred while running check."`. Only that one check is affected — other checks and publishing continue normally.
- **Background-cycle timeout** (`HealthCheckPlusBackGroundOptions.Timeout`, default 30s): a safety net around a single background cycle, applied twice — once around running that cycle's due checks, and separately around dispatching that cycle's publishers — so neither phase can block the other or the next cycle's idle wait indefinitely. Both are *cooperative*: they cancel a `CancellationToken` that the check/publisher is expected to observe. A check or publisher that ignores it (blocking synchronous I/O, or an awaited call with no cancellation support) is not forcibly stopped and can still block that phase past the configured Timeout — this is the same limitation the native ASP.NET Core health check publisher has. Setting `Timeout` to `System.Threading.Timeout.InfiniteTimeSpan` disables this safety net entirely for that cycle (the phase can then genuinely block indefinitely on a misbehaving check/publisher); the app's own shutdown cancellation still applies regardless.
- **Ambient cancellation** (the HTTP client disconnecting mid-request, or — for the background path — the cycle timeout above firing while a check is still running): the check's execution is abandoned, logged as a Warning (`HealthCheckExecutionAborted`), and counted as the `check_execution_aborted` anomaly (table below). Its last known result is left untouched — deliberately not overwritten with a synthetic one — so every other reader keeps seeing the last real status. The check's schedule is still advanced as if this attempt had happened (see the `dateRef` caveat above), so it's retried on its normal per-status period rather than immediately on every subsequent poll or request. On the background path this backoff always applies, even for a status with no explicit policy registered - `HealthCheckPlusBackGroundOptions`'s own per-status defaults (`HealthyPeriod`/`DegradedPeriod`/`UnhealthyPeriod`, 30s each unless configured) apply whenever there's no explicit one, so there's always *some* period. On the HTTP-only path (no `AddBackgroundPolicy`), this only holds when the check's current-status policy has an explicit period configured — a check left at its default (no period set for the current status, which on this path always falls back to the check's own Healthy policy) has no backoff at all and is re-attempted on every request regardless of this mechanism.

An unhandled exception from a check's own code (not a timeout, not ambient cancellation) is caught, logged as `HealthCheckError`, and turned into a result with `FailureStatus` and the exception's message as `description` — the check's own exception is never allowed to fail the request or crash the background loop. The same applies to the *cached status* if the check can't even be *constructed* (e.g. its registration's factory throws while resolving a dependency): the cache is still updated to `FailureStatus`, not silently left at whatever status it happened to have before, and this is logged as `HealthCheckError` too. Unlike a normal check failure, though, a factory failure still propagates out of that specific call: the background loop absorbs it (logs `ProcessingError` and continues on the next cycle), but on the HTTP path the `/health` request itself fails rather than returning a response with that one check marked `Unhealthy` — the *next* request already sees the corrected cached status.

## Diagnosing common situations

**A check appears stuck reporting the same status longer than its configured period.**
Check whether its policy's period for the *current* status is what you expect — `Healthy`/`Degraded`/`Unhealthy` can each have a different period, and if none was explicitly registered for the current status, the fallback differs by path (the check's own Healthy policy on the HTTP path; the background service's own per-status default on the background path — see `ARCHITECTURE.md`). Also check `origin`: a `SwitchTo` result only clears on the next successful poll or another manual call.

**The background service seems to have stopped running checks entirely.**
First, look for `HealthCheckPlusBackGroundLoopFaulted` (Critical) — this is the one signal that means the loop actually ran and then died, as opposed to never having started. If it's present, the exception it carries is the real cause; nothing else below applies. `HealthCheckPublisherCycleError` (Warning) and `HealthCheckPlusBackGroundPublishReportBuildError` (Error) are not this - both mean the loop hit a failure during a cycle (a publisher throwing, or the configured `Predicate` throwing while building the report to publish, respectively) and *continued* regardless, so their presence alone isn't the problem either. If checks have genuinely stopped (no new `healthcheckplus.check.executions` measurements, no periodic `HealthCheckPlus Background-Service` debug/processing logs, and no `HealthCheckPlusBackGroundLoopFaulted`), that indicates the background hosted service itself never started — check that `AddBackgroundPolicy()` was actually called and that the host started successfully.

**A publisher isn't firing when you expect it to.**
Check `healthcheckplus.publisher.invocations` filtered by that publisher's type name and look at the `healthcheckplus.publisher.result` tag: `skipped_no_change` means the aggregate report hasn't changed since the last publish (`WhenReportChange`); `skipped_condition` means the publisher's own `IHealthCheckPlusPublisher.PublisherCondition` returned false; `error` means it threw (see the paired `HealthCheckPublisherError`/`HealthCheckPublisherTimeout` log for which publisher and why).

**You're seeing `healthcheckplus.anomalies` measurements or their paired Warning logs.**
These are defensive paths that were handled without failing a request or crashing a loop — see the reason:

| `healthcheckplus.anomaly.reason` | Paired log | What it means | What to do |
|---|---|---|---|
| `update_result_dropped` | `HealthCheckPlusUpdateDropped` | A check's result arrived but was discarded — either the check name isn't registered (a configuration bug), or two overlapping executions of the same check both finished and the second one's result was dropped. | If frequent for a specific check, its period may be shorter than the check's own typical execution time, causing overlap; consider lengthening the period or shortening the check's own work. |
| `adopted_check_dispose_failed` | `HealthCheckDisposeError` | An adopted external check's own `IDisposable.Dispose()` threw during host shutdown. | Investigate the underlying dependency's disposal behavior (e.g. a connection multiplexer failing because its socket was already force-closed) — shutdown still completed correctly, but that resource's cleanup didn't. |
| `publisher_cycle_failed_but_continued` | `HealthCheckPublisherCycleError` | A publisher threw during a cycle; the background loop kept running. | See the specific publisher's own `HealthCheckPublisherError`/`HealthCheckPublisherTimeout` log entry for which one and why. |
| `check_execution_aborted` | `HealthCheckExecutionAborted` | A check's execution was abandoned because the ambient cancellation fired (client disconnect, or a background-cycle timeout) before it finished — see "Ambient cancellation" above. Its last real result is unchanged; it retries on its normal schedule, not immediately. | Usually benign for an occasional HTTP client disconnect. A high rate on the background path suggests the check itself is routinely slower than the configured `Timeout`. |
| `publish_report_build_failed` | `HealthCheckPlusBackGroundPublishReportBuildError` | Building the report to hand to publishers this cycle failed — most likely `HealthCheckPlusBackGroundOptions.Predicate` itself throwing. No publishers were invoked this cycle; the background loop continues on its next cycle regardless. | Check the exception in the paired log; a `Predicate` should handle every registration it might see without throwing (e.g. don't assume every registration has tags). |
| `switchto_dropped_while_running` | `HealthCheckPlusSwitchToDropped` | A manual override (`SwitchToUnhealthy`/`SwitchToDegraded`) was dropped because a scheduled execution was already running for that check. The override never applied; the in-flight execution's own result will land shortly instead. | Usually benign - retry the override after the in-flight execution completes if it's still needed. A high rate suggests overrides are being issued for checks that are already running frequently. |
| `logging_sink_failed` | **None, by design** | A log call itself threw (a broken third-party `ILoggerProvider`/sink) and was swallowed instead of crashing the background loop, `Dispose`/`DisposeAsync`, or turning an already-successful check evaluation into a 500 on `/health`. There is deliberately no paired log for this one - the log call is exactly what's broken, so trying to log its own failure could throw again for the same reason. **This counter reading zero does not prove every logging call is guarded**: `RunCheckAsync`'s `_logger.BeginScope(...)` is a known, deliberately unguarded exception (see `docs/ARCHITECTURE.md`) - a throwing sink there surfaces as a real exception on `/health` instead of this anomaly. Separately, a logger failure occurring at the exact same moment as a `Meter`/`MeterListener` failure can still propagate out of `Dispose`/`DisposeAsync`/`StopAsync`, an accepted residual gap (also documented in `docs/ARCHITECTURE.md`). | Investigate the registered `ILoggerProvider`(s), not HealthCheckPlus - any nonzero rate here means logging itself is unreliable, independent of check/publisher health. |

A `HealthCheckPlusMetricsRecordingError` log with no matching `healthcheckplus.anomalies` measurement means the metrics pipeline itself failed to record something (e.g. a broken exporter/listener) — health evaluation and publishing are unaffected, but check whatever is consuming the `"HealthCheckPlus"` meter for its own errors.

A `HealthCheckPlusBackGroundStopCancellationError` log, also with no matching `healthcheckplus.anomalies` measurement, means cancelling the background loop's own shutdown token itself threw while the host was already stopping (e.g. a health check's own `CancellationToken.Register` callback misbehaving) — shutdown still proceeds regardless, but investigate whatever registered that callback.

## Metrics quick reference

See `ARCHITECTURE.md`'s [Metrics](./ARCHITECTURE.md#metrics) section for the full instrument list and tags. For alerting, the two worth watching by default are:

- `healthcheckplus.check.status_transitions{healthcheckplus.check.status=Unhealthy}` — rate of checks becoming unhealthy.
- `healthcheckplus.anomalies` — any nonzero rate here means a defensive path fired; read the paired log for detail (table above) — except `logging_sink_failed`, which has none by design.
