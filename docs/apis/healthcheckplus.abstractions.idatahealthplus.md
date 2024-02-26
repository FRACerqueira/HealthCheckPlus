# <img align="left" width="100" height="100" src="../images/icon.png">HealthCheckPlus API:IDataHealthPlus 

[![Build](https://github.com/FRACerqueira/HealthCheckPlus/workflows/Build/badge.svg)](https://github.com/FRACerqueira/HealthCheckPlus/actions/workflows/build.yml)
[![License](https://img.shields.io/badge/License-MIT-brightgreen.svg)](https://github.com/FRACerqueira/HealthCheckPlus/blob/master/LICENSE)
[![NuGet](https://img.shields.io/nuget/v/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)
[![Downloads](https://img.shields.io/nuget/dt/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)

[**Back to List Api**](./apis.md)

# IDataHealthPlus

Namespace: HealthCheckPlus.Abstractions

Represents data from the last Health Check performed

```csharp
public interface IDataHealthPlus
```

## Properties

### <a id="properties-dateref"/>**Dateref**

The date reference of last execute.

```csharp
public abstract DateTime Dateref { get; }
```

#### Property Value

[DateTime](https://docs.microsoft.com/en-us/dotnet/api/system.datetime)<br>

### <a id="properties-duration"/>**Duration**

The time the health check took to execute.

```csharp
public abstract TimeSpan Duration { get; }
```

#### Property Value

[TimeSpan](https://docs.microsoft.com/en-us/dotnet/api/system.timespan)<br>

### <a id="properties-lastresult"/>**Lastresult**

The result, see .

```csharp
public abstract HealthCheckResult Lastresult { get; }
```

#### Property Value

HealthCheckResult<br>

### <a id="properties-name"/>**Name**

Name of health check

```csharp
public abstract string Name { get; set; }
```

#### Property Value

[String](https://docs.microsoft.com/en-us/dotnet/api/system.string)<br>

### <a id="properties-origin"/>**Origin**

The result source

```csharp
public abstract HealthCheckTrigger Origin { get; }
```

#### Property Value

[HealthCheckTrigger](./healthcheckplus.abstractions.healthchecktrigger.md)<br>


- - -
[**Back to List Api**](./apis.md)
