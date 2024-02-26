# <img align="left" width="100" height="100" src="./docs/images/icon.png">Welcome to HealthCheckPlus
[![Build](https://github.com/FRACerqueira/HealthCheckPlus/workflows/Build/badge.svg)](https://github.com/FRACerqueira/HealthCheckPlus/actions/workflows/build.yml)
[![License](https://img.shields.io/github/license/FRACerqueira/HealthCheckPlus)](https://github.com/FRACerqueira/HealthCheckPlus/blob/master/LICENSE)
[![NuGet](https://img.shields.io/nuget/v/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)
[![Downloads](https://img.shields.io/nuget/dt/HealthCheckPlus)](https://www.nuget.org/packages/HealthCheckPlus/)


### **HealthCheck with individual delay and interval and interval policy for unhealthy/degraded status.**

**HealthCheckPlus** was developed in c# with the **.Net6** , **.Net7** and **.Net8** target frameworks.

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

### V2.0.1

- Created dependency isolation package: HealthCheckPlus.Abstractions
    - Now all public interfaces and classes are isolated in another assembly  

### V2.0.0
[**Top**](#table-of-contents)

- Complete refactoring to only extend native functionalities without changing their behavior when the new features are not used.

## Features
[**Top**](#table-of-contents)

- Command to Change to unhealthy/degraded any HealthCheck by forcing check by interval policy
- Command to retrieve the last result of each HealthCheck kept in cache
- Optional Delay and interval for each HealthCheck 
    - Policy for Healthy while keeping results cached (default)
    - Policy for degraded (Optional)
    - Policy for unhealthy (Optional)
- Register an external health check (package import) and associate delay, interval and individual policy rules.
- Policy background service for updating and running HealthChecks
    - Optional set delay and interval are used in the background update service parameters when defined and HealthCheck is null for delay and interval
    - Integration with registered publishers with the interface IHealthCheckPublisher with extra filters:
        - Number of counts idle to publish.
        - Run publish only when the report has a status change in one of its entries.
- Response templates with small/full details in "application/json" ContentType
    - HealthCheckPlusOptions.WriteShortDetails
    - HealthCheckPlusOptions.WriteShortDetailsPlus (with extra fields : cache source and reference date of last run)
    - HealthCheckPlusOptions.WriteDetailsWithoutException
    - HealthCheckPlusOptions.WriteDetailsWithoutExceptionPlus (with extra fields : cache source and reference date of last run)
    - HealthCheckPlusOptions.WriteDetailsWithException
    - HealthCheckPlusOptions.WriteDetailsWithExceptionPlus (with extra fields : cache source and reference date of last run)
- Simple and clear fluent syntax extending the native features of healt check

## Installing
[**Top**](#table-of-contents)

Top layer
```
Install-Package HealthCheckPlus [-pre]
```

```
dotnet add package HealthCheckPlus [--prerelease]
```

Other layer
```
Install-Package HealthCheckPlus.Abstractions [-pre]
```

```
dotnet add package HealthCheckPlus.Abstractions [--prerelease]
```

**_Note:  [-pre]/[--prerelease] usage for pre-release versions_**

## Examples
[**Top**](#table-of-contents)

See folder [**Samples**](https://github.com/FRACerqueira/HealthCheckPlus/tree/main/Samples).

## Usage
[**Top**](#table-of-contents)

The **HealthCheckPlus** use **fluent interface**; an object-oriented API whose design relies extensively on method chaining. Its goal is to increase code legibility. The term was coined in 2005 by Eric Evans and Martin Fowler.

```csharp
//create list all HealthCheck by enum (optional type)
public enum MyEnum
{
    HcTeste1,
    HcTeste2,
    Redis
}
//create list all HealthCheck by string (compatible type)
private static readonly string[] HealthChecknames = ["HcTest1", "HcTest2", "Redis"];
```

```csharp
//At Statup / Program (without background services policies with string)
builder.Services
    //Add HealthCheckPlus
    .AddHealthChecksPlus(HealthChecknames)
    //your custom HC    
    .AddCheckPlus<HcTeste1>("HcTest1")
    //your custom HC    
    .AddCheckPlus<HcTeste2>("HcTest2", failureStatus: HealthStatus.Degraded)
    //external HC 
    .AddRedis("connection string", "Myredis")
    //register external HC 
    .AddCheckLinkTo("Redis", "MyRedis", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30))
    //policy for Unhealthy
    .AddUnhealthyPolicy("HcTest1", TimeSpan.FromSeconds(2))
    //policy for Degraded
    .AddDegradedPolicy("HcTest2", TimeSpan.FromSeconds(3))
    //policy for Unhealthy
    .AddUnhealthyPolicy("Redis", TimeSpan.FromSeconds(1));
```

```csharp
//At Statup / Program (without background services policies with enum)
builder.Services
    //Add HealthCheckPlus
    .AddHealthChecksPlus<MyEnum>()
    //your custom HC    
    .AddCheckPlus<HcTeste1>(MyEnum.HcTest1)
    //your custom HC    
    .AddCheckPlus<HcTeste2>(MyEnum.HcTest2, failureStatus: HealthStatus.Degraded)
    //external HC 
    .AddRedis("connection string", "Myredis")
    //register external HC 
    .AddCheckLinkTo(MyEnum.Redis, "MyRedis", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30))
    //policy for Unhealthy
    .AddUnhealthyPolicy(MyEnum.HcTest1, TimeSpan.FromSeconds(2))
    //policy for Degraded
    .AddDegradedPolicy(MyEnum.HcTest2, TimeSpan.FromSeconds(3))
    //policy for Unhealthy
    .AddUnhealthyPolicy(MyEnum.Redis, TimeSpan.FromSeconds(1));
```

```csharp
//At Statup / Program (without background services policies)
builder.Services
    //Add HealthCheckPlus
    .AddHealthChecksPlus<MyEnum>()
    //your custom HC    
    .AddCheckPlus<HcTeste1>(MyEnum.HcTest1)
    //your custom HC    
    .AddCheckPlus<HcTeste2>(MyEnum.HcTest2, failureStatus: HealthStatus.Degraded)
    //external HC 
    .AddRedis("connection string", "Myredis")
    //register external HC 
    .AddCheckLinkTo(MyEnum.Redis, "MyRedis", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30))
    //policy for Unhealthy
    .AddUnhealthyPolicy(MyEnum.HcTest1, TimeSpan.FromSeconds(2))
    //policy for Degraded
    .AddDegradedPolicy(MyEnum.HcTest2, TimeSpan.FromSeconds(3))
    //policy for Unhealthy
    .AddUnhealthyPolicy(MyEnum.Redis, TimeSpan.FromSeconds(1));

```

```csharp
//At Statup / Program (with background services policies with enum)
builder.Services
    //Add HealthCheckPlus
    .AddHealthChecksPlus<MyEnum>()
    //your custom HC with custom delay and period   
    .AddCheckPlus<HcTeste1>(MyEnum.HcTest1, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10))
    //your custom HC without delay and period (using BackgroundPolicy)     
    .AddCheckPlus<HcTeste2>(MyEnum.HcTest2, failureStatus: HealthStatus.Degraded)
    //external HC 
    .AddRedis("connection string", "Myredis")
    //register external HC  without delay and period (using BackgroundPolicy)
    .AddCheckLinkTo(MyEnum.Redis, "MyRedis")
    //policy for running in Background service
    .AddBackgroundPolicy((opt) =>
    {
        opt.Delay = TimeSpan.FromSeconds(5);
        opt.Timeout = TimeSpan.FromSeconds(30);
        opt.Idle = TimeSpan.FromSeconds(1);
        //opt.AllStatusPeriod(TimeSpan.FromSeconds(30));
        opt.HealthyPeriod = TimeSpan.FromSeconds(30);
        opt.DegradedPeriod = TimeSpan.FromSeconds(30);
        opt.UnhealthyPeriod = TimeSpan.FromSeconds(30);
        opt.Publishing = new PublishingOptions() 
        { 
            //default values
            AfterIdleCount = 1,
            WhenReportChange = true
        };
    });
```


```csharp
//At Statup / Program (optional)

var app = builder.Build();

//save interfaces IStateHealthChecksPlus
using (IServiceScope startscope = app.Services.CreateScope())
{
    _stateHealthChecksPlus = startscope.ServiceProvider.GetRequiredService<IStateHealthChecksPlus>();
}
```

```csharp
//At Statup / Program
//Endpoints HC
app
    //Extend HealthCheckOptions with HealthCheckPlusOptions
    .UseHealthChecksPlus("/health/live", new HealthCheckPlusOptions
    {
        //name for HealthCheck kind
        HealthCheckName = "live",
        //custom function for status value of report
        StatusHealthReport = (rep) =>
        {
            if (rep.StatusResult(MyEnum.HcTest1) == HealthStatus.Unhealthy)
            {
                //do something
            }
            if (rep.TryGetNotHealthy(out var results))
            {
                //do something
            }
            return HealthStatus.Degraded;
        },
        //Result Status Codes  (same behavior as HealthCheckOptions)
        ResultStatusCodes =
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status200OK,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
        }
    })
    //default HealthCheckPlusOptions (same behavior as default HealthCheckOptions)
    .UseHealthChecksPlus("/health/ready", new HealthCheckPlusOptions
    {
        //name for HealthCheck kind
        HealthCheckName = "ready",
        //template for Response (same behavior as HealthCheckOptions)
        ResponseWriter = HealthCheckPlusOptions.WriteDetailsWithoutException,
        //Result Status Codes  (same behavior as HealthCheckOptions)
        ResultStatusCodes =
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status200OK,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
        }
    });
```

```csharp
//example of use in the middler pipeline
_ = app.Use(async (context, next) =>
{
    if (_stateHealthChecksPlus.Status("live") == HealthStatus.Unhealthy)
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
//example of use in a business class using dependency injection
public class MyBussines
{
    public MyBussines(IStateHealthChecksPlus healthCheckApp)
    {
        if (healthCheckApp.Status("live") == HealthStatus.Degraded)
        { 
            //do something
        }
        if (healthCheckApp.StatusResult(MyEnum.HcTeste2).Status == HealthStatus.Unhealthy)
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

```csharp
//example of  Publisher condition to execute
public class SamplePublishHealth : IHealthCheckPublisher, IHealthCheckPlusPublisher
{
   public Func<HealthReport, bool> PublisherCondition => (_) => true;
   public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
   {
      return Task.CompletedTask;
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

