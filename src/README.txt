 _   _               _  _    _
| | | |  ___   __ _ | || |_ | |__
| |_| | / _ \ / _` || || __|| '_ \
|  _  ||  __/| (_| || || |_ | | | |
|_| |_| \___| \__,_||_| \__||_| |_|
  ____  _                  _
 / ___|| |__    ___   ___ | | __
| |    | '_ \  / _ \ / __|| |/ /
| |___ | | | ||  __/| (__ |   <
 \____||_| |_| \___| \___||_|\_\
 ____   _
|  _ \ | | _   _  ___
| |_) || || | | |/ __|
|  __/ | || |_| |\__ \
|_|    |_| \__,_||___/

Welcome to HealthCheckPlus
**************************

HealthCheck with individual policies based on healthy/degraded/unhealthy status and optimized Report Publisher.

HealthCheckPlus was developed in c# with the **.Net9** and **.Net8** target frameworks.

Features
********
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

What's new
----------

- V3.0.0 (latest version)

    - Added support for .Net9
    - Removed support for .Net6, .Net7
    - Removed commands with enum for list of HealthCheck´s
    - Some property names have been refactored for readability or syntax errors.
    - Optimized several parts of the code to improve performance
    - Fixed publisher improper execution bug when set to only execute when there are changes
    - Documentation updated
    
- V2.0.1

    - Created dependency isolation package: HealthCheckPlus.Abstractions
        - Now all public interfaces and classes are isolated in another assembly  

Examples
********

See folder : https://github.com/FRACerqueira/HealthCheckPlus/tree/main/Samples

Usage
*****

//create list all HealthCheck by string (compatible type)
private static readonly string[] HealthChecknames = ["HcTest1", "HcTest2", "Redis"];

...

//At Statup / Program (without background services policies)
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
...

//At Statup / Program (wit background services policies)
builder.Services
    //Add HealthCheckPlus
    .AddHealthChecksPlus<MyEnum>()
    //your custom HC with custom delay and period   
    .AddCheckPlus<HcTeste1>("HcTest1", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10))
    //your custom HC without delay and period (using BackgroundPolicy)     
    .AddCheckPlus<HcTeste2>("HcTest2", failureStatus: HealthStatus.Degraded)
    //external HC 
    .AddRedis("connection string", "Myredis")
    //register external HC  without delay and period (using BackgroundPolicy)
    .AddCheckLinkTo("Redis", "MyRedis")
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
...


//At Statup / Program

... 

var app = builder.Build();

//save interfaces IStateHealthChecksPlus
using (IServiceScope startscope = app.Services.CreateScope())
{
    _stateHealthChecksPlus = startscope.ServiceProvider.GetRequiredService<IStateHealthChecksPlus>();
}

...

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
            if (rep.StatusResult("HcTest1") == HealthStatus.Unhealthy)
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

...

//At Statup / Program

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

...

//example of use in a business class using dependency injection
public class MyBussines
{
    public MyBussines(IStateHealthChecksPlus healthCheckApp)
    {
        if (healthCheckApp.Status("live") == HealthStatus.Degraded)
        { 
            //do something
        }
        if (healthCheckApp.StatusResult("HcTeste2").Status == HealthStatus.Unhealthy)
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

...

//example of  Publisher condition to execute
public class SamplePublishHealth : IHealthCheckPublisher, IHealthCheckPlusPublisher
{
   public Func<HealthReport, bool> PublisherCondition => (_) => true;
   public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
   {
      return Task.CompletedTask;
   }
}

License
*******

Copyright 2023 @ Fernando Cerqueira

HealthCheckPlus is licensed under the MIT license. See https://github.com/FRACerqueira/HealthCheckPlus/blob/master/LICENSE.
