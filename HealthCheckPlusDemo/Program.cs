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
    .AddHealthChecks<MyEnum>("AppHealthCheck", HealthStatus.Degraded)
    .AddRedis("teste1", "Myredis")
    .AddCheckPlus<MyEnum, HcTeste1>(MyEnum.HcTest1)
    .AddCheckPlus<MyEnum, HcTeste2>(MyEnum.HcTest2, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), failureStatus: HealthStatus.Degraded)
    .AddCheckRegistered(MyEnum.Redis, "MyRedis", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30))
    .AddUnhealthyPolicy(MyEnum.Redis, TimeSpan.FromSeconds(1));


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
   .UseHealthChecksPlus("/health/Live", HttpStatusCode.OK)
   .UseHealthChecksPlusStatus("/health/Status", HttpStatusCode.OK);

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

