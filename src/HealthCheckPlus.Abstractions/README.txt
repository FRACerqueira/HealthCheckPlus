HealthCheckPlus.Abstractions
****************************

Abstractions of HealthCheckPlus - the shared contract between the main package and your own code.

This package holds only the types you need to read HealthCheckPlus state and write your own
IHealthCheckPublisher - without depending on ASP.NET Core or the main HealthCheckPlus package's
registration/middleware surface. Reach for it from a class library, a background worker, or any
project that consumes health state but doesn't itself register or serve health checks.

If you're looking for AddHealthChecksPlus, UseHealthChecksPlus, AddBackgroundPolicy, or the
response-writer templates, those live in the main HealthCheckPlus package instead - installing it
already brings this one in as a dependency.

Before you build on it, see the plain-language points of attention:
https://github.com/FRACerqueira/HealthCheckPlus/blob/main/docs/POINTS_OF_ATTENTION.md

What's in this package
***********************

- IStateHealthChecksPlus - read cached check results and named aggregates (Status, StatusResult,
  TryGetHealthy/TryGetNotHealthy/TryGetDegraded/TryGetUnhealthy, ConvertToPlus), or force a manual
  override (SwitchToUnhealthy/SwitchToDegraded). Resolve it from DI in any project that also
  references the main HealthCheckPlus package, which registers the implementation.
- IDataHealthPlus - one check's last result (Name, LastResult, Duration, DateRef, Origin), the
  shape ConvertToPlus returns.
- HealthCheckTrigger - what triggered a result (UrlRequest, Background, SwitchTo, None, Default).
- IHealthCheckPlusPublisher - implement this (instead of the native IHealthCheckPublisher) to add
  an optional PublisherCondition the background service checks before invoking your publisher.
- HealthReportExtensions - convenience extensions on the native HealthReport (StatusResult,
  TryGetHealthy/TryGetNotHealthy/TryGetDegraded/TryGetUnhealthy) for code that only ever sees a
  plain HealthReport (e.g. inside an IHealthCheckPublisher), without needing IStateHealthChecksPlus
  at all.

What's new
----------

See the full changelog: https://github.com/FRACerqueira/HealthCheckPlus/blob/main/CHANGELOG.md

Usage
*****

//Reading state from anywhere IStateHealthChecksPlus can be resolved (requires the main
//HealthCheckPlus package to be registered somewhere in the same host)
public class SomeService
{
    public SomeService(IStateHealthChecksPlus state)
    {
        var overall = state.Status();               //default aggregate
        var redis = state.StatusResult("MyRedis");   //one check's last HealthCheckResult

        if (state.TryGetUnhealthy(out var unhealthy))
        {
            //do something
        }
    }
}

...

//Writing a publisher with its own opt-in condition
public class MyPublisher : IHealthCheckPlusPublisher
{
    public Func<HealthReport, bool>? PublisherCondition { get; set; } = report => report.Status != HealthStatus.Healthy;

    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        if (report.TryGetUnhealthy(out var unhealthy))
        {
            //do something
        }
        return Task.CompletedTask;
    }
}

License
*******

Copyright 2023 @ Fernando Cerqueira

HealthCheckPlus is licensed under the MIT license. See https://github.com/FRACerqueira/HealthCheckPlus/blob/main/LICENSE.
