// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Internal;
using Microsoft.Extensions.Logging;

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
    }
}
