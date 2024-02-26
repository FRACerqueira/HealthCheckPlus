# <img align="left" width="100" height="100" src="../images/icon.png">HealthCheckPlus API:HealthReportExtensions 

[![Build](https://github.com/FRACerqueira/HealthCheckPlus/workflows/Build/badge.svg)](https://github.com/FRACerqueira/HealthCheckPlus/actions/workflows/build.yml)
[![License](https://img.shields.io/badge/License-MIT-brightgreen.svg)](https://github.com/FRACerqueira/HealthCheckPlus/blob/master/LICENSE)
[![NuGet](https://img.shields.io/nuget/v/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)
[![Downloads](https://img.shields.io/nuget/dt/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)

[**Back to List Api**](./apis.md)

# HealthReportExtensions

Namespace: HealthCheckPlus.Abstractions

The Extensions for HealthReport

```csharp
public static class HealthReportExtensions
```

Inheritance [Object](https://docs.microsoft.com/en-us/dotnet/api/system.object) → [HealthReportExtensions](./healthcheckplus.abstractions.healthreportextensions.md)

## Methods

### <a id="methods-statusresult"/>**StatusResult(HealthReport, String)**

The last  data for HealthCheck.

```csharp
public static HealthStatus StatusResult(HealthReport report, string keydep)
```

#### Parameters

`report` HealthReport<br>
The .

`keydep` [String](https://docs.microsoft.com/en-us/dotnet/api/system.string)<br>
The name dependence.

#### Returns

HealthStatus

### <a id="methods-statusresult"/>**StatusResult(HealthReport, Enum)**

The last  data for HealthCheck.

```csharp
public static HealthStatus StatusResult(HealthReport report, Enum keydep)
```

#### Parameters

`report` HealthReport<br>
The .

`keydep` [Enum](https://docs.microsoft.com/en-us/dotnet/api/system.enum)<br>
The Enum value dependence.

#### Returns

HealthStatus

### <a id="methods-trygetdegraded"/>**TryGetDegraded(HealthReport, ref Dictionary`2)**

Try get all degraded status.

```csharp
public static bool TryGetDegraded(HealthReport report, ref Dictionary`2 result)
```

#### Parameters

`report` HealthReport<br>
The .

`result` [Dictionary`2&](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2&)<br>
the Dictionary with all HealthCheck Result with degraded status

#### Returns

True if found, oyherwise false.

### <a id="methods-trygethealthy"/>**TryGetHealthy(HealthReport, ref Dictionary`2)**

Try get all healthy status.

```csharp
public static bool TryGetHealthy(HealthReport report, ref Dictionary`2 result)
```

#### Parameters

`report` HealthReport<br>
The .

`result` [Dictionary`2&](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2&)<br>
the Dictionary with all HealthCheck Result with healthy status

#### Returns

True if found, oyherwise false.

### <a id="methods-trygetnothealthy"/>**TryGetNotHealthy(HealthReport, ref Dictionary`2)**

Try get all not healthy status.

```csharp
public static bool TryGetNotHealthy(HealthReport report, ref Dictionary`2 result)
```

#### Parameters

`report` HealthReport<br>
The .

`result` [Dictionary`2&](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2&)<br>
the Dictionary with all HealthCheck Result with not healthy status

#### Returns

True if found, oyherwise false.

### <a id="methods-trygetunhealthy"/>**TryGetUnhealthy(HealthReport, ref Dictionary`2)**

Try get all unhealthy status.

```csharp
public static bool TryGetUnhealthy(HealthReport report, ref Dictionary`2 result)
```

#### Parameters

`report` HealthReport<br>
The .

`result` [Dictionary`2&](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2&)<br>
the Dictionary with all HealthCheck Result with unhealthy status

#### Returns

True if found, oyherwise false.


- - -
[**Back to List Api**](./apis.md)
