![HealthCheckPlus Logo](https://raw.githubusercontent.com/FRACerqueira/HealthCheckPlus/refs/heads/main/icon.png)

### IStateHealthChecksPlus.TryGetUnhealthy method
</br>


#### Tries to get all unhealthy statuses.

```csharp
public bool TryGetUnhealthy(out IReadOnlyDictionary<string, HealthCheckResult> result)
```

| parameter | description |
| --- | --- |
| result | The dictionary with all HealthCheckResult with unhealthy status. |

### Return Value

True if found, otherwise false.

### See Also

* interface [IStateHealthChecksPlus](../IStateHealthChecksPlus.md)
* namespace [HealthCheckPlus.Abstractions](../../HealthCheckPlus.Abstractions.md)

<!-- DO NOT EDIT: generated by xmldocmd for HealthCheckPlus.Abstractions.dll -->
