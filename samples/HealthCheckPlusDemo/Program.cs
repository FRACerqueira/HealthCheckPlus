using HealthCheckPlus;
using HealthCheckPlusDemo;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddHealthChecks<MyEnum>("AppHealthCheck", (deps) =>
        {
            if (deps.StatusDep(MyEnum.HcTest2).Status != HealthStatus.Healthy)
            {
                return HealthStatus.Unhealthy;
            }
            var alldeps = (MyEnum[])Enum.GetValues(typeof(MyEnum));
            var result = HealthStatus.Healthy;
            foreach (var item in alldeps)
            {
                if (deps.StatusDep(item).Status != HealthStatus.Healthy)
                {
                    result = HealthStatus.Degraded;
                    break;
                }
            }
            return result;
        },
        "HealthCheckPlusDemo", 
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
    .AddRedis("connection string", "Myredis")
    .AddCheckPlus<MyEnum, HcTeste1>(MyEnum.HcTest1, TimeSpan.FromSeconds(1600), TimeSpan.FromSeconds(1600))
    .AddCheckPlus<MyEnum, HcTeste2>(MyEnum.HcTest2, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1600), failureStatus: HealthStatus.Degraded)
    .AddCheckRegistered(MyEnum.Redis, "MyRedis", TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1600))
    .AddUnhealthyPolicy(MyEnum.HcTest1, TimeSpan.FromSeconds(1))
    .AddDegradedPolicy(MyEnum.HcTest2, TimeSpan.FromSeconds(1));


var app = builder.Build();

//example usage
using (IServiceScope startscope = app.Services.CreateScope())
{
    var healthCheckApp = app.Services.GetRequiredService<IStateHealthChecksPlus>();
    if (healthCheckApp.StatusApp.Status == HealthStatus.Unhealthy)
    { 
    }
    var _ = healthCheckApp.StatusDep(MyEnum.HcTest1);
}

app.UseHealthChecksPlus("/health/ready", HttpStatusCode.OK)
   .UseHealthChecksPlusStatus("/health/Live", HttpStatusCode.OK);

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
