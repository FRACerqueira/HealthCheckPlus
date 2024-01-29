# <img align="left" width="100" height="100" src="./docs/images/icon.png">Welcome to HealthCheckPlus
[![Build](https://github.com/FRACerqueira/HealthCheckPlus/workflows/Build/badge.svg)](https://github.com/FRACerqueira/HealthCheckPlus/actions/workflows/build.yml)
[![License](https://img.shields.io/github/license/FRACerqueira/HealthCheckPlus)](https://github.com/FRACerqueira/HealthCheckPlus/blob/master/LICENSE)
[![NuGet](https://img.shields.io/nuget/v/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)
[![Downloads](https://img.shields.io/nuget/dt/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)


### **HealthCheck with individual delay and interval and interval policy for unhealthy/degraded status.**

**HealthCheckPlus** was developed in c# with the **netstandard2.1**, **.Net6** , **.Net7** and **.Net8** target frameworks.

**[Visit the official page for more documentation of HealthCheckPlus](https://fracerqueira.github.io/HealthCheckPlus)**

## Table of Contents

- [What's new - previous versions](./docs/whatsnewprev.md)
- [Features](#features)
- [Installing](#installing)
- [Examples](#examples)
- [Usage](#usage)
- [Code of Conduct](#code-of-conduct)
- [Contributing](#contributing)
- [Credits](#credits)
- [License](#license)
- [API Reference](https://fracerqueira.github.io/HealthCheckPlus/apis/apis.html)


## What's new in the latest version 

### V1.0.5

- Added parameter 'Delay'  on AddHealthChecks<T> : Initial delay applied after the application starts. The default value is 5 seconds.The min.value is 1 second.
- Added parameter 'Period' on AddHealthChecks<T> : Period of HealthCheckPublisher execution. The default value is 1 seconds. The min.value is 500 milesecond.

### V1.0.4

[**Top**](#table-of-contents)

- First Release G.A

## Features
[**Top**](#table-of-contents)

- Delay and interval for HealthCheckPublisher  
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

## Installing
[**Top**](#table-of-contents)

```
Install-Package HealthCheckPlus [-pre]
```

```
dotnet add package HealthCheckPlus [--prerelease]
```

**_Note:  [-pre]/[--prerelease] usage for pre-release versions_**

## Examples
[**Top**](#table-of-contents)

See folder [**Samples**](https://github.com/FRACerqueira/HealthCheckPlus/tree/main/samples).

## Usage
[**Top**](#table-of-contents)

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
    //Add HealthCheckPlus
    .AddHealthChecks<MyEnum>("AppHealthCheck", (deps) =>
        //custom result status 
        {
            if (deps.TryGetNotHealthy(out _))
            {
                return HealthStatus.Degraded;
            }
            return HealthStatus.Healthy;
        },
        //category log
        "HealthCheckPlusDemo",
        //action for log    
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
    //your custom HC    
    .AddCheckPlus<MyEnum, HcTeste1>(MyEnum.HcTest1, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(30))
    //your custom HC    
    .AddCheckPlus<MyEnum, HcTeste2>(MyEnum.HcTest2, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(40), failureStatus: HealthStatus.Degraded)
    //external HC 
    .AddRedis("connection string", "Myredis")
    //register external HC 
    .AddCheckRegistered(MyEnum.Redis, "MyRedis", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(60))
    //policy for Unhealthy
    .AddUnhealthyPolicy(MyEnum.HcTest1, TimeSpan.FromSeconds(2))
    //policy for Degraded
    .AddDegradedPolicy(MyEnum.HcTest2, TimeSpan.FromSeconds(3))
    //policy for Unhealthy
    .AddUnhealthyPolicy(MyEnum.Redis, TimeSpan.FromSeconds(5));
```

```csharp
//At Statup / Program
//Endpoints HC
app.UseHealthChecksPlus("/health/live", HttpStatusCode.OK)
   .UseHealthChecksPlusStatus("/health/ready", HttpStatusCode.OK);
```

```csharp
//At Statup / Program
//middler pipeline
_ = app.Use(async (context, next) =>
{
    if (_stateHealthChecksPlus.StatusApp.Status == HealthStatus.Unhealthy)
    {
        var msg = JsonSerializer.Serialize(new { Error = "App Unhealthy" });
        context.Response.ContentType = "application/json";
        context.Response.ContentLength = msg.Length;
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync(msg);
        await context.Response.CompleteAsync();
        return;
    }
    await next();
});
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

## Code of Conduct
[**Top**](#table-of-contents)

This project has adopted the code of conduct defined by the Contributor Covenant to clarify expected behavior in our community.
For more information see the [Code of Conduct](CODE_OF_CONDUCT.md).

## Contributing
[**Top**](#table-of-contents)

See the [Contributing guide](CONTRIBUTING.md) for developer documentation.

## Credits
[**Top**](#table-of-contents)

**API documentation generated by**

- [xmldoc2md](https://github.com/FRACerqueira/xmldoc2md), Copyright (c) 2022 Charles de Vandière. See [LICENSE](Licenses/LICENSE-xmldoc2md.md).

## License
[**Top**](#table-of-contents)

Copyright 2023 @ Fernando Cerqueira

HealthCheckPlus is licensed under the MIT license. See [LICENSE](https://github.com/FRACerqueira/HealthCheckPlus/blob/master/LICENSE).

