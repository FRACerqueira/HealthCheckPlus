# <img align="left" width="100" height="100" src="../images/icon.png">HealthCheckPlus API:HealthCheckPlusOptions 

[![Build](https://github.com/FRACerqueira/HealthCheckPlus/workflows/Build/badge.svg)](https://github.com/FRACerqueira/HealthCheckPlus/actions/workflows/build.yml)
[![License](https://img.shields.io/badge/License-MIT-brightgreen.svg)](https://github.com/FRACerqueira/HealthCheckPlus/blob/master/LICENSE)
[![NuGet](https://img.shields.io/nuget/v/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)
[![Downloads](https://img.shields.io/nuget/dt/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)

[**Back to List Api**](./apis.md)

# HealthCheckPlusOptions

Namespace: HealthCheckPlus.options

Contains optionsSerilz for the .

```csharp
public class HealthCheckPlusOptions : Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
```

Inheritance [Object](https://docs.microsoft.com/en-us/dotnet/api/system.object) → HealthCheckOptions → [HealthCheckPlusOptions](./healthcheckplus.options.healthcheckplusoptions.md)

## Properties

### <a id="properties-allowcachingresponses"/>**AllowCachingResponses**

```csharp
public bool AllowCachingResponses { get; set; }
```

#### Property Value

[Boolean](https://docs.microsoft.com/en-us/dotnet/api/system.boolean)<br>

### <a id="properties-healthcheckname"/>**HealthCheckName**

Gets or sets the name of Agregate Status for HealthReport.

```csharp
public string HealthCheckName { get; set; }
```

#### Property Value

[String](https://docs.microsoft.com/en-us/dotnet/api/system.string)<br>

### <a id="properties-predicate"/>**Predicate**

```csharp
public Func<HealthCheckRegistration, Boolean> Predicate { get; set; }
```

#### Property Value

[Func&lt;HealthCheckRegistration, Boolean&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.func-2)<br>

### <a id="properties-responsewriter"/>**ResponseWriter**

```csharp
public Func<HttpContext, HealthReport, Task> ResponseWriter { get; set; }
```

#### Property Value

[Func&lt;HttpContext, HealthReport, Task&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.func-3)<br>

### <a id="properties-resultstatuscodes"/>**ResultStatusCodes**

```csharp
public IDictionary<HealthStatus, Int32> ResultStatusCodes { get; }
```

#### Property Value

[IDictionary&lt;HealthStatus, Int32&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.idictionary-2)<br>

### <a id="properties-statushealthreport"/>**StatusHealthReport**

Gets or sets the Agregate Status for HealthReport. Default value is min() of all status reported

```csharp
public Func<HealthReport, HealthStatus> StatusHealthReport { get; set; }
```

#### Property Value

[Func&lt;HealthReport, HealthStatus&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.func-2)<br>

## Constructors

### <a id="constructors-.ctor"/>**HealthCheckPlusOptions()**

```csharp
public HealthCheckPlusOptions()
```

## Methods

### <a id="methods-writedetailswithexception"/>**WriteDetailsWithException(HttpContext, HealthReport)**

Response template with small details of the HealthCheck report (with exception)

```csharp
public static Task WriteDetailsWithException(HttpContext context, HealthReport report)
```

#### Parameters

`context` HttpContext<br>
The .

`report` HealthReport<br>
The .

#### Returns

[Task](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task).

### <a id="methods-writedetailswithexceptionplus"/>**WriteDetailsWithExceptionPlus(HttpContext, HealthReport, IStateHealthChecksPlus)**

Response template with small details of the HealthCheck report (with exception), cache source and reference date of last run

```csharp
public static Task WriteDetailsWithExceptionPlus(HttpContext context, HealthReport report, IStateHealthChecksPlus statecache)
```

#### Parameters

`context` HttpContext<br>
The .

`report` HealthReport<br>
The .

`statecache` IStateHealthChecksPlus<br>
The cache instance : [IStateHealthChecksPlus](./healthcheckplus.abstractions.istatehealthchecksplus.md).


#### Returns

[Task](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task).

### <a id="methods-writedetailswithoutexception"/>**WriteDetailsWithoutException(HttpContext, HealthReport)**

Response template with details of the HealthCheck report (without exception)

```csharp
public static Task WriteDetailsWithoutException(HttpContext context, HealthReport report)
```

#### Parameters

`context` HttpContext<br>
The .

`report` HealthReport<br>
The .

#### Returns

[Task](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task).

### <a id="methods-writedetailswithoutexceptionplus"/>**WriteDetailsWithoutExceptionPlus(HttpContext, HealthReport, IStateHealthChecksPlus)**

Response template with small details of the HealthCheck report (without exception), cache source and reference date of last run

```csharp
public static Task WriteDetailsWithoutExceptionPlus(HttpContext context, HealthReport report, IStateHealthChecksPlus statecache)
```

#### Parameters

`context` HttpContext<br>
The .

`report` HealthReport<br>
The .

`statecache` IStateHealthChecksPlus<br>
The cache instance : [IStateHealthChecksPlus](./healthcheckplus.istatehealthchecksplus.md).

#### Returns

[Task](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task).

### <a id="methods-writeshortdetails"/>**WriteShortDetails(HttpContext, HealthReport)**

Response template with small details of the HealthCheck report

```csharp
public static Task WriteShortDetails(HttpContext context, HealthReport report)
```

#### Parameters

`context` HttpContext<br>
The .

`report` HealthReport<br>
The .

#### Returns

[Task](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task).

### <a id="methods-writeshortdetailsplus"/>**WriteShortDetailsPlus(HttpContext, HealthReport, IStateHealthChecksPlus)**

Response template with small details of the HealthCheck report, cache source and reference date of last run

```csharp
public static Task WriteShortDetailsPlus(HttpContext context, HealthReport report, IStateHealthChecksPlus statecache)
```

#### Parameters

`context` HttpContext<br>
The .

`report` HealthReport<br>
The .

`statecache` IStateHealthChecksPlus<br>
The cache instance : [IStateHealthChecksPlus](./healthcheckplus.istatehealthchecksplus.md).

#### Returns

[Task](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task).


- - -
[**Back to List Api**](./apis.md)
