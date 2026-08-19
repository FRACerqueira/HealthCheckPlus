using HealthCheckPlus.Options;
using HealthCheckPlusDemoMetrics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlusDemoMetrics
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services
                //Add HealthCheckPlus
                .AddHealthChecksPlus()
                //a check that cycles Healthy/Degraded/Unhealthy on every run - see FlakyCheck.cs
                .AddCheckPlus<FlakyCheck>("Flaky")
                //run continuously in the background, independent of any HTTP traffic, so metrics
                //keep flowing even if nothing ever calls /health
                .AddBackgroundPolicy(opt =>
                {
                    opt.Delay = TimeSpan.FromSeconds(1);
                    opt.Idle = TimeSpan.FromSeconds(1);
                    opt.AllStatusPeriod(TimeSpan.FromSeconds(2));
                    //publish every idle cycle regardless of whether the report changed, so this
                    //sample's console output stays predictable to watch
                    opt.Publishing = new PublishingOptions
                    {
                        AfterIdleCount = 1,
                        WhenReportChange = false
                    };
                });

            builder.Services.AddSingleton<IHealthCheckPublisher, MetricsDemoPublisher>();

            //the actual point of this sample: prints every healthcheckplus.* measurement to the
            //console as it's recorded - see MetricsConsoleListener.cs
            builder.Services.AddHostedService<MetricsConsoleListener>();

            var app = builder.Build();

            //full details endpoint, in case you want to curl it while this sample is running
            app.UseHealthChecksPlus("/health", new HealthCheckPlusOptions
            {
                ResponseWriter = HealthCheckPlusOptions.WriteDetailsWithoutException
            });

            app.Run();
        }
    }
}
