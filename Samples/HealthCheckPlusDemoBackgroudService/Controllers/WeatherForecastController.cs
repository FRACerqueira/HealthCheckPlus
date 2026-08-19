using HealthCheckPlus.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlusDemoBackgroudService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController(IStateHealthChecksPlus stateHealthChecks, ILogger<WeatherForecastController> logger) : ControllerBase
    {
        private static readonly string[] Summaries =
        [
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        ];

        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            if (stateHealthChecks.StatusResult("HcTest1").Status != HealthStatus.Healthy)
            {
                logger.LogWarning("HcTest1 is not Healthy; forecast data may be degraded.");
            }
            if (stateHealthChecks.Status("live") != HealthStatus.Healthy)
            {
                logger.LogWarning("Application is not Healthy; consider degrading this response.");
            }

            // Manual override (IStateHealthChecksPlus.SwitchToDegraded/SwitchToUnhealthy): forces a
            // status change from application code, not from a poll - e.g. after catching an
            // exception from the dependency this check represents. Called unconditionally here
            // only to make the background publisher's "publish on change" filter fire on demand;
            // in a real app this call would sit inside a catch block.
            stateHealthChecks.SwitchToDegraded("HcTest1");

            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }
    }
}
