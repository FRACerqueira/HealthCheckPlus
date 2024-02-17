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

### <a id="methods-addbackgroundpolicy"/>**AddBackgroundPolicy(IHealthChecksBuilder, Action&lt;HealthCheckPlusBackGroundOptions&gt;)**

Register HealthChecksPlus Background service with [HealthCheckPlusBackGroundOptions](./healthcheckplus.options.healthcheckplusbackgroundoptions.md) options.
 <br>Default Values:<br>Delay = 5 seconds.<br>HealthyPeriod = 30 seconds.<br>DegradedPeriod = 30 seconds.<br>UnhealthyPeriod = 30 seconds.<br>Timeout = 30 seconds.<br>Predicate = All HealthCheck.

```csharp
public static IHealthChecksBuilder AddBackgroundPolicy(IHealthChecksBuilder ihb, Action<HealthCheckPlusBackGroundOptions> option)
```

#### Parameters

`ihb` IHealthChecksBuilder<br>
The .

`option` [Action&lt;HealthCheckPlusBackGroundOptions&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.action-1)<br>
The options for HealthChecksPlus Background service. See [HealthCheckPlusBackGroundOptions](./healthcheckplus.options.healthcheckplusbackgroundoptions.md).

#### Returns

The .

### <a id="methods-addchecklinkto"/>**AddCheckLinkTo(IHealthChecksBuilder, Enum, String, Nullable&lt;TimeSpan&gt;, Nullable&lt;TimeSpan&gt;)**

Register then external(package import) dependence health check to run. the health check must added in .

```csharp
public static IHealthChecksBuilder AddCheckLinkTo(IHealthChecksBuilder ihb, Enum enumdep, string name, Nullable<TimeSpan> delay, Nullable<TimeSpan> period)
```

#### Parameters

`ihb` IHealthChecksBuilder<br>
The .

`enumdep` [Enum](https://docs.microsoft.com/en-us/dotnet/api/system.enum)<br>
The enum value with health check list to run.

`name` [String](https://docs.microsoft.com/en-us/dotnet/api/system.string)<br>
The name health check registered. This param is case insensitive

`delay` [Nullable&lt;TimeSpan&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.nullable-1)<br>
An optional [TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan). The initial delay applied after the application starts before executing
  instances. The delay is applied once at startup, and does
 not apply to subsequent iterations. The default value is 5 seconds.

`period` [Nullable&lt;TimeSpan&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.nullable-1)<br>
An optional [TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan). The period of  execution. The default value is 30 seconds

#### Returns

The .

**Remarks:**

The [HealthChecksPlusExtension.AddCheckLinkTo(IHealthChecksBuilder, Enum, String, Nullable&lt;TimeSpan&gt;, Nullable&lt;TimeSpan&gt;)](./microsoft.extensions.dependencyinjection.healthchecksplusextension.md#addchecklinktoihealthchecksbuilder-enum-string-nullabletimespan-nullabletimespan) cannot be set to a period value lower than 1 second.

### <a id="methods-addcheckplus"/>**AddCheckPlus&lt;T&gt;(IHealthChecksBuilder, Enum, Nullable&lt;TimeSpan&gt;, Nullable&lt;TimeSpan&gt;, IEnumerable&lt;String&gt;, Nullable&lt;HealthStatus&gt;, Nullable&lt;TimeSpan&gt;)**

Register then dependence health check to run.

```csharp
public static IHealthChecksBuilder AddCheckPlus<T>(IHealthChecksBuilder ihb, Enum enumdep, Nullable<TimeSpan> delay, Nullable<TimeSpan> period, IEnumerable<String> tags, Nullable<HealthStatus> failureStatus, Nullable<TimeSpan> timeout)
```

#### Type Parameters

`T`<br>

#### Parameters

`ihb` IHealthChecksBuilder<br>
The .

`enumdep` [Enum](https://docs.microsoft.com/en-us/dotnet/api/system.enum)<br>
The enum value with health check list to run.

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

**Remarks:**

The [HealthChecksPlusExtension.AddCheckPlus&lt;T&gt;(IHealthChecksBuilder, Enum, Nullable&lt;TimeSpan&gt;, Nullable&lt;TimeSpan&gt;, IEnumerable&lt;String&gt;, Nullable&lt;HealthStatus&gt;, Nullable&lt;TimeSpan&gt;)](./microsoft.extensions.dependencyinjection.healthchecksplusextension.md#addcheckplustihealthchecksbuilder-enum-nullabletimespan-nullabletimespan-ienumerablestring-nullablehealthstatus-nullabletimespan) cannot be set to a period value lower than 1 second.

### <a id="methods-adddegradedpolicy"/>**AddDegradedPolicy(IHealthChecksBuilder, Enum, TimeSpan)**

Register Degraded Policy for the health check

```csharp
public static IHealthChecksBuilder AddDegradedPolicy(IHealthChecksBuilder ihb, Enum enumdep, TimeSpan period)
```

#### Parameters

`ihb` IHealthChecksBuilder<br>
The .

`enumdep` [Enum](https://docs.microsoft.com/en-us/dotnet/api/system.enum)<br>
The enum value with health check to run.

`period` [TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan)<br>
Requeried [TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan). The period of execution when status is Unhealthy.

#### Returns

The .

**Remarks:**

The [HealthChecksPlusExtension.AddDegradedPolicy(IHealthChecksBuilder, Enum, TimeSpan)](./microsoft.extensions.dependencyinjection.healthchecksplusextension.md#adddegradedpolicyihealthchecksbuilder-enum-timespan) cannot be set to a value lower than 1 second.

### <a id="methods-addhealthchecksplus"/>**AddHealthChecksPlus&lt;T&gt;(IServiceCollection)**

Register HealthChecksPlus Service

```csharp
public static IHealthChecksBuilder AddHealthChecksPlus<T>(IServiceCollection sc)
```

#### Type Parameters

`T`<br>
Type Enum with all HealthChecks

#### Parameters

`sc` IServiceCollection<br>
The .

#### Returns

The .

### <a id="methods-addunhealthypolicy"/>**AddUnhealthyPolicy(IHealthChecksBuilder, Enum, TimeSpan)**

Register Unhealthy Policy for the health check

```csharp
public static IHealthChecksBuilder AddUnhealthyPolicy(IHealthChecksBuilder ihb, Enum enumdep, TimeSpan period)
```

#### Parameters

`ihb` IHealthChecksBuilder<br>
The .

`enumdep` [Enum](https://docs.microsoft.com/en-us/dotnet/api/system.enum)<br>
The enum value with health check to run.

`period` [TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan)<br>
Requeried [TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan) The period of execution when status is Unhealthy.

#### Returns

The .

**Remarks:**

The [HealthChecksPlusExtension.AddUnhealthyPolicy(IHealthChecksBuilder, Enum, TimeSpan)](./microsoft.extensions.dependencyinjection.healthchecksplusextension.md#addunhealthypolicyihealthchecksbuilder-enum-timespan) cannot be set to a value lower than 1 second.


- - -
[**Back to List Api**](./apis.md)
