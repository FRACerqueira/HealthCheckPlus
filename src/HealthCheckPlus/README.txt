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

Per-status polling policies, cached results, and a smarter publisher pipeline for ASP.NET Core
health checks.

HealthCheckPlus is written in C#, targeting .NET 10, .NET 9, and .NET 8. It builds on top of
ASP.NET Core's native health check system (Microsoft.Extensions.Diagnostics.HealthChecks) rather
than replacing it - your existing IHealthCheck implementations and third-party check packages keep
working unchanged.

Before you build on it, see the plain-language points of attention:
https://github.com/FRACerqueira/HealthCheckPlus/blob/main/docs/POINTS_OF_ATTENTION.md

Features
********
- Per-status scheduling: each health check can poll at a different rate depending on its own
  last-known status - for example, every 30 seconds while healthy but every 5 while degraded.
  Configure a Healthy policy (the default, always required), and optionally a Degraded and/or
  Unhealthy policy for each check.
- A cache of the last result per check: a request to /health doesn't necessarily re-run every
  check synchronously - it reads whatever the last poll already produced. You can also read the
  cached result for any check, or force one to Unhealthy/Degraded directly from application code
  (e.g. after catching an exception talking to a dependency).
- Adopting external/third-party checks: register a check from any existing IHealthChecksBuilder
  extension (e.g. AddRedis(...)) and give it its own delay, period, and policy rules the same way
  as a custom check.
- An optional background service: runs checks on its own schedule, independent of HTTP traffic,
  and drives every registered IHealthCheckPublisher with two extra filters on top of the native
  publisher pipeline:
    - Publish only every N idle cycles, not every cycle.
    - Publish only when the aggregate report actually changed since the last publish.
    - A publisher can add its own custom gating via IHealthCheckPlusPublisher.PublisherCondition.
- Six response templates, all serialized as "application/json; charset=utf-8", from a short
  status-only body up to full details with descriptions and exceptions:
    - HealthCheckPlusOptions.WriteShortDetails / WriteShortDetailsPlus
    - HealthCheckPlusOptions.WriteDetailsWithoutException / WriteDetailsWithoutExceptionPlus
    - HealthCheckPlusOptions.WriteDetailsWithException / WriteDetailsWithExceptionPlus
    - The ...Plus writers take an extra IStateHealthChecksPlus parameter, so ResponseWriter needs a
      small lambda rather than a direct method reference: ResponseWriter = (ctx, report) =>
      HealthCheckPlusOptions.WriteDetailsWithExceptionPlus(ctx, report, stateHealthChecksPlus)
      (see the Samples for a full working example)
- Native metrics via System.Diagnostics.Metrics (check executions, status transitions, publisher
  invocations) - no extra package dependency, consumable by any exporter (OpenTelemetry,
  Prometheus, App Insights, ...).
- A fluent, chainable API that extends the native health check builder rather than replacing it.

What's new
----------

See the full changelog: https://github.com/FRACerqueira/HealthCheckPlus/blob/main/CHANGELOG.md

Examples
********

See folder : https://github.com/FRACerqueira/HealthCheckPlus/tree/main/Samples

Usage
*****

//At Startup / Program (without background services policies)
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

//At Startup / Program (with background services policies)
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


//At Startup / Program

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

//At Startup / Program

//example of use in the middleware pipeline
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
public class MyBusiness
{
    public MyBusiness(IStateHealthChecksPlus healthCheckApp)
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

HealthCheckPlus is licensed under the MIT license. See https://github.com/FRACerqueira/HealthCheckPlus/blob/main/LICENSE.
