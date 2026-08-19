using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlusDemoMetrics
{
    // Cycles deterministically through Healthy -> Degraded -> Unhealthy on every run, so this
    // sample's healthcheckplus.check.status_transitions metric has something to show without
    // needing a real flaky dependency.
    public class FlakyCheck : IHealthCheck
    {
        // Static, not instance: HealthCheckPlus constructs a fresh IHealthCheck instance from its
        // factory on every run (see AddCheckPlus<T>/RunCheckAsync) - an instance field here would
        // reset to its default on every single call and never actually cycle.
        private static readonly HealthStatus[] Cycle = [HealthStatus.Healthy, HealthStatus.Degraded, HealthStatus.Unhealthy];
        private static int _count = -1;

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var run = Interlocked.Increment(ref _count);
            var status = Cycle[run % Cycle.Length];
            return Task.FromResult(new HealthCheckResult(status, $"Cycled to {status} (run #{run})"));
        }
    }
}
