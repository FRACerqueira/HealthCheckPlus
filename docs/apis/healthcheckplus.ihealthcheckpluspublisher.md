# <img align="left" width="100" height="100" src="../images/icon.png">HealthCheckPlus API:IHealthCheckPlusPublisher 

[![Build](https://github.com/FRACerqueira/HealthCheckPlus/workflows/Build/badge.svg)](https://github.com/FRACerqueira/HealthCheckPlus/actions/workflows/build.yml)
[![License](https://img.shields.io/badge/License-MIT-brightgreen.svg)](https://github.com/FRACerqueira/HealthCheckPlus/blob/master/LICENSE)
[![NuGet](https://img.shields.io/nuget/v/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)
[![Downloads](https://img.shields.io/nuget/dt/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)

[**Back to List Api**](./apis.md)

# IHealthCheckPlusPublisher

Namespace: HealthCheckPlus

Represents a publisher of  information.

```csharp
public interface IHealthCheckPlusPublisher
```

## Properties

### <a id="properties-publishercondition"/>**PublisherCondition**

The Publisher Condition to execute. Default value is null (always run)

```csharp
public abstract Func<HealthReport, Boolean> PublisherCondition { get; }
```

#### Property Value

[Func&lt;HealthReport, Boolean&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.func-2)<br>


- - -
[**Back to List Api**](./apis.md)
