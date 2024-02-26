// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using System.Text.Json;
using System.Text.Json.Serialization;
using HealthCheckPlus.Abstractions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlus.options
{
    /// <summary>
    /// Contains optionsSerilz for the <see cref="HealthCheckMiddleware"/>.
    /// </summary>
    public class HealthCheckPlusOptions : HealthCheckOptions
    {
        private static readonly JsonSerializerOptions optionsSerilz = new() 
        { 
            WriteIndented = true, 
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull 
        };

        /// <summary>
        /// Response template with small details of the HealthCheck report
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/>.</param>
        /// <param name="report">The <see cref="HealthReport"/>.</param>
        /// <returns><see cref="Task"/>.</returns>
        public static Task WriteShortDetails(HttpContext context, HealthReport report)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(report);

            context.Response.ContentType = "application/json";
            var result = JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                entries = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString()
                })
            }, options: optionsSerilz);
            return context.Response.WriteAsync(result);
        }

        /// <summary>
        /// Response template with small details of the HealthCheck report, cache source and reference date of last run
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/>.</param>
        /// <param name="report">The <see cref="HealthReport"/>.</param>
        /// <param name="statecache">The cache instance : <see cref="IStateHealthChecksPlus"/>.</param>
        /// <returns><see cref="Task"/>.</returns>
        public static Task WriteShortDetailsPlus(HttpContext context, HealthReport report, IStateHealthChecksPlus statecache)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(report);
            ArgumentNullException.ThrowIfNull(statecache);

            var lst = statecache.ConvertToPlus(report);
            context.Response.ContentType = "application/json; charset=utf-8";
            var result = JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                entries = lst.Select(e => new
                {
                    name = e.Name,
                    status = e.Lastresult.Status.ToString(),
                    dateref = e.Dateref,
                    origin = e.Origin.ToString()
                })
            }, options: optionsSerilz);
            return context.Response.WriteAsync(result);
        }

        /// <summary>
        /// Response template with details of the HealthCheck report (without exception)
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/>.</param>
        /// <param name="report">The <see cref="HealthReport"/>.</param>
        /// <returns><see cref="Task"/>.</returns>
        public static Task WriteDetailsWithoutException(HttpContext context, HealthReport report)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(report);

            context.Response.ContentType = "application/json; charset=utf-8";
            var result = JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                entries = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = e.Value.Duration
                })
            }, options: optionsSerilz);
            return context.Response.WriteAsync(result);
        }

        /// <summary>
        /// Response template with small details of the HealthCheck report (without exception), cache source and reference date of last run
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/>.</param>
        /// <param name="report">The <see cref="HealthReport"/>.</param>
        /// <param name="statecache">The cache instance : <see cref="IStateHealthChecksPlus"/>.</param>
        /// <returns><see cref="Task"/>.</returns>
        public static Task WriteDetailsWithoutExceptionPlus(HttpContext context, HealthReport report, IStateHealthChecksPlus statecache)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(report);
            ArgumentNullException.ThrowIfNull(statecache);

            var lst = statecache.ConvertToPlus(report);
            context.Response.ContentType = "application/json; charset=utf-8";
            var result = JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                entries = lst.Select(e => new
                {
                    name = e.Name,
                    status = e.Lastresult.Status.ToString(),
                    description = e.Lastresult.Description,
                    dateref = e.Dateref,
                    duration = e.Duration,
                    origin = e.Origin.ToString()
                })
            }, options: optionsSerilz);
            return context.Response.WriteAsync(result);
        }

        /// <summary>
        /// Response template with small details of the HealthCheck report (with exception)
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/>.</param>
        /// <param name="report">The <see cref="HealthReport"/>.</param>
        /// <returns><see cref="Task"/>.</returns>
        public static Task WriteDetailsWithException(HttpContext context, HealthReport report)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(report);

            context.Response.ContentType = "application/json; charset=utf-8";
            var result = JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                entries = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    exception = e.Value.Exception?.ToString().Replace(Environment.NewLine,""),
                    duration = e.Value.Duration
                })
            }, options: optionsSerilz);
            return context.Response.WriteAsync(result);
        }


        /// <summary>
        /// Response template with small details of the HealthCheck report (with exception), cache source and reference date of last run
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/>.</param>
        /// <param name="report">The <see cref="HealthReport"/>.</param>
        /// <param name="statecache">The cache instance : <see cref="IStateHealthChecksPlus"/>.</param>
        /// <returns><see cref="Task"/>.</returns>
        public static Task WriteDetailsWithExceptionPlus(HttpContext context, HealthReport report, IStateHealthChecksPlus statecache)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(report);
            ArgumentNullException.ThrowIfNull(statecache);

            var lst = statecache.ConvertToPlus(report);
            context.Response.ContentType = "application/json; charset=utf-8";
            var result = JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                entries = lst.Select(e => new
                {
                    name = e.Name,
                    status = e.Lastresult.Status.ToString(),
                    description = e.Lastresult.Description,
                    dateref = e.Dateref,
                    duration = e.Duration,
                    exception = e.Lastresult.Exception?.ToString().Replace(Environment.NewLine, ""),
                    origin = e.Origin.ToString()
                })
            }, options: optionsSerilz);
            return context.Response.WriteAsync(result);
        }

        /// <summary>
        /// Gets or sets the Agregate Status for HealthReport. Default value is min() of all status reported
        /// </summary>
        public Func<HealthReport, HealthStatus>? StatusHealthReport { get; set; }

        /// <summary>
        /// Gets or sets the name of Agregate Status for HealthReport.
        /// </summary>
        public string? HealthCheckName { get; set; }
    }
}
