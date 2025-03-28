![HealthCheckPlus Logo](https://raw.githubusercontent.com/FRACerqueira/HealthCheckPlus/refs/heads/main/icon.png)

### HealthChecksPlusExtension.AddBackgroundPolicy method
</br>


#### Register HealthChecksPlus Background service with [`HealthCheckPlusBackGroundOptions`](../../HealthCheckPlus.options/HealthCheckPlusBackGroundOptions.md) options. Default Values:Delay = 5 seconds.HealthyPeriod = 30 seconds.DegradedPeriod = 30 seconds.UnhealthyPeriod = 30 seconds.Timeout = 30 seconds.Predicate = All HealthCheck.

```csharp
public static IHealthChecksBuilder AddBackgroundPolicy(this IHealthChecksBuilder ihb, 
    Action<HealthCheckPlusBackGroundOptions>? option = null)
```

| parameter | description |
| --- | --- |
| ihb | The IHealthChecksBuilder. |
| option | The options for HealthChecksPlus Background service. See [`HealthCheckPlusBackGroundOptions`](../../HealthCheckPlus.options/HealthCheckPlusBackGroundOptions.md). |

### Return Value

The IHealthChecksBuilder.

### See Also

* class [HealthCheckPlusBackGroundOptions](../../HealthCheckPlus.options/HealthCheckPlusBackGroundOptions.md)
* class [HealthChecksPlusExtension](../HealthChecksPlusExtension.md)
* namespace [Microsoft.Extensions.DependencyInjection](../../HealthCheckPlus.md)

<!-- DO NOT EDIT: generated by xmldocmd for HealthCheckPlus.dll -->
