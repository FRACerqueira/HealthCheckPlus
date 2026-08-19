using HealthCheckPlus.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlusDemo.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController(IStateHealthChecksPlus stateHealthChecks, ILogger<WeatherForecastController> logger) : ControllerBase
    {

        private static readonly string[] Summaries =
        [
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        ];

        private readonly ILogger<WeatherForecastController> _logger = logger;

        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            // Read a single dependency's cached status without waiting for it to run again -
            // useful when a business action depends on one specific check, not the whole app.
            if (stateHealthChecks.StatusResult("HcTest1").Status != HealthStatus.Healthy)
            {
                _logger.LogWarning("HcTest1 is not Healthy; forecast data may be degraded.");
            }

            // Read the same aggregate status the "live" endpoint reports, from application code
            // instead of an HTTP call - e.g. to skip non-essential work while the app is unhealthy.
            if (stateHealthChecks.Status("live") != HealthStatus.Healthy)
            {
                _logger.LogWarning("Application is not Healthy; consider degrading this response.");
            }

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
