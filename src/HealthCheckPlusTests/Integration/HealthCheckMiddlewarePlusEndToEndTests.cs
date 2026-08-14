// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using System.Net;
using System.Text.Json;
using HealthCheckPlus.options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlusTests.Integration
{
    // End-to-end coverage for the action plan (doc/plano-acao-healthcheckplus.md), step P3.4 —
    // the middleware (HealthCheckMiddlewarePlus): status code mapping and the ready-made response
    // writers, through a real HTTP request. Previously zero coverage (audit's "Motor de
    // orquestração sem nenhum teste automatizado" finding, doc/healthcheckplus-audit.html).
    public class HealthCheckMiddlewarePlusEndToEndTests
    {
        private sealed class AlwaysUnhealthyCheck : IHealthCheck
        {
            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("simulated failure"));
            }
        }

        [Fact]
        public async Task GetHealth_ShouldMapUnhealthyStatus_ToConfiguredStatusCode()
        {
            using var host = await TestHost.CreateAsync(
                services =>
                {
                    services.AddLogging();
                    var ihb = services.AddHealthChecksPlus(["Test1"]);
                    ihb.AddCheckPlus<AlwaysUnhealthyCheck>("Test1");
                },
                app => app.UseHealthChecksPlus("/health", new HealthCheckPlusOptions
                {
                    HealthCheckName = "live",
                    ResultStatusCodes =
                    {
                        [HealthStatus.Healthy] = StatusCodes.Status200OK,
                        [HealthStatus.Degraded] = StatusCodes.Status200OK,
                        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
                    }
                }));

            var client = host.GetTestClient();
            var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        [Fact]
        public async Task GetHealth_WithWriteDetailsWithException_ShouldIncludeExceptionAndDescription()
        {
            using var host = await TestHost.CreateAsync(
                services =>
                {
                    services.AddLogging();
                    var ihb = services.AddHealthChecksPlus(["Test1"]);
                    ihb.AddCheckPlus<AlwaysUnhealthyCheck>("Test1");
                },
                app => app.UseHealthChecksPlus("/health", new HealthCheckPlusOptions
                {
                    ResponseWriter = HealthCheckPlusOptions.WriteDetailsWithException,
                    ResultStatusCodes =
                    {
                        [HealthStatus.Healthy] = StatusCodes.Status200OK,
                        [HealthStatus.Degraded] = StatusCodes.Status200OK,
                        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
                    }
                }));

            var client = host.GetTestClient();
            var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;

            Assert.Equal("Unhealthy", root.GetProperty("status").GetString());
            var entry = root.GetProperty("entries").EnumerateArray().Single();
            Assert.Equal("Test1", entry.GetProperty("name").GetString());
            Assert.Equal("simulated failure", entry.GetProperty("description").GetString());
        }
    }
}
