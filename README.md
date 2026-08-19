# ![HealthCheckPlus Logo](https://raw.githubusercontent.com/FRACerqueira/HealthCheckPlus/refs/heads/main/icon.png) Welcome to HealthCheckPlus

### **Per-status polling policies, cached results, and a smarter publisher pipeline for ASP.NET Core health checks.**

[![Build](https://github.com/FRACerqueira/HealthCheckPlus/workflows/Build/badge.svg)](https://github.com/FRACerqueira/HealthCheckPlus/actions/workflows/build.yml)
[![License](https://img.shields.io/github/license/FRACerqueira/HealthCheckPlus)](./LICENSE)
[![NuGet](https://img.shields.io/nuget/v/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)
[![Downloads](https://img.shields.io/nuget/dt/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)

**HealthCheckPlus** is written in C#, targeting **.NET 10**, **.NET 9**, and **.NET 8**. It builds on top of ASP.NET Core's native health check system (`Microsoft.Extensions.Diagnostics.HealthChecks`) rather than replacing it - your existing `IHealthCheck` implementations and third-party check packages keep working unchanged.

## Table of Contents

- [Features](#features)
- [Installing](#installing)
- [Examples](#examples)
- [Usage](#usage)
- [Documentation](#documentation)
- [Changelog](#changelog)
- [Code of Conduct](#code-of-conduct)
- [Contributing](#contributing)
- [Credits](#credits)
- [License](#license)

## Features
[**Top**](#table-of-contents)

**Per-status scheduling.** Each health check can poll at a different rate depending on its own last-known status - for example, check a healthy dependency every 30 seconds but a degraded one every 5. Configure a Healthy policy (the default, always required), and optionally a Degraded and/or Unhealthy policy for each check.

**A cache of the last result per check.** A request to `/health` doesn't necessarily re-run every check synchronously - it reads whatever the last poll already produced, subject to that check's own policy. You can also read the cached result for any check, or force one to Unhealthy/Degraded directly from application code (e.g. after catching an exception talking to a dependency) - see [`SwitchToUnhealthy`/`SwitchToDegraded`](#usage) below.

**Adopting external/third-party checks.** Register a check from any existing `IHealthChecksBuilder` extension (e.g. `AddRedis(...)`) and give it its own delay, period, and policy rules the same way as a custom check.

**An optional background service.** Runs checks on its own schedule, independent of HTTP traffic, and drives every registered `IHealthCheckPublisher` with two extra filters on top of the native publisher pipeline:
- Publish only every N idle cycles, not every cycle (`AfterIdleCount`).
- Publish only when the aggregate report actually changed since the last publish (`WhenReportChange`).
- A publisher can add its own custom gating via `IHealthCheckPlusPublisher.PublisherCondition`.

**Six response templates**, all serialized as `application/json; charset=utf-8`, from a short status-only body up to full details with descriptions and exceptions:
- `WriteShortDetails` / `WriteShortDetailsPlus`
- `WriteDetailsWithoutException` / `WriteDetailsWithoutExceptionPlus`
- `WriteDetailsWithException` / `WriteDetailsWithExceptionPlus`

The `...Plus` variants add `dateRef`/`origin` to each entry (how stale a cached result is, and what triggered it), which means they need an extra `IStateHealthChecksPlus` parameter - so `ResponseWriter` takes a small lambda instead of a direct method reference: `ResponseWriter = (ctx, report) => HealthCheckPlusOptions.WriteDetailsWithExceptionPlus(ctx, report, stateHealthChecksPlus)` (see the Samples for a full working example).

**Native metrics** via `System.Diagnostics.Metrics` (check executions, status transitions, publisher invocations) - no extra package dependency, consumable by any exporter (OpenTelemetry, Prometheus, App Insights, ...).

**A fluent, chainable API** that extends the native health check builder rather than replacing it.

## Installing
[**Top**](#table-of-contents)

Top layer
```
Install-Package HealthCheckPlus [-pre]
```

```
dotnet add package HealthCheckPlus [--prerelease]
```

Other layer
```
Install-Package HealthCheckPlus.Abstractions [-pre]
```

```
dotnet add package HealthCheckPlus.Abstractions [--prerelease]
```

**_Note:  [-pre]/[--prerelease] usage for pre-release versions_**

## Examples
[**Top**](#table-of-contents)

Three runnable projects under [**Samples**](./Samples) — clone the repo and `dotnet run` any of them:

- [**HealthCheckPlusDemo**](./Samples/HealthCheckPlusDemo) — the smallest complete setup: custom checks, an adopted external check (Redis), per-status policies, the HTTP endpoints, and the manual-override/middleware patterns. Start here.
- [**HealthCheckPlusDemoBackgroudService**](./Samples/HealthCheckPlusDemoBackgroudService) — the same setup plus background polling and publishing: `AddBackgroundPolicy`, a custom `IHealthCheckPublisher`, and five endpoints side by side showing different response templates (short, full, default, and interop with the native `UseHealthChecks` middleware).
- [**HealthCheckPlusDemoMetrics**](./Samples/HealthCheckPlusDemoMetrics) — observing the native `System.Diagnostics.Metrics` instrumentation: a `MeterListener` prints every `healthcheckplus.*` measurement to the console as it's recorded, no exporter required.

## Usage
[**Top**](#table-of-contents)

**HealthCheckPlus** uses a **fluent interface** - method chaining, in the same style as the native `IHealthChecksBuilder` it extends - so a full setup reads top to bottom as one continuous configuration.

```csharp
//At Startup / Program (without background services policies)
builder.Services
    //Add HealthCheckPlus - the set of tracked checks comes from whatever ends up registered
    //below (AddCheckPlus/AddCheckLinkTo/native AddCheck), no separate list to keep in sync.
    //A check added only via a native extension still needs AddCheckPlus/AddCheckLinkTo for
    //its own Healthy policy below, or the host fails fast at startup naming it.
    .AddHealthChecksPlus()
    //your custom HC    
    .AddCheckPlus<HcTeste1>("HcTest1")
    //your custom HC    
    .AddCheckPlus<HcTeste2>("HcTest2", failureStatus: HealthStatus.Degraded)
    //external HC 
    .AddRedis("connection string", "MyRedis")
    //register external HC 
    .AddCheckLinkTo("Redis", "MyRedis", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30))
    //policy for Unhealthy
    .AddUnhealthyPolicy("HcTest1", TimeSpan.FromSeconds(2))
    //policy for Degraded
    .AddDegradedPolicy("HcTest2", TimeSpan.FromSeconds(3))
    //policy for Unhealthy
    .AddUnhealthyPolicy("Redis", TimeSpan.FromSeconds(1));
```

```csharp
//At Startup / Program (with background services policies)
builder.Services
    //Add HealthCheckPlus
    .AddHealthChecksPlus()
    //your custom HC with custom delay and period   
    .AddCheckPlus<HcTeste1>("HcTest1", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10))
    //your custom HC without delay and period (using BackgroundPolicy)     
    .AddCheckPlus<HcTeste2>("HcTest2", failureStatus: HealthStatus.Degraded)
    //external HC 
    .AddRedis("connection string", "MyRedis")
    //register external HC  without delay and period (using BackgroundPolicy)
    .AddCheckLinkTo("Redis", "MyRedis")
    //policy for running in Background service
    .AddBackgroundPolicy((opt) =>
    {
        opt.Delay = TimeSpan.FromSeconds(5);
        opt.Timeout = TimeSpan.FromSeconds(30);
        opt.Idle = TimeSpan.FromSeconds(1);
        //opt.AllStatusPeriod(TimeSpan.FromSeconds(30));
        opt.HealthyPeriod = TimeSpan.FromSeconds(30);
        opt.DegradedPeriod = TimeSpan.FromSeconds(30);
        opt.UnhealthyPeriod = TimeSpan.FromSeconds(30);
        //Publishing.Enabled defaults to false - assigning a new PublishingOptions() (as below) is
        //what turns it on. AfterIdleCount/WhenReportChange below happen to match its own defaults,
        //but the assignment itself is not redundant boilerplate: deleting this block silently
        //disables all publishing, with no log or metric signal.
        opt.Publishing = new PublishingOptions() 
        { 
            AfterIdleCount = 1,
            WhenReportChange = true
        };
    });
```


```csharp
//At Startup / Program (optional)

var app = builder.Build();

//save interfaces IStateHealthChecksPlus
using (IServiceScope startscope = app.Services.CreateScope())
{
    _stateHealthChecksPlus = startscope.ServiceProvider.GetRequiredService<IStateHealthChecksPlus>();
}
```

```csharp
//At Startup / Program
//Endpoints HC
app
    //Extend HealthCheckOptions with HealthCheckPlusOptions
    .UseHealthChecksPlus("/health/live", new HealthCheckPlusOptions
    {
        //name for HealthCheck kind
        HealthCheckName = "live",
        //custom function for status value of report
        StatusHealthReport = (rep) =>
        {
            if (rep.StatusResult("HcTest1") == HealthStatus.Unhealthy)
            {
                //do something
            }
            if (rep.TryGetNotHealthy(out var results))
            {
                //do something
            }
            return HealthStatus.Degraded;
        },
        //Result Status Codes  (same behavior as HealthCheckOptions)
        ResultStatusCodes =
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status200OK,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
        }
    })
    //default HealthCheckPlusOptions (same behavior as default HealthCheckOptions)
    .UseHealthChecksPlus("/health/ready", new HealthCheckPlusOptions
    {
        //name for HealthCheck kind
        HealthCheckName = "ready",
        //template for Response (same behavior as HealthCheckOptions)
        ResponseWriter = HealthCheckPlusOptions.WriteDetailsWithoutException,
        //Result Status Codes  (same behavior as HealthCheckOptions)
        ResultStatusCodes =
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status200OK,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
        }
    });
```

```csharp
//example of use in the middleware pipeline
_ = app.Use(async (context, next) =>
{
    if (_stateHealthChecksPlus.Status("live") == HealthStatus.Unhealthy)
    {
        var msg = JsonSerializer.Serialize(new { Error = "App Unhealthy" });
        context.Response.ContentType = "application/json";
        context.Response.ContentLength = msg.Length;
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync(msg);
        await context.Response.CompleteAsync();
        return;
    }
    await next();
});
```

```csharp
//example of use in a business class using dependency injection
public class MyBusiness
{
    public MyBusiness(IStateHealthChecksPlus healthCheckApp)
    {
        if (healthCheckApp.Status("live") == HealthStatus.Degraded)
        { 
            //do something
        }
        if (healthCheckApp.StatusResult("HcTest2").Status == HealthStatus.Unhealthy)
        { 
            //do something. This dependency 'HcTest2' is not available
        }
        try
        {
            //redis access
        }
        catch (ExceptionRedis rex)
        {
            healthCheckApp.SwitchToUnhealthy("Redis");
        }
    }
}
```

```csharp
//example of  Publisher condition to execute
public class SamplePublishHealth : IHealthCheckPlusPublisher
{
   public Func<HealthReport, bool>? PublisherCondition { get; set; } = (_) => true;
   public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
   {
      return Task.CompletedTask;
   }
}
```

## Documentation
[**Top**](#table-of-contents)

- [Points of attention](./docs/POINTS_OF_ATTENTION.md) — what to know before you build on HealthCheckPlus, in plain language. Start here.
- [Architecture](./docs/ARCHITECTURE.md) — how HealthCheckPlus is put together internally, for maintainers and contributors.
- [Operational runbook](./docs/RUNBOOK.md) — how to read a health check response and diagnose common problems, for operators.
- [API reference](./docs/api/docindex.md) — generated from the XML doc comments.

## Changelog
[**Top**](#table-of-contents)

See [CHANGELOG.md](CHANGELOG.md) for the version history.

## Code of Conduct
[**Top**](#table-of-contents)

This project has adopted the code of conduct defined by the Contributor Covenant to clarify expected behavior in our community.
For more information see the [Code of Conduct](CODE_OF_CONDUCT.md).

## Contributing
[**Top**](#table-of-contents)

See the [Contributing guide](CONTRIBUTING.md) for developer documentation.

## Credits
[**Top**](#table-of-contents)

**API documentation generated by**

- [XmlDocMarkdown](https://github.com/ejball/XmlDocMarkdown), Copyright (c) 2024 [Ed Ball](https://github.com/ejball)
    - See an unrefined customization to contain header and other adjustments in project [XmlDocMarkdownGenerator](./src/XmlDocMarkdownGenerator)  
     
## License
[**Top**](#table-of-contents)

Copyright 2023 @ Fernando Cerqueira

HealthCheckPlus is licensed under the MIT license. See [LICENSE](./LICENSE).

