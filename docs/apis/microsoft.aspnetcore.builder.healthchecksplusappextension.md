# <img align="left" width="100" height="100" src="../images/icon.png">HealthCheckPlus API:HealthChecksPlusAppExtension 

[![Build](https://github.com/FRACerqueira/HealthCheckPlus/workflows/Build/badge.svg)](https://github.com/FRACerqueira/HealthCheckPlus/actions/workflows/build.yml)
[![License](https://img.shields.io/badge/License-MIT-brightgreen.svg)](https://github.com/FRACerqueira/HealthCheckPlus/blob/master/LICENSE)
[![NuGet](https://img.shields.io/nuget/v/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)
[![Downloads](https://img.shields.io/nuget/dt/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)

[**Back to List Api**](./apis.md)

# HealthChecksPlusAppExtension

Namespace: Microsoft.AspNetCore.Builder

HealthChecksPlus Extension for IApplicationBuilder

```csharp
public static class HealthChecksPlusAppExtension
```

Inheritance [Object](https://docs.microsoft.com/en-us/dotnet/api/system.object) → [HealthChecksPlusAppExtension](./microsoft.aspnetcore.builder.healthchecksplusappextension.md)

## Methods

### <a id="methods-usehealthchecksplus"/>**UseHealthChecksPlus(IApplicationBuilder, PathString, HealthCheckOptions)**

HealthcheckPlus : Adds a middleware that provides health check status.

```csharp
public static IApplicationBuilder UseHealthChecksPlus(IApplicationBuilder app, PathString path, HealthCheckOptions options)
```

#### Parameters

`app` IApplicationBuilder<br>
The .

`path` PathString<br>
The The path on which to provide health check status.

`options` HealthCheckOptions<br>
The  used to configure.

#### Returns

The .

**Remarks:**

If path is set to null or the empty string then the health check middleware will
 ignore the URL path and process all requests. If path is set to a non-empty value,
 the health check middleware will process requests with a URL that matches the
 provided value of path case-insensitively, allowing for an extra trailing slash
 ('/') character.

### <a id="methods-usehealthchecksplus"/>**UseHealthChecksPlus(IApplicationBuilder, PathString, HttpStatusCode, HttpStatusCode, HttpStatusCode)**

HealthcheckPlus : Adds a middleware that provides health check status.

```csharp
public static IApplicationBuilder UseHealthChecksPlus(IApplicationBuilder app, PathString path, HttpStatusCode UnhealthySta, HttpStatusCode DegradedSta, HttpStatusCode HealthySta)
```

#### Parameters

`app` IApplicationBuilder<br>
The .

`path` PathString<br>
The The path on which to provide health check status.

`UnhealthySta` HttpStatusCode<br>
The  used when application status is Unhealthy. Defaut value is 503(ServiceUnavailable)

`DegradedSta` HttpStatusCode<br>
The  used when application status is Degraded. Defaut value is 200(Ok)

`HealthySta` HttpStatusCode<br>
The  used when application status is Healthy. Defaut value is 200(Ok)

#### Returns

The .

### <a id="methods-usehealthchecksplusstatus"/>**UseHealthChecksPlusStatus(IApplicationBuilder, PathString, HttpStatusCode, HttpStatusCode, HttpStatusCode, Func&lt;HttpContext, HealthReport, Task&gt;)**

HealthcheckPlus : Adds a middleware that provides health check status with default json details from all health checks.

```csharp
public static IApplicationBuilder UseHealthChecksPlusStatus(IApplicationBuilder app, PathString path, HttpStatusCode UnhealthySta, HttpStatusCode DegradedSta, HttpStatusCode HealthySta, Func<HttpContext, HealthReport, Task> responseWriter)
```

#### Parameters

`app` IApplicationBuilder<br>
The .

`path` PathString<br>
The The path on which to provide health check status.

`UnhealthySta` HttpStatusCode<br>
The  used when application status is Unhealthy. Defaut value is 503(ServiceUnavailable)

`DegradedSta` HttpStatusCode<br>
The  used when application status is Degraded. Defaut value is 200(Ok)

`HealthySta` HttpStatusCode<br>
The  used when application status is Healthy. Defaut value is 200(Ok)

`responseWriter` [Func&lt;HttpContext, HealthReport, Task&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.func-3)<br>
The a delegate used to write the response

#### Returns

The .


- - -
[**Back to List Api**](./apis.md)
