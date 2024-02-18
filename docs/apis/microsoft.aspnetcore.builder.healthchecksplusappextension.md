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

### <a id="methods-usehealthchecksplus"/>**UseHealthChecksPlus(IApplicationBuilder, PathString)**

Adds a middleware that provides health check status.

```csharp
public static IApplicationBuilder UseHealthChecksPlus(IApplicationBuilder app, PathString path)
```

#### Parameters

`app` IApplicationBuilder<br>
The .

`path` PathString<br>
The path on which to provide health check status.

#### Returns

A reference to the  after the operation has completed.

**Remarks:**

If  is set to `null` or the empty string then the health check middleware
 will ignore the URL path and process all requests. If  is set to a non-empty
 value, the health check middleware will process requests with a URL that matches the provided value
 of  case-insensitively, allowing for an extra trailing slash ('/') character.

The health check middleware will use default settings from .

### <a id="methods-usehealthchecksplus"/>**UseHealthChecksPlus(IApplicationBuilder, PathString, Int32)**

Adds a middleware that provides health check status.

```csharp
public static IApplicationBuilder UseHealthChecksPlus(IApplicationBuilder app, PathString path, int port)
```

#### Parameters

`app` IApplicationBuilder<br>
The .

`path` PathString<br>
The path on which to provide health check status.

`port` [Int32](https://docs.microsoft.com/en-us/dotnet/api/system.int32)<br>
The port to listen on. Must be a local port on which the server is listening.

#### Returns

A reference to the  after the operation has completed.

**Remarks:**

If  is set to `null` or the empty string then the health check middleware
 will ignore the URL path and process all requests on the specified port. If  is
 set to a non-empty value, the health check middleware will process requests with a URL that matches the
 provided value of  case-insensitively, allowing for an extra trailing slash ('/')
 character.

The health check middleware will use default settings from .

### <a id="methods-usehealthchecksplus"/>**UseHealthChecksPlus(IApplicationBuilder, PathString, HealthCheckPlusOptions)**

Adds a middleware that provides health check status.

```csharp
public static IApplicationBuilder UseHealthChecksPlus(IApplicationBuilder app, PathString path, HealthCheckPlusOptions options)
```

#### Parameters

`app` IApplicationBuilder<br>
The .

`path` PathString<br>
The The path on which to provide health check status.

`options` [HealthCheckPlusOptions](./healthcheckplus.options.healthcheckplusoptions.md)<br>
The [HealthCheckPlusOptions](./healthcheckplus.options.healthcheckplusoptions.md) used to configure.

#### Returns

The .

**Remarks:**

ignore the URL path and process all requests. If path is set to a non-empty value,
 the health check middleware will process requests with a URL that matches the
 provided value of path case-insensitively, allowing for an extra trailing slash
 ('/') character.

### <a id="methods-usehealthchecksplus"/>**UseHealthChecksPlus(IApplicationBuilder, PathString, Int32, HealthCheckPlusOptions)**

Adds a middleware that provides health check status.

```csharp
public static IApplicationBuilder UseHealthChecksPlus(IApplicationBuilder app, PathString path, int port, HealthCheckPlusOptions options)
```

#### Parameters

`app` IApplicationBuilder<br>
The .

`path` PathString<br>
The The path on which to provide health check status.

`port` [Int32](https://docs.microsoft.com/en-us/dotnet/api/system.int32)<br>
The port to listen on. Must be a local port on which the server is listening.

`options` [HealthCheckPlusOptions](./healthcheckplus.options.healthcheckplusoptions.md)<br>
The [HealthCheckPlusOptions](./healthcheckplus.options.healthcheckplusoptions.md) used to configure.

#### Returns

The .

**Remarks:**

ignore the URL path and process all requests. If path is set to a non-empty value,
 the health check middleware will process requests with a URL that matches the
 provided value of path case-insensitively, allowing for an extra trailing slash
 ('/') character.


- - -
[**Back to List Api**](./apis.md)
