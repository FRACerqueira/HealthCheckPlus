using HealthCheckPlus;
using HealthCheckPlusDemo;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net;
using System.Text.Json;

IStateHealthChecksPlus? _stateHealthChecksPlus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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


var app = builder.Build();

//save interfaces IStateHealthChecksPlus
using (IServiceScope startscope = app.Services.CreateScope())
{
    _stateHealthChecksPlus = startscope.ServiceProvider.GetRequiredService<IStateHealthChecksPlus>();
}


//Endpoints HC
app.UseHealthChecksPlus("/health/live", HttpStatusCode.OK)
   .UseHealthChecksPlusStatus("/health/ready", HttpStatusCode.OK);

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


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

