# <img align="left" width="100" height="100" src="../images/icon.png">HealthCheckPlus API:HealthCheckPlusBackGroundOptions 

[![Build](https://github.com/FRACerqueira/HealthCheckPlus/workflows/Build/badge.svg)](https://github.com/FRACerqueira/HealthCheckPlus/actions/workflows/build.yml)
[![License](https://img.shields.io/badge/License-MIT-brightgreen.svg)](https://github.com/FRACerqueira/HealthCheckPlus/blob/master/LICENSE)
[![NuGet](https://img.shields.io/nuget/v/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)
[![Downloads](https://img.shields.io/nuget/dt/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)

[**Back to List Api**](./apis.md)

# HealthCheckPlusBackGroundOptions

Namespace: HealthCheckPlus.options

Options for the HealthCheckPlus background service instance.

```csharp
public class HealthCheckPlusBackGroundOptions
```

Inheritance [Object](https://docs.microsoft.com/en-us/dotnet/api/system.object) → [HealthCheckPlusBackGroundOptions](./healthcheckplus.options.healthcheckplusbackgroundoptions.md)

## Properties

### <a id="properties-degradedperiod"/>**DegradedPeriod**

Gets or sets the period when HealthCheck's period property is null and last status is of . The default value is
 30 seconds.

```csharp
public TimeSpan DegradedPeriod { get; set; }
```

#### Property Value

[TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan)<br>

**Remarks:**

The [HealthCheckPlusBackGroundOptions.DegradedPeriod](./healthcheckplus.options.healthcheckplusbackgroundoptions.md#degradedperiod) cannot be set to a value lower than 1 second.

### <a id="properties-delay"/>**Delay**

Gets or sets the initial delay applied after the application starts before executing
 HealthCheckPlus background service. The delay is applied once at startup, and does
 not apply to subsequent iterations. The default value is 5 seconds.

```csharp
public TimeSpan Delay { get; set; }
```

#### Property Value

[TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan)<br>

### <a id="properties-healthyperiod"/>**HealthyPeriod**

Gets or sets the period when HealthCheck's period property is null and last status is of . The default value is
 30 seconds.

```csharp
public TimeSpan HealthyPeriod { get; set; }
```

#### Property Value

[TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan)<br>

**Remarks:**

The [HealthCheckPlusBackGroundOptions.HealthyPeriod](./healthcheckplus.options.healthcheckplusbackgroundoptions.md#healthyperiod) cannot be set to a value lower than 1 second.

### <a id="properties-idle"/>**Idle**

Gets or sets the idle after try execute HealthChecks on background service. The default value is
 1 seconds.

```csharp
public TimeSpan Idle { get; set; }
```

#### Property Value

[TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan)<br>

**Remarks:**

The [HealthCheckPlusBackGroundOptions.Idle](./healthcheckplus.options.healthcheckplusbackgroundoptions.md#idle) cannot be set to a value lower than 1 second.

### <a id="properties-predicate"/>**Predicate**

Gets or sets a predicate that is used to filter the set of health checks executed.

```csharp
public Func<HealthCheckRegistration, Boolean> Predicate { get; set; }
```

#### Property Value

[Func&lt;HealthCheckRegistration, Boolean&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.func-2)<br>

**Remarks:**

If [HealthCheckPlusBackGroundOptions.Predicate](./healthcheckplus.options.healthcheckplusbackgroundoptions.md#predicate) is `null`, will run all
 registered health checks - this is the default behavior. To run a subset of health checks,
 provide a function that filters the set of checks.

### <a id="properties-timeout"/>**Timeout**

Gets or sets the timeout for executing the health checks an all HealthCheckPlus background service.
 Use [Timeout.InfiniteTimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.threading.timeout.infinitetimespan) to execute with no timeout.
 The default value is 30 seconds.

```csharp
public TimeSpan Timeout { get; set; }
```

#### Property Value

[TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan)<br>

**Remarks:**

The [HealthCheckPlusBackGroundOptions.Timeout](./healthcheckplus.options.healthcheckplusbackgroundoptions.md#timeout) cannot be set to a value lower than 1 second.

### <a id="properties-unhealthyperiod"/>**UnhealthyPeriod**

Gets or sets the period when HealthCheck's period property is null and last status is of . The default value is
 30 seconds.

```csharp
public TimeSpan UnhealthyPeriod { get; set; }
```

#### Property Value

[TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan)<br>

**Remarks:**

The [HealthCheckPlusBackGroundOptions.UnhealthyPeriod](./healthcheckplus.options.healthcheckplusbackgroundoptions.md#unhealthyperiod) cannot be set to a value lower than 1 second.

## Constructors

### <a id="constructors-.ctor"/>**HealthCheckPlusBackGroundOptions()**

Creates a new instance of [HealthCheckPlusBackGroundOptions](./healthcheckplus.options.healthcheckplusbackgroundoptions.md).
 <br>Default Values:<br>Delay = 5 seconds.<br>HealthyPeriod = 30 seconds.<br>DegradedPeriod = 30 seconds.<br>UnhealthyPeriod = 30 seconds.<br>Timeout = 30 seconds.<br>Predicate = All HealthCheck.

```csharp
public HealthCheckPlusBackGroundOptions()
```

## Methods

### <a id="methods-allstatusperiod"/>**AllStatusPeriod(TimeSpan)**

Sets the all periods when HealthCheck's period property is null. See : [HealthCheckPlusBackGroundOptions.HealthyPeriod](./healthcheckplus.options.healthcheckplusbackgroundoptions.md#healthyperiod), [HealthCheckPlusBackGroundOptions.DegradedPeriod](./healthcheckplus.options.healthcheckplusbackgroundoptions.md#degradedperiod), [HealthCheckPlusBackGroundOptions.UnhealthyPeriod](./healthcheckplus.options.healthcheckplusbackgroundoptions.md#unhealthyperiod). The default value is
 30 seconds.

```csharp
public void AllStatusPeriod(TimeSpan value)
```

#### Parameters

`value` [TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan)<br>

**Remarks:**

The [HealthCheckPlusBackGroundOptions.AllStatusPeriod(TimeSpan)](./healthcheckplus.options.healthcheckplusbackgroundoptions.md#allstatusperiodtimespan) cannot be set to a value lower than 1 second.


- - -
[**Back to List Api**](./apis.md)
