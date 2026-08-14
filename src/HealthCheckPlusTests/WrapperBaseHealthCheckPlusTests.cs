// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Internal;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlusTests
{
    // Regression test for the action plan (doc/plano-acao-healthcheckplus.md), step P0.5.
    // High finding from the audit (doc/healthcheckplus-audit.html): "Disposable external checks
    // stop working after the first execution". AddCheckLinkTo caches this wrapper (and the
    // wrapped instance) across every polling cycle, so disposing the wrapped instance inside
    // CheckHealthAsync on every call left every call after the first running against an already
    // disposed object.
    public class WrapperBaseHealthCheckPlusTests
    {
        private sealed class DisposableTrackingCheck : IHealthCheck, IDisposable
        {
            public int CallCount { get; private set; }
            public bool Disposed { get; private set; }

            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                if (Disposed)
                {
                    throw new ObjectDisposedException(nameof(DisposableTrackingCheck));
                }
                CallCount++;
                return Task.FromResult(HealthCheckResult.Healthy());
            }

            public void Dispose()
            {
                Disposed = true;
            }
        }

        [Fact]
        public async Task CheckHealthAsync_ShouldNotDisposeWrappedInstance_AcrossMultipleCalls()
        {
            var inner = new DisposableTrackingCheck();
            var wrapper = new WrapperBaseHealthCheckPlus(inner);
            var context = new HealthCheckContext();

            await wrapper.CheckHealthAsync(context, CancellationToken.None);
            await wrapper.CheckHealthAsync(context, CancellationToken.None);

            Assert.Equal(2, inner.CallCount);
            Assert.False(inner.Disposed);
        }
    }
}
