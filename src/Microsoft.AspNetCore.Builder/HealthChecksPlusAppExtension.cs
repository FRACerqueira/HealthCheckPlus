// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus;
using HealthCheckPlus.Internal;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// HealthChecksPlus Extension for IApplicationBuilder
/// </summary>
public static class HealthChecksPlusAppExtension
{
    /// <summary>
    /// HealthcheckPlus : Adds a middleware that provides health check status.
    /// </summary>
    /// <param name="app">The <see cref="IApplicationBuilder"/>.</param>
    /// <param name="path">The The path on which to provide health check status.</param>
    /// <param name="options">The <see cref="HealthCheckOptions"/> used to configure.</param>
    /// <remarks>
    /// If path is set to null or the empty string then the health check middleware will
    /// ignore the URL path and process all requests. If path is set to a non-empty value,
    /// the health check middleware will process requests with a URL that matches the
    /// provided value of path case-insensitively, allowing for an extra trailing slash
    ///('/') character.
    /// </remarks>
    /// <returns>The <see cref="IApplicationBuilder"/>.</returns>


    public static IApplicationBuilder UseHealthChecksPlus(this IApplicationBuilder app, PathString path,  HealthCheckOptions options)
    {
        app.UseHealthChecks(path, options);
        return app;
    }

    /// <summary>
    /// HealthcheckPlus : Adds a middleware that provides health check status.
    /// </summary>
    /// <param name="app">The <see cref="IApplicationBuilder"/>.</param>
    /// <param name="path">The The path on which to provide health check status.</param>
    /// <param name="UnhealthySta">The <see cref="HttpStatusCode"/> used when application status is Unhealthy. Defaut value is 503(ServiceUnavailable)</param>
    /// <param name="DegradedSta">The <see cref="HttpStatusCode"/> used when application status is Degraded. Defaut value is 200(Ok)</param>
    /// <param name="HealthySta">The <see cref="HttpStatusCode"/> used when application status is Healthy. Defaut value is 200(Ok)</param>
    /// <returns>The <see cref="IApplicationBuilder"/>.</returns>
    public static IApplicationBuilder UseHealthChecksPlus(this IApplicationBuilder app, PathString path, HttpStatusCode UnhealthySta = HttpStatusCode.ServiceUnavailable, HttpStatusCode DegradedSta = HttpStatusCode.OK, HttpStatusCode HealthySta = HttpStatusCode.OK)
    {
        var srv = app.ApplicationServices.GetRequiredService<IOptions<HealthCheckPlusOptions>>();
        var opt = new HealthCheckOptions
        {
            Predicate = r => r.Name == srv.Value.NameCheckApp,
        };
        opt.ResultStatusCodes[HealthStatus.Healthy] = (int)HealthySta;
        opt.ResultStatusCodes[HealthStatus.Degraded] = (int)DegradedSta;
        opt.ResultStatusCodes[HealthStatus.Unhealthy] = (int)UnhealthySta;
        return app.UseHealthChecksPlus(path, opt);
    }

    /// <summary>
    /// HealthcheckPlus : Adds a middleware that provides health check status with default json details from all health checks.
    /// </summary>
    /// <param name="app">The <see cref="IApplicationBuilder"/>.</param>
    /// <param name="path">The The path on which to provide health check status.</param>
    /// <param name="UnhealthySta">The <see cref="HttpStatusCode"/> used when application status is Unhealthy. Defaut value is 503(ServiceUnavailable)</param>
    /// <param name="DegradedSta">The <see cref="HttpStatusCode"/> used when application status is Degraded. Defaut value is 200(Ok)</param>
    /// <param name="HealthySta">The <see cref="HttpStatusCode"/> used when application status is Healthy. Defaut value is 200(Ok)</param>
    /// <param name="responseWriter">The a delegate used to write the response</param>
    /// <returns>The <see cref="IApplicationBuilder"/>.</returns>
    public static IApplicationBuilder UseHealthChecksPlusStatus(this IApplicationBuilder app, PathString path, HttpStatusCode UnhealthySta = HttpStatusCode.ServiceUnavailable, HttpStatusCode DegradedSta = HttpStatusCode.OK, HttpStatusCode HealthySta = HttpStatusCode.OK, Func<HttpContext, HealthReport, Task> responseWriter = null)
    {
        var srv = app.ApplicationServices.GetRequiredService<IOptions<HealthCheckPlusOptions>>();
        var opt = new HealthCheckOptions
        {
            Predicate = r => r.Name == srv.Value.NameCheckApp,
            ResponseWriter = responseWriter??ResponseAsJson()
        };
        opt.ResultStatusCodes[HealthStatus.Healthy] = (int)HealthySta;
        opt.ResultStatusCodes[HealthStatus.Degraded] = (int)DegradedSta;
        opt.ResultStatusCodes[HealthStatus.Unhealthy] = (int)UnhealthySta;
        return app.UseHealthChecksPlus(path, opt);
    }
    private static Func<HttpContext, HealthReport, Task> ResponseAsJson()
    {
        return async (c, r) =>
        {
            c.Response.ContentType = "application/json";
            string result = JsonSerializer.Serialize(new
            {
                status = r.Status.ToString(),
                description = r.Entries.FirstOrDefault().Value.Description,
                data = r.Entries.FirstOrDefault().Value.Data.Select(e =>
                {
                    var det = (DataResutStatus)e.Value;
                    return new { dep = det.Name, status = det.Status.ToString(), description = det.Description , date = det.Date, elapsedtime = det.ElapsedTime};
                })
            }, new JsonSerializerOptions { WriteIndented = true });
            await c.Response.WriteAsync(result);
        };
    }
}
