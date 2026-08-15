// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HealthCheckPlusTests.Integration
{
    // Shared real-host test infrastructure: end-to-end coverage of the DI wiring, middleware, and
    // background service through an actual ASP.NET Core pipeline (Microsoft.AspNetCore.TestHost),
    // rather than constructing internal types directly.
    internal static class TestHost
    {
        public static async Task<IHost> CreateAsync(Action<IServiceCollection> configureServices, Action<IApplicationBuilder> configureApp)
        {
            var hostBuilder = new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder
                        .UseTestServer()
                        .ConfigureServices(configureServices)
                        .Configure(configureApp);
                });

            return await hostBuilder.StartAsync();
        }
    }
}
