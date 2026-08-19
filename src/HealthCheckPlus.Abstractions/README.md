# ![HealthCheckPlus Logo](https://raw.githubusercontent.com/FRACerqueira/HealthCheckPlus/refs/heads/main/icon.png) HealthCheckPlus.Abstractions

### **Abstractions of HealthCheckPlus - the shared contract between the main package and your own code.**

[![Build](https://github.com/FRACerqueira/HealthCheckPlus/workflows/Build/badge.svg)](https://github.com/FRACerqueira/HealthCheckPlus/actions/workflows/build.yml)
[![License](https://img.shields.io/github/license/FRACerqueira/HealthCheckPlus)](https://github.com/FRACerqueira/HealthCheckPlus/blob/main/LICENSE)
[![NuGet](https://img.shields.io/nuget/v/HealthCheckPlus.Abstractions)](https://www.nuget.org/packages/HealthCheckPlus.Abstractions/)
[![Downloads](https://img.shields.io/nuget/dt/HealthCheckPlus.Abstractions)](https://www.nuget.org/packages/HealthCheckPlus.Abstractions/)

This package holds only the types you need to **read** HealthCheckPlus state and **write** your own `IHealthCheckPublisher` - without depending on ASP.NET Core or the main `HealthCheckPlus` package's registration/middleware surface. Reach for it from a class library, a background worker, or any project that consumes health state but doesn't itself register or serve health checks.

If you're looking for `AddHealthChecksPlus`, `UseHealthChecksPlus`, `AddBackgroundPolicy`, or the response-writer templates, those live in the main [**HealthCheckPlus**](https://www.nuget.org/packages/HealthCheckPlus/) package instead - installing it already brings this one in as a dependency.

## Table of Contents

- [Installing](#installing)
- [What's in this package](#whats-in-this-package)
- [Usage](#usage)
- [Documentation](#documentation)
- [Changelog](#changelog)
- [Code of Conduct](#code-of-conduct)
- [Contributing](#contributing)
- [License](#license)

## Installing
[**Top**](#table-of-contents)

```
Install-Package HealthCheckPlus.Abstractions [-pre]
```

```
dotnet add package HealthCheckPlus.Abstractions [--prerelease]
```

**_Note: [-pre]/[--prerelease] usage for pre-release versions_**

## What's in this package
[**Top**](#table-of-contents)

- `IStateHealthChecksPlus` - read cached check results and named aggregates (`Status`, `StatusResult`, `TryGetHealthy`/`TryGetNotHealthy`/`TryGetDegraded`/`TryGetUnhealthy`, `ConvertToPlus`), or force a manual override (`SwitchToUnhealthy`/`SwitchToDegraded`). Resolve it from DI in any project that also references the main `HealthCheckPlus` package, which registers the implementation.
- `IDataHealthPlus` - one check's last result (`Name`, `LastResult`, `Duration`, `DateRef`, `Origin`), the shape `ConvertToPlus` returns.
- `HealthCheckTrigger` - what triggered a result (`UrlRequest`, `Background`, `SwitchTo`, `None`, `Default`).
- `IHealthCheckPlusPublisher` - implement this (instead of the native `IHealthCheckPublisher`) to add an optional `PublisherCondition` the background service checks before invoking your publisher.
- `HealthReportExtensions` - convenience extensions on the native `HealthReport` (`StatusResult`, `TryGetHealthy`/`TryGetNotHealthy`/`TryGetDegraded`/`TryGetUnhealthy`) for code that only ever sees a plain `HealthReport` (e.g. inside an `IHealthCheckPublisher`), without needing `IStateHealthChecksPlus` at all.

## Usage
[**Top**](#table-of-contents)

Reading state from anywhere `IStateHealthChecksPlus` can be resolved (requires the main `HealthCheckPlus` package to be registered somewhere in the same host):

```csharp
public class SomeService(IStateHealthChecksPlus state)
{
    public void Report()
    {
        var overall = state.Status();               // default aggregate
        var redis = state.StatusResult("MyRedis");   // one check's last HealthCheckResult

        if (state.TryGetUnhealthy(out var unhealthy))
        {
            foreach (var (name, result) in unhealthy)
            {
                // ...
            }
        }
    }
}
```

Writing a publisher with its own opt-in condition:

```csharp
public class MyPublisher : IHealthCheckPlusPublisher
{
    public Func<HealthReport, bool>? PublisherCondition { get; set; } = report => report.Status != HealthStatus.Healthy;

    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        if (report.TryGetUnhealthy(out var unhealthy))
        {
            // ...
        }
        return Task.CompletedTask;
    }
}
```

## Documentation
[**Top**](#table-of-contents)

- [Points of attention](https://github.com/FRACerqueira/HealthCheckPlus/blob/main/docs/POINTS_OF_ATTENTION.md) - what to know before you build on HealthCheckPlus, in plain language. Start here.
- [Architecture](https://github.com/FRACerqueira/HealthCheckPlus/blob/main/docs/ARCHITECTURE.md) - how HealthCheckPlus is put together internally, for maintainers and contributors.
- [Operational runbook](https://github.com/FRACerqueira/HealthCheckPlus/blob/main/docs/RUNBOOK.md) - how to read a health check response and diagnose common problems, for operators.
- [API reference](https://github.com/FRACerqueira/HealthCheckPlus/blob/main/docs/api/docindex.md) - generated from the XML doc comments.

## Changelog
[**Top**](#table-of-contents)

See [CHANGELOG.md](https://github.com/FRACerqueira/HealthCheckPlus/blob/main/CHANGELOG.md) for the version history (shared with the main package - both are versioned and released together).

## Code of Conduct
[**Top**](#table-of-contents)

This project has adopted the code of conduct defined by the Contributor Covenant to clarify expected behavior in our community.
For more information see the [Code of Conduct](https://github.com/FRACerqueira/HealthCheckPlus/blob/main/CODE_OF_CONDUCT.md).

## Contributing
[**Top**](#table-of-contents)

See the [Contributing guide](https://github.com/FRACerqueira/HealthCheckPlus/blob/main/CONTRIBUTING.md) for developer documentation.

## License
[**Top**](#table-of-contents)

Copyright 2023 @ Fernando Cerqueira
