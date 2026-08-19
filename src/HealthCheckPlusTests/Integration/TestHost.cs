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

        // A number of end-to-end tests here wait for the background loop to complete "at least N"
        // cycles before asserting - a fixed Task.Delay sized as "a generous multiple of the
        // nominal per-cycle time" turned out not to be generous enough under this project's own
        // full-solution, 3-TFM-parallel test run (confirmed by an independent audit round: real
        // system contention, not a library bug, occasionally ate enough of that margin to leave a
        // cycle or two short). Polling for the actual condition, up to a much larger ceiling, is
        // correct regardless of how slow the host machine happens to be at the moment - it only
        // takes as long as the condition actually needs, and only fails if it genuinely never
        // becomes true within the ceiling.
        public static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, string timeoutMessage, CancellationToken cancellationToken)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            try
            {
                while (!condition())
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(50), cts.Token);
                }
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(timeoutMessage);
            }
        }
    }
}
