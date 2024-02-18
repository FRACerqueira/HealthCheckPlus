// ********************************************************************************************
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// ********************************************************************************************
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace HealthCheckPlus.Internal.WrapperMicrosoft
{
    internal class HealthCheckMiddlewarePlus 
    {
        private readonly HealthCheckPlusOptions _healthCheckOptions;
        private readonly DefaultHealthCheckServicePlus _healthCheckService;
        public HealthCheckMiddlewarePlus(RequestDelegate next, IOptions<HealthCheckPlusOptions> healthCheckOptions, HealthCheckService healthCheckService) 
        {
            ArgumentNullException.ThrowIfNull(next);

            ArgumentNullException.ThrowIfNull(healthCheckOptions);

            ArgumentNullException.ThrowIfNull(healthCheckService);

            _healthCheckOptions = healthCheckOptions.Value;
            _healthCheckService = (DefaultHealthCheckServicePlus)healthCheckService;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            HealthReport healthReport = await _healthCheckService.CheckHealthPlusAsync(_healthCheckOptions.Predicate, _healthCheckOptions.StatusHealthReport, HealthCheckTrigger.UrlRequest, httpContext.RequestAborted);
            if (!_healthCheckOptions.ResultStatusCodes.TryGetValue(healthReport.Status, out var value))
            {
                throw new InvalidOperationException(string.Format("No status code mapping found for {0} value: {1}.", "HealthStatus", healthReport.Status) + "HealthCheckOptions.ResultStatusCodes must contain" + $"an entry for {healthReport.Status}.");
            }

            httpContext.Response.StatusCode = value;
            if (!_healthCheckOptions.AllowCachingResponses)
            {
                IHeaderDictionary headers = httpContext.Response.Headers;
                headers["Cache-Control"] = "no-store, no-cache";
                headers["Pragma"] = "no-cache";
                headers["Expires"] = "Thu, 01 Jan 1970 00:00:00 GMT";
            }

            if (_healthCheckOptions.ResponseWriter != null)
            {
                await _healthCheckOptions.ResponseWriter(httpContext, healthReport);
            }
        }
    }
}
