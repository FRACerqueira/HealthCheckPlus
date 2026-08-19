// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using System.Runtime.CompilerServices;

namespace HealthCheckPlusTests
{
    internal static class TestAssemblySetup
    {
        // Several tests in this suite (both here and in integration tests) block synchronously
        // waiting for a background Task.Run work item to start - the same shape as a real
        // consumer's own health check execution. xunit itself dispatches async test methods
        // through the thread pool, so under this project's own full-solution, 3-TFM-parallel test
        // run, that self-references: many already-running test methods (themselves pool threads)
        // block waiting for a further pool thread to run the work they're waiting on, while the
        // pool's default slow-growth throttle (roughly one new thread every ~500ms once
        // starvation is detected) can't keep up. Raising the minimum worker count up front avoids
        // that throttle for the tests that actually need a thread promptly, instead of each one
        // independently working around it with `Thread` instead of `Task.Run`.
        [ModuleInitializer]
        internal static void RaiseMinThreadPoolWorkersForTestParallelism()
        {
            var minWorkers = Math.Max(64, Environment.ProcessorCount * 8);
            ThreadPool.GetMinThreads(out _, out var minCompletionPortThreads);
            ThreadPool.SetMinThreads(minWorkers, minCompletionPortThreads);
        }
    }
}
