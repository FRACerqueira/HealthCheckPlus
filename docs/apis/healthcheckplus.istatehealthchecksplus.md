# <img align="left" width="100" height="100" src="../images/icon.png">HealthCheckPlus API:IStateHealthChecksPlus 

[![Build](https://github.com/FRACerqueira/HealthCheckPlus/workflows/Build/badge.svg)](https://github.com/FRACerqueira/HealthCheckPlus/actions/workflows/build.yml)
[![License](https://img.shields.io/badge/License-MIT-brightgreen.svg)](https://github.com/FRACerqueira/HealthCheckPlus/blob/master/LICENSE)
[![NuGet](https://img.shields.io/nuget/v/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)
[![Downloads](https://img.shields.io/nuget/dt/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)

[**Back to List Api**](./apis.md)

# IStateHealthChecksPlus

Namespace: HealthCheckPlus

Represents the commands of the HealthChecksPlus for access data

```csharp
public interface IStateHealthChecksPlus
```

## Properties

### <a id="properties-statusapp"/>**StatusApp**

The last  data for application.

```csharp
public abstract HealthCheckResult StatusApp { get; }
```

#### Property Value

HealthCheckResult<br>

## Methods

### <a id="methods-statusdep"/>**StatusDep(Enum)**

The last  data for dependence.

```csharp
HealthCheckResult StatusDep(Enum keydep)
```

#### Parameters

`keydep` [Enum](https://docs.microsoft.com/en-us/dotnet/api/system.enum)<br>
The Enum value dependence.

#### Returns

HealthCheckResult

### <a id="methods-swithtodegraded"/>**SwithToDegraded(Enum)**

Swith state to Degraded.

```csharp
void SwithToDegraded(Enum keydep)
```

#### Parameters

`keydep` [Enum](https://docs.microsoft.com/en-us/dotnet/api/system.enum)<br>
The Enum value dependence.

### <a id="methods-swithtounhealthy"/>**SwithToUnhealthy(Enum)**

Swith state to unhealthy.

```csharp
void SwithToUnhealthy(Enum keydep)
```

#### Parameters

`keydep` [Enum](https://docs.microsoft.com/en-us/dotnet/api/system.enum)<br>
The Enum value dependence.

### <a id="methods-trygetdegraded"/>**TryGetDegraded(ref IEnumerable`1)**

Try get all degraded status.

```csharp
bool TryGetDegraded(ref IEnumerable`1 result)
```

#### Parameters

`result` [IEnumerable`1&](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1&)<br>
[DataResutStatus](./healthcheckplus.dataresutstatus.md) degrated.

#### Returns

True if found, oyherwise false.

### <a id="methods-trygethealthy"/>**TryGetHealthy(ref IEnumerable`1)**

Try get all healthy status.

```csharp
bool TryGetHealthy(ref IEnumerable`1 result)
```

#### Parameters

`result` [IEnumerable`1&](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1&)<br>
[DataResutStatus](./healthcheckplus.dataresutstatus.md) healthy.

#### Returns

True if found, oyherwise false.

### <a id="methods-trygetnothealthy"/>**TryGetNotHealthy(ref IEnumerable`1)**

Try get all not healthy status.

```csharp
bool TryGetNotHealthy(ref IEnumerable`1 result)
```

#### Parameters

`result` [IEnumerable`1&](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1&)<br>
[DataResutStatus](./healthcheckplus.dataresutstatus.md) healthy.

#### Returns

True if found, oyherwise false.

### <a id="methods-trygetunhealthy"/>**TryGetUnhealthy(ref IEnumerable`1)**

Try get all unhealthy status.

```csharp
bool TryGetUnhealthy(ref IEnumerable`1 result)
```

#### Parameters

`result` [IEnumerable`1&](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1&)<br>
[DataResutStatus](./healthcheckplus.dataresutstatus.md) unhealthy.

#### Returns

True if found, oyherwise false.


- - -
[**Back to List Api**](./apis.md)
