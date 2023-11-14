# <img align="left" width="100" height="100" src="../images/icon.png">HealthCheckPlus API:BaseHealthCheckPlus 

[![Build](https://github.com/FRACerqueira/HealthCheckPlus/workflows/Build/badge.svg)](https://github.com/FRACerqueira/HealthCheckPlus/actions/workflows/build.yml)
[![License](https://img.shields.io/badge/License-MIT-brightgreen.svg)](https://github.com/FRACerqueira/HealthCheckPlus/blob/master/LICENSE)
[![NuGet](https://img.shields.io/nuget/v/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)
[![Downloads](https://img.shields.io/nuget/dt/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)

[**Back to List Api**](./apis.md)

# BaseHealthCheckPlus

Namespace: HealthCheckPlus

Abstract class for create HealthCheck class. Inherit .

```csharp
public abstract class BaseHealthCheckPlus : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
```

Inheritance [Object](https://docs.microsoft.com/en-us/dotnet/api/system.object) → [BaseHealthCheckPlus](./healthcheckplus.basehealthcheckplus.md)<br>
Implements IHealthCheck

## Constructors

### <a id="constructors-.ctor"/>**BaseHealthCheckPlus(IServiceProvider)**

Abstract class for create HealthCheck class. Inherit .

```csharp
public BaseHealthCheckPlus(IServiceProvider serviceProvider)
```

#### Parameters

`serviceProvider` IServiceProvider<br>

## Methods

### <a id="methods-checkhealthasync"/>**CheckHealthAsync(HealthCheckContext, CancellationToken)**

Runs the method DoHealthCheck, returning the status of the component being checked.

```csharp
public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
```

#### Parameters

`context` HealthCheckContext<br>
A context object associated with the current execution.

`cancellationToken` [CancellationToken](https://docs.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken)<br>
A [CancellationToken](https://docs.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken) that can be used to cancel the health check.

#### Returns

A [Task&lt;TResult&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task-1) that completes when the health check has finished, yielding the status of the component being checked.

### <a id="methods-dohealthcheck"/>**DoHealthCheck(HealthCheckContext, CancellationToken)**

Default method DoHealthCheck : NotImplemente!

```csharp
public Task<HealthCheckResult> DoHealthCheck(HealthCheckContext context, CancellationToken cancellationToken)
```

#### Parameters

`context` HealthCheckContext<br>
A context object associated with the current execution.

`cancellationToken` [CancellationToken](https://docs.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken)<br>
A [CancellationToken](https://docs.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken) that can be used to cancel the health check.

#### Returns

A [NotImplementedException](https://docs.microsoft.com/en-us/dotnet/api/system.notimplementedexception)


- - -
[**Back to List Api**](./apis.md)
