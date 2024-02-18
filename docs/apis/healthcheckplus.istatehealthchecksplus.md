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

## Methods

### <a id="methods-converttoplus"/>**ConvertToPlus(HealthReport)**

Convert  to [IDataHealthPlus](./healthcheckplus.idatahealthplus.md)

```csharp
IEnumerable<IDataHealthPlus> ConvertToPlus(HealthReport report)
```

#### Parameters

`report` HealthReport<br>
The report

#### Returns

[IEnumerable&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1)

### <a id="methods-status"/>**Status(String)**

Gets a  representing the aggregate status of all the health checks.

```csharp
HealthStatus Status(string name)
```

#### Parameters

`name` [String](https://docs.microsoft.com/en-us/dotnet/api/system.string)<br>

#### Returns

HealthStatus

### <a id="methods-statusdep"/>**StatusDep(String)**

The last  data for HealthCheck.

```csharp
HealthCheckResult StatusDep(string keydep)
```

#### Parameters

`keydep` [String](https://docs.microsoft.com/en-us/dotnet/api/system.string)<br>
The Enum value dependence.

#### Returns

HealthCheckResult

### <a id="methods-swithtodegraded"/>**SwithToDegraded(String)**

Swith state to Degraded.

```csharp
void SwithToDegraded(string keydep)
```

#### Parameters

`keydep` [String](https://docs.microsoft.com/en-us/dotnet/api/system.string)<br>
The name value dependence.

### <a id="methods-swithtounhealthy"/>**SwithToUnhealthy(String)**

Swith state to unhealthy.

```csharp
void SwithToUnhealthy(string keydep)
```

#### Parameters

`keydep` [String](https://docs.microsoft.com/en-us/dotnet/api/system.string)<br>
The name value dependence.

### <a id="methods-trygetdegraded"/>**TryGetDegraded(ref IEnumerable`1)**

Try get all degraded status.

```csharp
bool TryGetDegraded(ref IEnumerable`1 result)
```

#### Parameters

`result` [IEnumerable`1&](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1&)<br>
[IEnumerable&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1) degrated.

#### Returns

True if found, oyherwise false.

### <a id="methods-trygethealthy"/>**TryGetHealthy(ref IEnumerable`1)**

Try get all healthy status.

```csharp
bool TryGetHealthy(ref IEnumerable`1 result)
```

#### Parameters

`result` [IEnumerable`1&](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1&)<br>
[IEnumerable&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1) healthy.

#### Returns

True if found, oyherwise false.

### <a id="methods-trygetnothealthy"/>**TryGetNotHealthy(ref IEnumerable`1)**

Try get all not healthy status.

```csharp
bool TryGetNotHealthy(ref IEnumerable`1 result)
```

#### Parameters

`result` [IEnumerable`1&](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1&)<br>
[IEnumerable&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1) healthy.

#### Returns

True if found, oyherwise false.

### <a id="methods-trygetunhealthy"/>**TryGetUnhealthy(ref IEnumerable`1)**

Try get all unhealthy status.

```csharp
bool TryGetUnhealthy(ref IEnumerable`1 result)
```

#### Parameters

`result` [IEnumerable`1&](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1&)<br>
[IEnumerable&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1) unhealthy.

#### Returns

True if found, oyherwise false.


- - -
[**Back to List Api**](./apis.md)
