# Contributing to HealthCheckPlus

Thanks for considering a contribution. This guide covers what you need to know to get a change built, tested, and reviewed.

- [Before you start](#before-you-start)
- [Project layout](#project-layout)
- [Building and testing](#building-and-testing)
- [Code conventions](#code-conventions)
- [Making a change](#making-a-change)
- [Documentation](#documentation)
- [Reporting bugs and requesting features](#reporting-bugs-and-requesting-features)

## Before you start

By contributing, you assert that:

- The contribution is your own original work, and you have the right to submit it under this project's license.
- You agree to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

For anything beyond a small fix, open an issue first to discuss the approach before investing time in an implementation — it avoids rework if the direction isn't a fit.

## Project layout

| Path | Contents |
|---|---|
| `src/HealthCheckPlus` | The main package. Targets `net8.0`/`net9.0`/`net10.0`. |
| `src/HealthCheckPlus.Abstractions` | Public interfaces and types shared across target frameworks, published as a separate package so consumers can depend on the contract without the full implementation. |
| `src/HealthCheckPlusTests` | The test suite (xUnit v3), including integration tests built on `Microsoft.AspNetCore.TestHost`. |
| `Samples/` | Runnable example projects demonstrating usage. |
| `docs/` | Architecture and operational documentation for maintainers/contributors and operators. |

## Building and testing

```
dotnet build HealthCheckPlus.sln
dotnet test src/HealthCheckPlusTests
```

A pull request's CI run builds the solution and runs the full test suite on every push — a change isn't mergeable until both are green. Since the main package multi-targets three frameworks, prefer building the whole solution locally rather than a single project, so a regression on `net8.0`/`net9.0` specifically doesn't surface only in CI.

**The `HealthCheckPlus` package itself must have no dependencies beyond the .NET BCL.** This is a hard constraint, not a preference: features that would otherwise need a third-party library (for example, metrics instrumentation) are built on BCL primitives (`System.Diagnostics.Metrics`) specifically to preserve this. `HealthCheckPlus.Abstractions`, the test project, and the `Samples/` projects are not held to this constraint.

New behavior should come with test coverage — prefer an integration-style test (using `TestHost`, see existing tests in `src/HealthCheckPlusTests/Integration`) over a unit test that calls internals directly whenever the behavior is reachable from the public API, since it exercises the same path a real consumer would.

## Code conventions

Standard .NET conventions apply — see the [.NET Framework Design Guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/) for the general shape. A few things specific to this codebase:

- Logging uses the `[LoggerMessage]` source-generated pattern, with a hard-coded `EventId`/`EventName` per log method (grep for existing `EventIds`/`Log` nested classes for the pattern) — this keeps event names stable even if a method is renamed later.
- A `catch` block must never swallow an exception silently. At minimum, log it; where the codebase already has metrics for a similar defensive path, add one. See `docs/ARCHITECTURE.md`'s [Logging and anomalies](./docs/ARCHITECTURE.md#logging-and-anomalies) section for the pattern this codebase follows.
- Prefer public, documented extension points (`IServiceCollection.Configure<T>`, well-known interfaces) over reflecting into another library's internal types, even when it takes more code — see `docs/ARCHITECTURE.md`'s [Product positioning](./docs/ARCHITECTURE.md#product-positioning) section for why.

## Making a change

- Branch off `main`.
- Keep a pull request focused on one logical change — a large, mixed-purpose PR is harder to review and more likely to stall.
- Avoid reformatting code you didn't otherwise need to touch; unrelated formatting changes make it harder to see what actually changed.
- Make sure `dotnet build` is warning-free and `dotnet test` passes locally before opening the PR.
- Describe what changed and why in the PR description — for a bug fix, include how to reproduce the original problem if it isn't obvious from the diff.

There's no formal SLA on review turnaround. If a PR goes quiet, a follow-up comment is welcome — it doesn't mean the contribution isn't valued.

## Documentation

If a change affects public behavior, update the relevant doc alongside the code change, not as an afterthought:

- **`docs/ARCHITECTURE.md`** — internal design, component responsibilities, and the reasoning behind non-obvious decisions. Aimed at maintainers/contributors.
- **`docs/RUNBOOK.md`** — how to read a health check response, what each log/metric means operationally, and how to diagnose common problems. Aimed at operators.
- **`README.md`** — public API usage and examples.

Describe the current behavior and its trade-offs — the benefit it provides and any point of attention a maintainer or operator should know — rather than narrating how or when a change was made.

## Reporting bugs and requesting features

Open a GitHub issue. Include, for a bug: what you expected, what actually happened, and a minimal reproduction if possible. For a feature request: the use case it solves, not just the shape of the API you'd want.
