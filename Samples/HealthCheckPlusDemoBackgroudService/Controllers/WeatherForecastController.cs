using HealthCheckPlus.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlusDemoBackgroudService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController(IStateHealthChecksPlus stateHealthChecks) : ControllerBase
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
                //do something
            }
            if (stateHealthChecks.Status("live") != HealthStatus.Healthy)
            {
                //do something
            }

            //change status to force the pubisher report (sample only)
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
