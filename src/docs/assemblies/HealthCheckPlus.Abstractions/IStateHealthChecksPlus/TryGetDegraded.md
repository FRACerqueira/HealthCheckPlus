![HealthCheckPlus Logo](https://raw.githubusercontent.com/FRACerqueira/HealthCheckPlus/refs/heads/main/icon.png)

### IStateHealthChecksPlus.TryGetDegraded method
</br>


#### Tries to get all degraded statuses.

```csharp
public bool TryGetDegraded(out IReadOnlyDictionary<string, HealthCheckResult> result)
```

| parameter | description |
| --- | --- |
| result | The dictionary with all HealthCheckResult with degraded status. |

### Return Value

True if found, otherwise false.

### See Also

* interface [IStateHealthChecksPlus](../IStateHealthChecksPlus.md)
* namespace [HealthCheckPlus.Abstractions](../../HealthCheckPlus.Abstractions.md)

<!-- DO NOT EDIT: generated by xmldocmd for HealthCheckPlus.Abstractions.dll -->
