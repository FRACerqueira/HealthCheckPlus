using HealthCheckPlus;
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
            if (stateHealthChecks.StatusResult(MyEnum.HcTest1).Status != HealthStatus.Healthy)
            {
                //do something
            }
            if (stateHealthChecks.Status("live") != HealthStatus.Healthy)
            {
                //do something
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
