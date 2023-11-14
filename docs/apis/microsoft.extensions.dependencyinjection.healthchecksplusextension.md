# <img align="left" width="100" height="100" src="../images/icon.png">HealthCheckPlus API:HealthChecksPlusExtension 

[![Build](https://github.com/FRACerqueira/HealthCheckPlus/workflows/Build/badge.svg)](https://github.com/FRACerqueira/HealthCheckPlus/actions/workflows/build.yml)
[![License](https://img.shields.io/badge/License-MIT-brightgreen.svg)](https://github.com/FRACerqueira/HealthCheckPlus/blob/master/LICENSE)
[![NuGet](https://img.shields.io/nuget/v/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)
[![Downloads](https://img.shields.io/nuget/dt/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)

[**Back to List Api**](./apis.md)

# HealthChecksPlusExtension

Namespace: Microsoft.Extensions.DependencyInjection

HealthChecksPlus Extension for DependencyInjection

```csharp
public static class HealthChecksPlusExtension
```

Inheritance [Object](https://docs.microsoft.com/en-us/dotnet/api/system.object) → [HealthChecksPlusExtension](./microsoft.extensions.dependencyinjection.healthchecksplusextension.md)

## Methods

### <a id="methods-addcheckplus"/>**AddCheckPlus&lt;TE, T&gt;(IHealthChecksBuilder, TE, Nullable&lt;TimeSpan&gt;, Nullable&lt;TimeSpan&gt;, IEnumerable&lt;String&gt;, Nullable&lt;HealthStatus&gt;, Nullable&lt;TimeSpan&gt;)**

Register then dependence health check to run. the class health check class must inherit from [BaseHealthCheckPlus](./healthcheckplus.basehealthcheckplus.md).

```csharp
public static IHealthChecksBuilder AddCheckPlus<TE, T>(IHealthChecksBuilder ihb, TE enumdep, Nullable<TimeSpan> delay, Nullable<TimeSpan> period, IEnumerable<String> tags, Nullable<HealthStatus> failureStatus, Nullable<TimeSpan> timeout)
```

#### Type Parameters

`TE`<br>

`T`<br>

#### Parameters

`ihb` IHealthChecksBuilder<br>
The .

`enumdep` TE<br>
The [Enum](https://docs.microsoft.com/en-us/dotnet/api/system.enum) with health check list to run.

`delay` [Nullable&lt;TimeSpan&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.nullable-1)<br>
An optional [TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan). The initial delay applied after the application starts before executing
  instances. The delay is applied once at startup, and does
 not apply to subsequent iterations. The default value is 5 seconds.

`period` [Nullable&lt;TimeSpan&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.nullable-1)<br>
An optional [TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan). The period of  execution. The default value is 30 seconds

`tags` [IEnumerable&lt;String&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1)<br>
A list of tags that can be used for filtering health checks.

`failureStatus` [Nullable&lt;HealthStatus&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.nullable-1)<br>
The  that should be reported when the health check reports a failure. If the provided value
 is `null`, then  will be reported.

`timeout` [Nullable&lt;TimeSpan&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.nullable-1)<br>
An optional [TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan) representing the timeout of the check.

#### Returns

The .

### <a id="methods-addcheckregistered"/>**AddCheckRegistered&lt;TE&gt;(IHealthChecksBuilder, TE, String, Nullable&lt;TimeSpan&gt;, Nullable&lt;TimeSpan&gt;, IEnumerable&lt;String&gt;, Nullable&lt;HealthStatus&gt;, Nullable&lt;TimeSpan&gt;)**

Register then external(package import) dependence health check to run. the health check must added in .

```csharp
public static IHealthChecksBuilder AddCheckRegistered<TE>(IHealthChecksBuilder ihb, TE enumdep, string name, Nullable<TimeSpan> delay, Nullable<TimeSpan> period, IEnumerable<String> tags, Nullable<HealthStatus> failureStatus, Nullable<TimeSpan> timeout)
```

#### Type Parameters

`TE`<br>

#### Parameters

`ihb` IHealthChecksBuilder<br>
The .

`enumdep` TE<br>
The [Enum](https://docs.microsoft.com/en-us/dotnet/api/system.enum) with health check list to run.

`name` [String](https://docs.microsoft.com/en-us/dotnet/api/system.string)<br>
The name health check registered. This param is case insensitive

`delay` [Nullable&lt;TimeSpan&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.nullable-1)<br>
An optional [TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan). The initial delay applied after the application starts before executing
  instances. The delay is applied once at startup, and does
 not apply to subsequent iterations. The default value is 5 seconds.

`period` [Nullable&lt;TimeSpan&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.nullable-1)<br>
An optional [TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan). The period of  execution. The default value is 30 seconds

`tags` [IEnumerable&lt;String&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1)<br>
A list of tags that can be used for filtering health checks.

`failureStatus` [Nullable&lt;HealthStatus&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.nullable-1)<br>
The  that should be reported when the health check reports a failure. If the provided value
 is `null`, then  will be reported.

`timeout` [Nullable&lt;TimeSpan&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.nullable-1)<br>
An optional [TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan) representing the timeout of the check.

#### Returns

The .

### <a id="methods-adddegradedpolicy"/>**AddDegradedPolicy&lt;T&gt;(IHealthChecksBuilder, T, TimeSpan)**

Register Degraded Policy for the health check

```csharp
public static IHealthChecksBuilder AddDegradedPolicy<T>(IHealthChecksBuilder ihb, T enumdep, TimeSpan period)
```

#### Type Parameters

`T`<br>

#### Parameters

`ihb` IHealthChecksBuilder<br>
The .

`enumdep` T<br>
The [Enum](https://docs.microsoft.com/en-us/dotnet/api/system.enum) with health check to run.

`period` [TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan)<br>
Requeried [TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan). The period of execution when status is Unhealthy.

#### Returns

The .

### <a id="methods-addhealthchecks"/>**AddHealthChecks&lt;T&gt;(IServiceCollection, String, HealthStatus, String, Action&lt;ILogger, DataResutStatus&gt;)**

Register Aplication health check

```csharp
public static IHealthChecksBuilder AddHealthChecks<T>(IServiceCollection sc, string name, HealthStatus failureStatus, string categorylog, Action<ILogger, DataResutStatus> actionlog)
```

#### Type Parameters

`T`<br>
Enum dependences

#### Parameters

`sc` IServiceCollection<br>
The .

`name` [String](https://docs.microsoft.com/en-us/dotnet/api/system.string)<br>
The health check name. Requeried.

`failureStatus` HealthStatus<br>
The  that should be reported when the health check reports a failure. If the provided value
 is `null`, then  will be reported.

`categorylog` [String](https://docs.microsoft.com/en-us/dotnet/api/system.string)<br>
An optional category name for logger.

`actionlog` [Action&lt;ILogger, DataResutStatus&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.action-2)<br>
An optional action to write log.

#### Returns

The .

### <a id="methods-addhealthchecks"/>**AddHealthChecks&lt;T&gt;(IServiceCollection, String, Func&lt;IStateHealthChecksPlus, HealthStatus&gt;, String, Action&lt;ILogger, DataResutStatus&gt;)**

Register Aplication health check

```csharp
public static IHealthChecksBuilder AddHealthChecks<T>(IServiceCollection sc, string name, Func<IStateHealthChecksPlus, HealthStatus> failureStatus, string categorylog, Action<ILogger, DataResutStatus> actionlog)
```

#### Type Parameters

`T`<br>
Enum dependences

#### Parameters

`sc` IServiceCollection<br>
The .

`name` [String](https://docs.microsoft.com/en-us/dotnet/api/system.string)<br>
The health check name. Requeried.

`failureStatus` [Func&lt;IStateHealthChecksPlus, HealthStatus&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.func-2)<br>
The user function to reports  .

`categorylog` [String](https://docs.microsoft.com/en-us/dotnet/api/system.string)<br>
An optional category name for logger.

`actionlog` [Action&lt;ILogger, DataResutStatus&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.action-2)<br>
An optional action to write log.

#### Returns

The .

### <a id="methods-addunhealthypolicy"/>**AddUnhealthyPolicy&lt;T&gt;(IHealthChecksBuilder, T, TimeSpan)**

Register Unhealthy Policy for the health check

```csharp
public static IHealthChecksBuilder AddUnhealthyPolicy<T>(IHealthChecksBuilder ihb, T enumdep, TimeSpan period)
```

#### Type Parameters

`T`<br>

#### Parameters

`ihb` IHealthChecksBuilder<br>
The .

`enumdep` T<br>
The [Enum](https://docs.microsoft.com/en-us/dotnet/api/system.enum) with health check to run.

`period` [TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan)<br>
Requeried [TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan). The period of execution when status is Unhealthy.

#### Returns

The .


- - -
[**Back to List Api**](./apis.md)
