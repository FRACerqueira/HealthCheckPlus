# <img align="left" width="100" height="100" src="../images/icon.png">HealthCheckPlus API:DataResutStatus 

[![Build](https://github.com/FRACerqueira/HealthCheckPlus/workflows/Build/badge.svg)](https://github.com/FRACerqueira/HealthCheckPlus/actions/workflows/build.yml)
[![License](https://img.shields.io/badge/License-MIT-brightgreen.svg)](https://github.com/FRACerqueira/HealthCheckPlus/blob/master/LICENSE)
[![NuGet](https://img.shields.io/nuget/v/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)
[![Downloads](https://img.shields.io/nuget/dt/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)

[**Back to List Api**](./apis.md)

# DataResutStatus

Namespace: HealthCheckPlus

Represents the HealthCheck result data.

```csharp
public struct DataResutStatus
```

Inheritance [Object](https://docs.microsoft.com/en-us/dotnet/api/system.object) → [ValueType](https://docs.microsoft.com/en-us/dotnet/api/system.valuetype) → [DataResutStatus](./healthcheckplus.dataresutstatus.md)

## Properties

### <a id="properties-date"/>**Date**

The date of result data.

```csharp
public Nullable<DateTime> Date { get; }
```

#### Property Value

[Nullable&lt;DateTime&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.nullable-1)<br>

### <a id="properties-description"/>**Description**

The description.

```csharp
public string Description { get; }
```

#### Property Value

[String](https://docs.microsoft.com/en-us/dotnet/api/system.string)<br>

### <a id="properties-elapsedtime"/>**ElapsedTime**

The elapsed time of HealthCheck.

```csharp
public Nullable<TimeSpan> ElapsedTime { get; }
```

#### Property Value

[Nullable&lt;TimeSpan&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.nullable-1)<br>

### <a id="properties-error"/>**Error**

The [Exception](https://docs.microsoft.com/en-us/dotnet/api/system.exception).

```csharp
public Exception Error { get; }
```

#### Property Value

[Exception](https://docs.microsoft.com/en-us/dotnet/api/system.exception)<br>

### <a id="properties-name"/>**Name**

The name of HealthCheck.

```csharp
public string Name { get; }
```

#### Property Value

[String](https://docs.microsoft.com/en-us/dotnet/api/system.string)<br>

### <a id="properties-status"/>**Status**

The .

```csharp
public HealthStatus Status { get; }
```

#### Property Value

HealthStatus<br>

## Constructors

### <a id="constructors-.ctor"/>**DataResutStatus()**

Create DataResutStatus.
 <br>Do not use this constructor!

```csharp
DataResutStatus()
```

#### Exceptions

[NotImplementedException](https://docs.microsoft.com/en-us/dotnet/api/system.notimplementedexception)<br>

### <a id="constructors-.ctor"/>**DataResutStatus(String, String, HealthStatus, Exception, Nullable&lt;DateTime&gt;, Nullable&lt;TimeSpan&gt;)**

Create DataResutStatus.

```csharp
DataResutStatus(string name, string description, HealthStatus status, Exception error, Nullable<DateTime> date, Nullable<TimeSpan> elapsedTime)
```

#### Parameters

`name` [String](https://docs.microsoft.com/en-us/dotnet/api/system.string)<br>
The name of HealthCheck.

`description` [String](https://docs.microsoft.com/en-us/dotnet/api/system.string)<br>
The description.

`status` HealthStatus<br>
The .

`error` [Exception](https://docs.microsoft.com/en-us/dotnet/api/system.exception)<br>
The [Exception](https://docs.microsoft.com/en-us/dotnet/api/system.exception).

`date` [Nullable&lt;DateTime&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.nullable-1)<br>
The date of result data.

`elapsedTime` [Nullable&lt;TimeSpan&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.nullable-1)<br>
The elapsed time of HealthCheck.


- - -
[**Back to List Api**](./apis.md)
