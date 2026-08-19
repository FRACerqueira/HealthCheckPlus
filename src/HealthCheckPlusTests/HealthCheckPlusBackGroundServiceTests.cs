// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Abstractions;
using HealthCheckPlus.Internal;
using HealthCheckPlus.Internal.Policies;
using HealthCheckPlus.Internal.WrapperMicrosoft;
using HealthCheckPlus.options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace HealthCheckPlusTests
{
    // Direct, synchronous coverage of ObserveLoopCompletion - the piece StopAsync's
    // ContinueWith delegates to. ContinueWith with no options runs regardless of the
    // antecedent's outcome but never inspects or rethrows it on its own, so a fault in the
    // background loop task used to vanish with no log and no exception anywhere. These use a
    // synthetic Task (Task.FromException/CompletedTask) instead of forcing a real exception
    // through the loop, the same reasoning DefaultHealthCheckServicePlusTests uses for
    // ClassifyBatchTask - the loop's own try/catch already handles every exception it can
    // reasonably anticipate, so a real end-to-end repro would require deliberately breaking
    // that guarantee just to exercise this backstop.
    public class HealthCheckPlusBackGroundServiceTests
    {
        [Fact]
        public void ObserveLoopCompletion_ShouldLogCritical_WhenLoopTaskFaulted()
        {
            var loggerProvider = new CapturingLoggerProvider();
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));
            var logger = loggerFactory.CreateLogger<HealthCheckPlusBackGroundService>();
            var faultedTask = Task.FromException(new InvalidOperationException("simulated background loop failure"));

            HealthCheckPlusBackGroundService.ObserveLoopCompletion(faultedTask, logger);

            Assert.Contains(loggerProvider.Entries, e =>
                e.Level == LogLevel.Critical && e.EventId.Name == "HealthCheckPlusBackGroundLoopFaulted");
        }

        [Fact]
        public void ObserveLoopCompletion_ShouldNotLog_WhenLoopTaskCompletedSuccessfully()
        {
            var loggerProvider = new CapturingLoggerProvider();
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));
            var logger = loggerFactory.CreateLogger<HealthCheckPlusBackGroundService>();

            HealthCheckPlusBackGroundService.ObserveLoopCompletion(Task.CompletedTask, logger);

            Assert.Empty(loggerProvider.Entries);
        }

        private sealed class AlwaysHealthyCheck : IHealthCheck
        {
            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(HealthCheckResult.Healthy());
            }
        }

        // Records every Task actually handed to QueueTask, while still running each one (via the
        // thread pool) so anything relying on this scheduler to make progress isn't stalled by
        // the test itself - the point is to observe WHICH scheduler a continuation is routed
        // through, not to construct an artificial deadlock.
        private sealed class TrackingTaskScheduler : TaskScheduler
        {
            public List<Task> QueuedTasks { get; } = [];

            protected override IEnumerable<Task>? GetScheduledTasks() => null;

            protected override void QueueTask(Task task)
            {
                lock (QueuedTasks)
                {
                    QueuedTasks.Add(task);
                }
                ThreadPool.UnsafeQueueUserWorkItem(_ => TryExecuteTask(task), null);
            }

            protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;
        }

        // Regression test: StopAsync's ContinueWith used to capture TaskScheduler.Current - the
        // scheduler ambient at the exact point StopAsync happens to be called - instead of always
        // using the thread pool. A custom, limited-concurrency scheduler saturated with unrelated
        // work at shutdown time could then stall this continuation (and therefore the Task
        // StopAsync returns) for a reason completely unrelated to this class's own logic.
        // Invoking StopAsync from inside a task queued to a tracking scheduler makes
        // TaskScheduler.Current equal to it at the exact point StopAsync builds its ContinueWith -
        // reproducing "whatever happens to be ambient" at that call site. Since ContinueWith only
        // ever hands its continuation to QueueTask (TryExecuteTaskInline above always declines),
        // the tracking scheduler must show exactly one queued task - the outer call itself - once
        // StopAsync's own returned task has completed; a second entry would mean the continuation
        // was routed through the ambient scheduler instead of the thread pool.
        [Fact]
        public async Task StopAsync_ShouldNotRouteItsContinuation_ThroughTheAmbientTaskScheduler()
        {
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test1"]);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test1", _ => new AlwaysHealthyCheck(), null, null));

            var services = new ServiceCollection();
            services.AddSingleton<IStateHealthChecksPlus>(cache);
            services.AddSingleton(new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1000), "Test1"));
            var provider = services.BuildServiceProvider();

            var healthCheckService = new DefaultHealthCheckServicePlus(
                provider.GetRequiredService<IServiceScopeFactory>(),
                provider,
                NullLogger<HealthCheckService>.Instance,
                Options.Create(hcOptions),
                new HealthChecksPlusRegistrationState());

            var backgroundOptions = new HealthCheckPlusBackGroundOptions
            {
                Delay = TimeSpan.Zero,
                Idle = TimeSpan.FromSeconds(1)
            };

            var backgroundService = new HealthCheckPlusBackGroundService(
                NullLogger<HealthCheckPlusBackGroundService>.Instance,
                healthCheckService,
                Options.Create(hcOptions),
                Options.Create(backgroundOptions),
                []);

            await backgroundService.StartAsync(TestContext.Current.CancellationToken);

            var trackingScheduler = new TrackingTaskScheduler();
            Task stopTask = null!;

            Action invokeStopAsync = () =>
            {
                stopTask = backgroundService.StopAsync(CancellationToken.None);
            };
            await Task.Factory.StartNew(invokeStopAsync, CancellationToken.None, TaskCreationOptions.None, trackingScheduler);

            await stopTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

            _ = Assert.Single(trackingScheduler.QueuedTasks);
        }

        // Regression test for a scenario a later independent audit round found: if a DIFFERENT
        // IHostedService registered after this one throws from its own StartAsync, the generic
        // host disposes the container without ever calling StopAsync on this one first -
        // confirmed empirically against a real Microsoft.Extensions.Hosting host that a later
        // service's StartAsync throwing does NOT trigger StopAsync on services that already
        // started. Before DisposeAsync existed, disposal only ever called the synchronous
        // Dispose(), which disposed _stopping immediately with the loop still running -
        // surfacing as an ObjectDisposedException inside the loop, logged as an ordinary
        // per-cycle failure (HealthCheckPlusBackGroundError) instead of a clean shutdown, since
        // nothing ever cancelled _stopping first. DisposeAsync must cancel it and actually wait
        // for the loop before disposing it - simulated here by calling DisposeAsync directly
        // without ever calling StopAsync, the same shape the container's own async disposal path
        // produces in that scenario.
        [Fact]
        public async Task DisposeAsync_ShouldStopTheLoopCleanly_WithoutObjectDisposedException_WhenStopAsyncNeverRan()
        {
            var loggerProvider = new CapturingLoggerProvider();
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));

            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test1"]);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test1", _ => new AlwaysHealthyCheck(), null, null));

            var services = new ServiceCollection();
            services.AddSingleton<IStateHealthChecksPlus>(cache);
            services.AddSingleton(new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1000), "Test1"));
            var provider = services.BuildServiceProvider();

            var healthCheckService = new DefaultHealthCheckServicePlus(
                provider.GetRequiredService<IServiceScopeFactory>(),
                provider,
                NullLogger<HealthCheckService>.Instance,
                Options.Create(hcOptions),
                new HealthChecksPlusRegistrationState());

            var backgroundOptions = new HealthCheckPlusBackGroundOptions
            {
                Delay = TimeSpan.Zero,
                Idle = TimeSpan.FromSeconds(1)
            };

            var backgroundService = new HealthCheckPlusBackGroundService(
                loggerFactory.CreateLogger<HealthCheckPlusBackGroundService>(),
                healthCheckService,
                Options.Create(hcOptions),
                Options.Create(backgroundOptions),
                []);

            await backgroundService.StartAsync(TestContext.Current.CancellationToken);
            // Give the loop a moment to actually be mid-cycle (inside its first Idle wait)
            // before disposing, instead of always happening to catch it during the startup
            // delay - a disposed-but-not-cancelled CancellationTokenSource does not disrupt a
            // Task.Delay already registered against its token (confirmed empirically), so the
            // failure this test targets only ever surfaces once the loop reaches a *fresh*
            // CreateLinkedTokenSource/Task.Delay call using it, on the following cycle.
            await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

            // The loop Task is private - reflection is the only way to observe directly whether
            // disposal actually let it finish, rather than inferring it indirectly from logs
            // (which, for the exact bug this guards against, are emitted asynchronously well
            // after DisposeAsync itself returns, and are easy to miss checking too early).
            var loopField = typeof(HealthCheckPlusBackGroundService).GetField("_runningHealthCheckPlus", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var loopTask = (Task)loopField.GetValue(backgroundService)!;

            // StopAsync deliberately never called - simulating the container disposing this
            // service without ever having a chance to call it first.
            var disposeTask = backgroundService.DisposeAsync().AsTask();
            var disposeCompleted = await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            Assert.Same(disposeTask, disposeCompleted);
            await disposeTask;

            // DisposeAsync must not return until the loop itself has actually finished - if it
            // returns first (the bug: dispose _stopping immediately, never wait for the loop),
            // the loop is left to keep running against an already-disposed CancellationTokenSource,
            // which resolves later as an unobserved fault rather than a clean, immediate exit.
            Assert.True(loopTask.IsCompleted, "The loop task was still running after DisposeAsync completed.");
            Assert.False(loopTask.IsFaulted, $"The loop task faulted: {(loopTask.IsFaulted ? loopTask.Exception : null)}");

            Assert.DoesNotContain(loggerProvider.Entries, e => e.Exception is ObjectDisposedException);
            Assert.DoesNotContain(loggerProvider.Entries, e => e.EventId.Name == "HealthCheckPlusBackGroundLoopFaulted");
        }
    }
}
