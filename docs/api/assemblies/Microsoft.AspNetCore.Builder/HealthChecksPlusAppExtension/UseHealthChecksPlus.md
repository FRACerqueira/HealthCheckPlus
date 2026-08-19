![HealthCheckPlus Logo](https://raw.githubusercontent.com/FRACerqueira/HealthCheckPlus/refs/heads/main/icon.png)

### HealthChecksPlusAppExtension.UseHealthChecksPlus method (1 of 4)
</br>


#### Adds a middleware that provides health check status.

```csharp
public static IApplicationBuilder UseHealthChecksPlus(this IApplicationBuilder app, PathString path)
```

| parameter | description |
| --- | --- |
| app | The IApplicationBuilder. |
| path | The path on which to provide health check status. |

### Return Value

A reference to the *app* after the operation has completed.

### Exceptions

| exception | condition |
| --- | --- |
| ArgumentNullException | *app* is `null`. |
| InvalidOperationException | No HealthCheckService is registered - `AddHealthChecksPlus` was never called. |

### Remarks

If *path* is set to `null` or the empty string then the health check middleware will ignore the URL path and process all requests. If *path* is set to a non-empty value, the health check middleware will process requests with a URL that matches the provided value of *path* case-insensitively, allowing for an extra trailing slash ('/') character.

The health check middleware will use default settings from IOptions.

### See Also

* class [HealthChecksPlusAppExtension](../HealthChecksPlusAppExtension.md)
* namespace [Microsoft.AspNetCore.Builder](../../HealthCheckPlus.md)

---

### HealthChecksPlusAppExtension.UseHealthChecksPlus method (2 of 4)

#### Adds a middleware that provides health check status.

```csharp
public static IApplicationBuilder UseHealthChecksPlus(this IApplicationBuilder app, 
    PathString path, HealthCheckPlusOptions options)
```

| parameter | description |
| --- | --- |
| app | The IApplicationBuilder. |
| path | The path on which to provide health check status. |
| options | The [`HealthCheckPlusOptions`](../../HealthCheckPlus.Options/HealthCheckPlusOptions.md) used to configure. |

### Return Value

The IApplicationBuilder.

### Exceptions

| exception | condition |
| --- | --- |
| ArgumentNullException | *app* or *options* is `null`. |
| ArgumentException | *options*' [`HealthCheckName`](../../HealthCheckPlus.Options/HealthCheckPlusOptions/HealthCheckName.md) was already registered by an earlier call. |
| InvalidOperationException | No HealthCheckService is registered - `AddHealthChecksPlus` was never called - or the registered IStateHealthChecksPlus is not the type `AddHealthChecksPlus()` registers. |

### Remarks

If path is set to null or the empty string then the health check middleware will ignore the URL path and process all requests. If path is set to a non-empty value, the health check middleware will process requests with a URL that matches the provided value of path case-insensitively, allowing for an extra trailing slash ('/') character.

### See Also

* class [HealthCheckPlusOptions](../../HealthCheckPlus.Options/HealthCheckPlusOptions.md)
* class [HealthChecksPlusAppExtension](../HealthChecksPlusAppExtension.md)
* namespace [Microsoft.AspNetCore.Builder](../../HealthCheckPlus.md)

---

### HealthChecksPlusAppExtension.UseHealthChecksPlus method (3 of 4)

#### Adds a middleware that provides health check status.

```csharp
public static IApplicationBuilder UseHealthChecksPlus(this IApplicationBuilder app, 
    PathString path, int port)
```

| parameter | description |
| --- | --- |
| app | The IApplicationBuilder. |
| path | The path on which to provide health check status. |
| port | The port to listen on. Must be a local port on which the server is listening. |

### Return Value

A reference to the *app* after the operation has completed.

### Exceptions

| exception | condition |
| --- | --- |
| ArgumentNullException | *app* is `null`. |
| InvalidOperationException | No HealthCheckService is registered - `AddHealthChecksPlus` was never called. |

### Remarks

If *path* is set to `null` or the empty string then the health check middleware will ignore the URL path and process all requests on the specified port. If *path* is set to a non-empty value, the health check middleware will process requests with a URL that matches the provided value of *path* case-insensitively, allowing for an extra trailing slash ('/') character.

The health check middleware will use default settings from IOptions.

### See Also

* class [HealthChecksPlusAppExtension](../HealthChecksPlusAppExtension.md)
* namespace [Microsoft.AspNetCore.Builder](../../HealthCheckPlus.md)

---

### HealthChecksPlusAppExtension.UseHealthChecksPlus method (4 of 4)

#### Adds a middleware that provides health check status.

```csharp
public static IApplicationBuilder UseHealthChecksPlus(this IApplicationBuilder app, 
    PathString path, int port, HealthCheckPlusOptions options)
```

| parameter | description |
| --- | --- |
| app | The IApplicationBuilder. |
| path | The path on which to provide health check status. |
| port | The port to listen on. Must be a local port on which the server is listening. |
| options | The [`HealthCheckPlusOptions`](../../HealthCheckPlus.Options/HealthCheckPlusOptions.md) used to configure. |

### Return Value

The IApplicationBuilder.

### Exceptions

| exception | condition |
| --- | --- |
| ArgumentNullException | *app* or *options* is `null`. |
| ArgumentException | *options*' [`HealthCheckName`](../../HealthCheckPlus.Options/HealthCheckPlusOptions/HealthCheckName.md) was already registered by an earlier call. |
| InvalidOperationException | No HealthCheckService is registered - `AddHealthChecksPlus` was never called - or the registered IStateHealthChecksPlus is not the type `AddHealthChecksPlus()` registers. |

### Remarks

If path is set to null or the empty string then the health check middleware will ignore the URL path and process all requests. If path is set to a non-empty value, the health check middleware will process requests with a URL that matches the provided value of path case-insensitively, allowing for an extra trailing slash ('/') character.

### See Also

* class [HealthCheckPlusOptions](../../HealthCheckPlus.Options/HealthCheckPlusOptions.md)
* class [HealthChecksPlusAppExtension](../HealthChecksPlusAppExtension.md)
* namespace [Microsoft.AspNetCore.Builder](../../HealthCheckPlus.md)

<!-- DO NOT EDIT: generated by xmldocmd for HealthCheckPlus.dll -->
