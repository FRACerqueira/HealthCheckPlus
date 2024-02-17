// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Internal;
using HealthCheckPlus.Internal.WrapperMicrosoft;
using HealthCheckPlus.options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace HealthCheckPlus.Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// HealthChecksPlus Extension for IApplicationBuilder
    /// </summary>
    public static class HealthChecksPlusAppExtension
    {

        /// <summary>
        /// Adds a middleware that provides health check status.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/>.</param>
        /// <param name="path">The path on which to provide health check status.</param>
        /// <returns>A reference to the <paramref name="app"/> after the operation has completed.</returns>
        /// <remarks>
        /// <para>
        /// If <paramref name="path"/> is set to <c>null</c> or the empty string then the health check middleware
        /// will ignore the URL path and process all requests. If <paramref name="path"/> is set to a non-empty
        /// value, the health check middleware will process requests with a URL that matches the provided value
        /// of <paramref name="path"/> case-insensitively, allowing for an extra trailing slash ('/') character.
        /// </para>
        /// <para>
        /// The health check middleware will use default settings from <see cref="IOptions{HealthCheckOptions}"/>.
        /// </para>
        /// </remarks>
        public static IApplicationBuilder UseHealthChecksPlus(this IApplicationBuilder app, PathString path)
        {
            ArgumentNullException.ThrowIfNull(app);
            UseHealthChecksCore(app, path, port: null, []);
            return app;
        }

        /// <summary>
        /// Adds a middleware that provides health check status.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/>.</param>
        /// <param name="path">The path on which to provide health check status.</param>
        /// <param name="port">The port to listen on. Must be a local port on which the server is listening.</param>
        /// <returns>A reference to the <paramref name="app"/> after the operation has completed.</returns>
        /// <remarks>
        /// <para>
        /// If <paramref name="path"/> is set to <c>null</c> or the empty string then the health check middleware
        /// will ignore the URL path and process all requests on the specified port. If <paramref name="path"/> is
        /// set to a non-empty value, the health check middleware will process requests with a URL that matches the
        /// provided value of <paramref name="path"/> case-insensitively, allowing for an extra trailing slash ('/')
        /// character.
        /// </para>
        /// <para>
        /// The health check middleware will use default settings from <see cref="IOptions{HealthCheckOptions}"/>.
        /// </para>
        /// </remarks>
        public static IApplicationBuilder UseHealthChecksPlus(this IApplicationBuilder app, PathString path, int port)
        {
            ArgumentNullException.ThrowIfNull(app);
            UseHealthChecksCore(app, path, port, []);
            return app;
        }

        /// <summary>
        /// Adds a middleware that provides health check status.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/>.</param>
        /// <param name="path">The The path on which to provide health check status.</param>
        /// <param name="options">The <see cref="HealthCheckPlusOptions"/> used to configure.</param>
        /// If path is set to null or the empty string then the health check middleware will
        /// <remarks>
        /// ignore the URL path and process all requests. If path is set to a non-empty value,
        /// the health check middleware will process requests with a URL that matches the
        /// provided value of path case-insensitively, allowing for an extra trailing slash
        ///('/') character.
        /// </remarks>
        /// <returns>The <see cref="IApplicationBuilder"/>.</returns>
        public static IApplicationBuilder UseHealthChecksPlus(this IApplicationBuilder app, PathString path, HealthCheckPlusOptions options)
        {
            ArgumentNullException.ThrowIfNull(app);

            ArgumentNullException.ThrowIfNull(options);

            var cacheStatus = (CacheHealthCheckPlus)app.ApplicationServices.GetRequiredService<IStateHealthChecksPlus>()!;
            cacheStatus.AddStatusName(options);

            object[] args = new IOptions<HealthCheckPlusOptions>[1] { Options.Create(options) };
            UseHealthChecksCore(app, path, null, args);
            return app;
        }

        /// <summary>
        /// Adds a middleware that provides health check status.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/>.</param>
        /// <param name="path">The The path on which to provide health check status.</param>
        /// <param name="port">The port to listen on. Must be a local port on which the server is listening.</param>
        /// <param name="options">The <see cref="HealthCheckPlusOptions"/> used to configure.</param>
        /// If path is set to null or the empty string then the health check middleware will
        /// <remarks>
        /// ignore the URL path and process all requests. If path is set to a non-empty value,
        /// the health check middleware will process requests with a URL that matches the
        /// provided value of path case-insensitively, allowing for an extra trailing slash
        ///('/') character.
        /// </remarks>
        /// <returns>The <see cref="IApplicationBuilder"/>.</returns>
        public static IApplicationBuilder UseHealthChecksPlus(this IApplicationBuilder app, PathString path, int port, HealthCheckPlusOptions options)
        {
            ArgumentNullException.ThrowIfNull(app);

            ArgumentNullException.ThrowIfNull(options);

            var cacheStatus = (CacheHealthCheckPlus)app.ApplicationServices.GetRequiredService<IStateHealthChecksPlus>()!;
            cacheStatus.AddStatusName(options);

            object[] args = new IOptions<HealthCheckPlusOptions>[1] { Options.Create(options) };
            UseHealthChecksCore(app, path, port, args);
            return app;
        }

        private static void UseHealthChecksCore(IApplicationBuilder app, PathString path, int? port, object[] args)
        {
            if (app.ApplicationServices.GetService(typeof(HealthCheckService)) == null)
            {
                throw new InvalidOperationException(string.Format("Unable Find {0})",
                    nameof(HealthCheckServiceCollectionExtensions.AddHealthChecks)));
            }

            // NOTE: we explicitly don't use Map here because it's really common for multiple health
            // check middleware to overlap in paths. Ex: `/health`, `/health/detailed` - this is order
            // sensitive with Map, and it's really surprising to people.
            //
            // See:
            // https://github.com/aspnet/Diagnostics/issues/511
            // https://github.com/aspnet/Diagnostics/issues/512
            // https://github.com/aspnet/Diagnostics/issues/514

            bool predicate(HttpContext c)
            {
                return

                    // Process the port if we have one
                    (port == null || c.Connection.LocalPort == port) &&

                    // We allow you to listen on all URLs by providing the empty PathString.
                    (!path.HasValue ||

                        // If you do provide a PathString, want to handle all of the special cases that
                        // StartsWithSegments handles, but we also want it to have exact match semantics.
                        //
                        // Ex: /Foo/ == /Foo (true)
                        // Ex: /Foo/Bar == /Foo (false)
                        (c.Request.Path.StartsWithSegments(path, out var remaining) &&
                        string.IsNullOrEmpty(remaining)));
            }
            app.MapWhen(predicate, b => b.UseMiddleware<HealthCheckMiddlewarePlus>(args));
        }


    }
}
