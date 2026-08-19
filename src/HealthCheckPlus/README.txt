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

HealthCheckPlus was developed in c# with the **.Net10**, **.Net9** and **.Net8** target frameworks.

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
        - Optional per-publisher custom condition via IHealthCheckPlusPublisher.PublisherCondition.
- Response templates with small/full details in "application/json; charset=utf-8" ContentType
    - HealthCheckPlusOptions.WriteShortDetails
    - HealthCheckPlusOptions.WriteShortDetailsPlus (with extra fields : cache source and reference date of last run)
    - HealthCheckPlusOptions.WriteDetailsWithoutException
    - HealthCheckPlusOptions.WriteDetailsWithoutExceptionPlus (with extra fields : cache source and reference date of last run)
    - HealthCheckPlusOptions.WriteDetailsWithException
    - HealthCheckPlusOptions.WriteDetailsWithExceptionPlus (with extra fields : cache source and reference date of last run)
    - The ...Plus writers take an extra IStateHealthChecksPlus parameter, so ResponseWriter needs a small lambda rather than a direct method reference: ResponseWriter = (ctx, report) => HealthCheckPlusOptions.WriteDetailsWithExceptionPlus(ctx, report, stateHealthChecksPlus) (see the Samples for a full working example)
- Simple and clear fluent syntax extending the native features of healt check
- Native metrics via System.Diagnostics.Metrics (check executions, status transitions, publisher invocations) - no extra package dependency, consumable by any exporter (OpenTelemetry, Prometheus, App Insights, ...)

What's new
----------

See the full changelog: https://github.com/FRACerqueira/HealthCheckPlus/blob/main/CHANGELOG.md

Examples
********

See folder : https://github.com/FRACerqueira/HealthCheckPlus/tree/main/Samples

Usage
*****

//At Statup / Program (without background services policies)
builder.Services
    //Add HealthCheckPlus - the set of tracked checks comes from whatever ends up registered
    //below (AddCheckPlus/AddCheckLinkTo/native AddCheck), no separate list to keep in sync.
    //A check added only via a native extension still needs AddCheckPlus/AddCheckLinkTo for
    //its own Healthy policy below, or the host fails fast at startup naming it.
    .AddHealthChecksPlus()
    //your custom HC    
    .AddCheckPlus<HcTeste1>("HcTest1")
    //your custom HC    
    .AddCheckPlus<HcTeste2>("HcTest2", failureStatus: HealthStatus.Degraded)
    //external HC 
    .AddRedis("connection string", "MyRedis")
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
    .AddHealthChecksPlus()
    //your custom HC with custom delay and period
    .AddCheckPlus<HcTeste1>("HcTest1", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10))
    //your custom HC without delay and period (using BackgroundPolicy)     
    .AddCheckPlus<HcTeste2>("HcTest2", failureStatus: HealthStatus.Degraded)
    //external HC 
    .AddRedis("connection string", "MyRedis")
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
        //Publishing.Enabled defaults to false - assigning a new PublishingOptions() (as below) is
        //what turns it on. AfterIdleCount/WhenReportChange below happen to match its own defaults,
        //but the assignment itself is not redundant boilerplate: deleting this block silently
        //disables all publishing, with no log or metric signal.
        opt.Publishing = new PublishingOptions()
        {
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
        if (healthCheckApp.StatusResult("HcTest2").Status == HealthStatus.Unhealthy)
        {
            //do something. This dependency 'HcTest2' is not available
        }
        try
        {
            //redis access
        }
        catch (ExceptionRedis rex)
        {
            healthCheckApp.SwitchToUnhealthy("Redis");
        }
    }
}

...

//example of  Publisher condition to execute
public class SamplePublishHealth : IHealthCheckPlusPublisher
{
   public Func<HealthReport, bool>? PublisherCondition { get; set; } = (_) => true;
   public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
   {
      return Task.CompletedTask;
   }
}

License
*******

Copyright 2023 @ Fernando Cerqueira

HealthCheckPlus is licensed under the MIT license. See https://github.com/FRACerqueira/HealthCheckPlus/blob/master/LICENSE.
