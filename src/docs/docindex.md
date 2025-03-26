![HtmlPdfPLus Logo](https://raw.githubusercontent.com/FRACerqueira/HealthCheckPlus/refs/heads/main/icon.png)

### HealthCheckPlus Documentation 
</br>

### HealthCheckPlus.options namespace

| public type | description |
| --- | --- |
| class [HealthCheckPlusBackGroundOptions](./assemblies/HealthCheckPlus.options/HealthCheckPlusBackGroundOptions.md) | Options for the HealthCheckPlus background service instance. |
| class [HealthCheckPlusOptions](./assemblies/HealthCheckPlus.options/HealthCheckPlusOptions.md) | Contains optionsSerilz for the HealthCheckMiddleware. |
| class [PublishingOptions](./assemblies/HealthCheckPlus.options/PublishingOptions.md) | Usage for publishers registered |

### HealthCheckPlus.Abstractions namespace

| public type | description |
| --- | --- |
| enum [HealthCheckTrigger](./assemblies/HealthCheckPlus.Abstractions/HealthCheckTrigger.md) | Enumerator values for the origin of the last status result. |
| static class [HealthReportExtensions](./assemblies/HealthCheckPlus.Abstractions/HealthReportExtensions.md) | The Extensions for HealthReport |
| interface [IDataHealthPlus](./assemblies/HealthCheckPlus.Abstractions/IDataHealthPlus.md) | Represents data from the last Health Check performed. |
| interface [IHealthCheckPlusPublisher](./assemblies/HealthCheckPlus.Abstractions/IHealthCheckPlusPublisher.md) | Represents a publisher of HealthReport information. |
| interface [IStateHealthChecksPlus](./assemblies/HealthCheckPlus.Abstractions/IStateHealthChecksPlus.md) | Represents the commands of the HealthChecksPlus for access data. |

### Microsoft.AspNetCore.Builder namespace

| public type | description |
| --- | --- |
| static class [HealthChecksPlusAppExtension](./assemblies/Microsoft.AspNetCore.Builder/HealthChecksPlusAppExtension.md) | HealthChecksPlus Extension for IApplicationBuilder |

### Microsoft.Extensions.DependencyInjection namespace

| public type | description |
| --- | --- |
| static class [HealthChecksPlusExtension](./assemblies/Microsoft.Extensions.DependencyInjection/HealthChecksPlusExtension.md) | HealthChecksPlus Extension for DependencyInjection |
