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

        private sealed class AlwaysHealthyCheck : IHealthCheck
        {
            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(HealthCheckResult.Healthy());
            }
        }

        // HttpContext.Connection.LocalPort is settable directly even without a real socket bound,
        // which is what lets a TestServer-backed request exercise the port-matching predicate
        // inside HealthChecksPlusAppExtension.UseHealthChecksCore.
        private static void UsePortFromHeader(IApplicationBuilder app)
        {
            app.Use(async (ctx, next) =>
            {
                if (ctx.Request.Headers.TryGetValue("X-Test-Port", out var portHeader) && int.TryParse(portHeader, out var port))
                {
                    ctx.Connection.LocalPort = port;
                }
                await next();
            });
        }

        private static HttpRequestMessage HealthRequest(int port) =>
            new(HttpMethod.Get, "/health") { Headers = { { "X-Test-Port", port.ToString() } } };

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

        // Closes the coverage gap noted in doc/progresso-plano-acao.md (Fase 3 follow-up): the
        // UseHealthChecksPlus(path, port) overload had never been exercised by any test.
        [Fact]
        public async Task UseHealthChecksPlus_WithPortOverload_ShouldOnlyMatchConfiguredPort()
        {
            const int expectedPort = 5443;

            using var host = await TestHost.CreateAsync(
                services =>
                {
                    services.AddLogging();
                    var ihb = services.AddHealthChecksPlus(["Test1"]);
                    ihb.AddCheckPlus<AlwaysHealthyCheck>("Test1");
                },
                app =>
                {
                    UsePortFromHeader(app);
                    app.UseHealthChecksPlus("/health", expectedPort);
                    app.Run(async ctx =>
                    {
                        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                        await ctx.Response.WriteAsync("not-health");
                    });
                });

            var client = host.GetTestClient();

            var matchingResponse = await client.SendAsync(HealthRequest(expectedPort), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, matchingResponse.StatusCode);

            var mismatchingResponse = await client.SendAsync(HealthRequest(expectedPort + 1), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, mismatchingResponse.StatusCode);
            Assert.Equal("not-health", await mismatchingResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }

        // Closes the coverage gap noted in doc/progresso-plano-acao.md (Fase 3 follow-up): the
        // UseHealthChecksPlus(path, port, options) overload had never been exercised by any test.
        [Fact]
        public async Task UseHealthChecksPlus_WithPortAndOptionsOverload_ShouldApplyOptionsOnlyWhenPortMatches()
        {
            const int expectedPort = 6443;

            using var host = await TestHost.CreateAsync(
                services =>
                {
                    services.AddLogging();
                    var ihb = services.AddHealthChecksPlus(["Test1"]);
                    ihb.AddCheckPlus<AlwaysHealthyCheck>("Test1");
                },
                app =>
                {
                    UsePortFromHeader(app);
                    app.UseHealthChecksPlus("/health", expectedPort, new HealthCheckPlusOptions
                    {
                        ResponseWriter = HealthCheckPlusOptions.WriteShortDetails
                    });
                    app.Run(async ctx =>
                    {
                        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                        await ctx.Response.WriteAsync("not-health");
                    });
                });

            var client = host.GetTestClient();

            var matchingResponse = await client.SendAsync(HealthRequest(expectedPort), TestContext.Current.CancellationToken);
            var body = await matchingResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            using var json = JsonDocument.Parse(body);
            Assert.Equal("Healthy", json.RootElement.GetProperty("status").GetString());

            var mismatchingResponse = await client.SendAsync(HealthRequest(expectedPort + 1), TestContext.Current.CancellationToken);
            Assert.Equal("not-health", await mismatchingResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }
    }
}
