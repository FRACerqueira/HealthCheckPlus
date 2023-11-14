# **Welcome to HealthCheckPlus**

### **HealthCheck with individual delay and interval and interval policy for unhealthy/degraded status.**

**HealthCheckPlus** was developed in c# with the **netstandard2.1**, **.Net6** and **.Net7** target frameworks.

## What's new V1.0.4

- First Release G.A

## Features

- Delay and interval for each HealthCheck
- Interval policy for unhealthy status for each HealthCheck
- Interval policy for degraded status for each HealthCheck
- Endpoints returns the latest internal status protecting your application.
- The parameter period for each health check acts as a circuit breaker when using it in your business logic, improving application responsiveness in high request rate scenarios and protecting your infrastructure.
- Change to unhealthy/degraded any HealthCheck by forcing check by interval policy
- Register an external health check (packet import) and associate delay, interval and individual policy rules.
- Optional action to write log.
- Optional parameter for log category name.
- Simple and clear fluent syntax

## Usage**

The **HealthCheckPlus** use **fluent interface**; an object-oriented API whose design relies extensively on method chaining. Its goal is to increase code legibility. The term was coined in 2005 by Eric Evans and Martin Fowler.


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
    .AddHealthChecks<MyEnum>("AppHealthCheck", 
        HealthStatus.Degraded,
        "AppDemo", 
        (log, result) => 
        {
            switch (result.Status)
            {
                case HealthStatus.Unhealthy:
                    log.LogError($"{result.Name} : {result.Description} : {result.Status} : {result.ElapsedTime} : {result.Date}");
                    break;
                case HealthStatus.Degraded:
                    log.LogWarning($"{result.Name} : {result.Description} : {result.Status} : {result.ElapsedTime} : {result.Date}");
                    break;
                case HealthStatus.Healthy:
                    log.LogInformation($"{result.Name} : {result.Description} : {result.Status} : {result.ElapsedTime} : {result.Date}");
                    break;
                default:
                    break;
            }
        })
    .AddRedis("connection string", "Myredis") //Register Xabaril Redis HealthCheck
    .AddCheckPlus<MyEnum, HcTeste1>(MyEnum.HcTest1)
    .AddCheckPlus<MyEnum, HcTeste2>(MyEnum.HcTest2, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), failureStatus: HealthStatus.Degraded)
    .AddCheckRegistered(MyEnum.Redis, "MyRedis", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30))
    .AddUnhealthyPolicy(MyEnum.Redis, TimeSpan.FromSeconds(10));
    .AddDegradedPolicy(MyEnum.HcTest2, TimeSpan.FromSeconds(5));
```

```csharp
//At Statup / Program
app.UseHealthChecksPlus("/health/ready", HttpStatusCode.OK)
   .UseHealthChecksPlusStatus("/health/Live", HttpStatusCode.OK);
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
        return await Task.FromResult(HealthCheckResult.Healthy($"teste1"));
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
        return await Task.FromResult(HealthCheckResult.Degraded($"teste2"));
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
        try
        {
            //redis access
        }
        catch (ExceptionRedis rex)
        {
            healthCheckApp.SwithToUnhealthy(MyEnum.Redis);
        }
    }
}
```

## License

Copyright 2023 @ Fernando Cerqueira

HealthCheckPlus is licensed under the MIT license. See [LICENSE](https://github.com/FRACerqueira/HealthCheckPlus/blob/master/LICENSE).

