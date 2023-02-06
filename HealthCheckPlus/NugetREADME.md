# **Welcome to HealthCheckPlus**

HealthCheck with IHealthCheckPublisher and individual check interval and Unhealth interval policy.
The healthcheck endpoint´s, when called, does not perform any action and returns the healthcheckPlus status, protecting the execution according to the specified interval configuration and the unheath policy.

HealthCheckPlus was developed in c# with the **netstandard2.1, .NET 6 AND .NET7 ** target frameworks.

**Relase Notes HealthCheckPlus (V1.0.1)**

- Added Documentation file helpper for Visual-Studio

## **Official pages** :

#### **[Visit the HealthCheckPlus official page for complete documentation](https://fracerqueira.github.io/HealthCheckPlus)**

## **HealthCheckPlus - Sample Usage**

```csharp
//Create enum with all HealthCheck
public enum MyEnum
{
    HcTeste1,
    HcTeste2,
    Redis
}
```

```csharp
//At Statup / Program
builder.Services
    .AddHealthChecks<MyEnum>("AppHealthCheck", HealthStatus.Degraded)
    .AddRedis("teste1", "Myredis") //Register Xabaril Redis HealthCheck
    .AddCheckPlus<MyEnum, HcTeste1>(MyEnum.HcTest1)
    .AddCheckPlus<MyEnum, HcTeste2>(MyEnum.HcTest2, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), failureStatus: HealthStatus.Degraded)
    .AddCheckRegistered(MyEnum.Redis, "MyRedis", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30))
    .AddUnhealthyPolicy(MyEnum.Redis, TimeSpan.FromSeconds(10));
```

```csharp
//At Statup / Program
app.UseHealthChecksPlus("/health/ready", HttpStatusCode.OK)
   .UseHealthChecksPlus("/health/Live", HttpStatusCode.OK)
   .UseHealthChecksPlusStatus("/health/Status", HttpStatusCode.OK);
```

```csharp
//Create HealthCheck class inheriting from BaseHealthCheckPlus(IHealthCheck)
public class HTest1 : BaseHealthCheckPlus
{
    public hcteste1(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    public override async Task<HealthCheckResult> DoHealthCheck(HealthCheckContext context, CancellationToken cancellationToken)
    {
        return await Task.FromResult(HealthCheckResult.Healthy($"teste"));
    }
}
//Create HealthCheck class inheriting from BaseHealthCheckPlus(IHealthCheck)
public class HTest2 : BaseHealthCheckPlus
{
    public hcteste2(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    public override async Task<HealthCheckResult> DoHealthCheck(HealthCheckContext context, CancellationToken cancellationToken)
    {
        return await Task.FromResult(HealthCheckResult.Healthy($"teste"));
    }
}
```

```csharp
//Consuming Status from HealthCheckPlus
public class MyBussines
{
    public MyBussines(IStateHealthChecksPlus healthCheckApp)
    {
        if (healthCheckApp.StatusApp.Status == HealthStatus.Degraded)
        { 
            //do something
        }
        if (healthCheckApp.StatusDep(MyEnum.HcTeste2).Status == HealthStatus.Unhealthy)
        { 
            //do something. This dependency 'HcTeste2' is not available
        }
    }
}
```

## **License**

This project is licensed under the [MIT License](https://github.com/FRACerqueira/HealthCheckPlus/blob/master/LICENSE)

