# <img align="left" width="100" height="100" src="../images/icon.png">HealthCheckPlus API:IStateHealthChecksPlus 

[![Build](https://github.com/FRACerqueira/HealthCheckPlus/workflows/Build/badge.svg)](https://github.com/FRACerqueira/HealthCheckPlus/actions/workflows/build.yml)
[![License](https://img.shields.io/badge/License-MIT-brightgreen.svg)](https://github.com/FRACerqueira/HealthCheckPlus/blob/master/LICENSE)
[![NuGet](https://img.shields.io/nuget/v/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)
[![Downloads](https://img.shields.io/nuget/dt/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)

[**Back to List Api**](./apis.md)

# IStateHealthChecksPlus

Namespace: HealthCheckPlus

HealthCheckPlus: Public interface for access data  Application and Dependences.

```csharp
public interface IStateHealthChecksPlus
```

## Properties

### <a id="properties-statusapp"/>**StatusApp**

HealthCheckPlus:  data Application.

```csharp
public abstract HealthCheckResult StatusApp { get; }
```

#### Property Value

HealthCheckResult<br>

## Methods

### <a id="methods-statusdep"/>**StatusDep(Enum)**

HealthCheckPlus:  data Dependence.

```csharp
HealthCheckResult StatusDep(Enum keydep)
```

#### Parameters

`keydep` [Enum](https://docs.microsoft.com/en-us/dotnet/api/system.enum)<br>
HealthCheckPlus: Enum HealthCheck value Dependence

#### Returns

HealthCheckResult

### <a id="methods-swithtodegraded"/>**SwithToDegraded(Enum)**

HealthCheckPlus: Swith state to Degraded

```csharp
void SwithToDegraded(Enum keydep)
```

#### Parameters

`keydep` [Enum](https://docs.microsoft.com/en-us/dotnet/api/system.enum)<br>
HealthCheckPlus: Enum HealthCheck value Dependence

### <a id="methods-swithtounhealthy"/>**SwithToUnhealthy(Enum)**

HealthCheckPlus: Swith state to unhealthy

```csharp
void SwithToUnhealthy(Enum keydep)
```

#### Parameters

`keydep` [Enum](https://docs.microsoft.com/en-us/dotnet/api/system.enum)<br>
HealthCheckPlus: Enum HealthCheck value Dependence


- - -
[**Back to List Api**](./apis.md)
