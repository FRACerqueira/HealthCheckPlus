using HealthCheckPlus;
using HealthCheckPlus.options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace HealthCheckPlusDemo
{
    public class Program
    {
        public static void Main(string[] args)
        {
            IStateHealthChecksPlus? _stateHealthChecksPlus;

            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

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

            var app = builder.Build();

            //save interfaces IStateHealthChecksPlus
            using (IServiceScope startscope = app.Services.CreateScope())
            {
                _stateHealthChecksPlus = startscope.ServiceProvider.GetRequiredService<IStateHealthChecksPlus>();
            }


            //Endpoints HC
            app.UseHealthChecksPlus("/health/live", new HealthCheckPlusOptions
                {
                    HealthCheckName = "live",
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
                    ResultStatusCodes =
                            {
                                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
                            }
                })
               .UseHealthChecksPlus("/health/ready", new HealthCheckPlusOptions
               {
                   ResponseWriter = (ctx, report) => HealthCheckPlusOptions.WriteDetailsWithoutExceptionPlus(ctx, report, _stateHealthChecksPlus),
                   ResultStatusCodes =
                    {
                        [HealthStatus.Healthy] = StatusCodes.Status200OK,
                        [HealthStatus.Degraded] = StatusCodes.Status200OK,
                        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
                    }
               });

            //middler pipeline
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
        }
    }
}
