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

HealthCheck with individual delay and interval and interval policy for unhealthy/degraded status.

HealthCheckPlus was developed in c# with the **netstandard2.1**, **.Net6** and **.Net7** target frameworks.

Visit the official page for more documentation : https://fracerqueira.github.io/HealthCheckPlus

What's new V1.0.4
*****************

- First Release G.A

Features
********

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

Examples
********

See folder : https://github.com/FRACerqueira/HealthCheckPlus/tree/main/Samples

Sample Usage
************

//Create enum with all HealthCheck
public enum MyEnum
{
    HcTeste1,
    HcTeste2,
    Redis
}

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

//At Statup / Program

app.UseHealthChecksPlus("/health/ready", HttpStatusCode.OK)
   .UseHealthChecksPlusStatus("/health/Live", HttpStatusCode.OK);


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

License
*******

Copyright 2023 @ Fernando Cerqueira

HealthCheckPlus is licensed under the MIT license. See https://github.com/FRACerqueira/HealthCheckPlus/blob/master/LICENSE.

