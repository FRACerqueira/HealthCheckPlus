using HealthCheckPlus.Abstractions;
using HealthCheckPlus.options;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace HealthCheckPlusDemoBackgroudService
{

    public class Program
    {
        private static readonly string[] names = ["HcTest1", "HcTest2", "Redis"];

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
                .AddHealthChecksPlus(names)
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

            builder.Services.AddSingleton<IHealthCheckPublisher, SamplePublishHealth>();

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
                ResultStatusCodes =
                            {
                                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
                            }
            })
               .UseHealthChecksPlus("/health/ready")
               .UseHealthChecks("/health/Test", new HealthCheckOptions
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
