// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlus.Internal
{
    // ownedScope: the DI scope the adopted check's dependency (if any) was resolved from. Since
    // this wrapper - and the wrapped check instance - is cached and reused for the process's
    // entire lifetime (see AddCheckLinkTo), that scope must live exactly as long as this wrapper
    // does, not just for the single execution that first constructed the check. Passing it in here
    // ties its disposal to this wrapper's own, which DefaultHealthCheckServicePlus.Dispose()
    // already drives at host shutdown.
    internal class WrapperBaseHealthCheckPlus(IHealthCheck healthCheckisnt, IServiceScope? ownedScope = null) : IHealthCheck, IDisposable
    {
        private readonly IHealthCheck _externalCheckinstance = healthCheckisnt;
        private readonly IServiceScope? _ownedScope = ownedScope;
        private bool disposed = false;

        // No finalizer and no subclasses exist for this internal, purely-managed-resources
        // wrapper, so the full Dispose(bool disposing)/GC.SuppressFinalize pattern doesn't apply
        // here - a single guarded Dispose() is enough.
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;

            if (_externalCheckinstance is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _ownedScope?.Dispose();
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            // Do not dispose the wrapped instance here. AddCheckLinkTo caches this wrapper (and
            // therefore this same wrapped instance) across every polling cycle, so disposing it
            // after the first execution would leave every later call running against an already
            // disposed object. Disposal is this class's own IDisposable responsibility (see
            // Dispose(bool) above), driven by whoever holds the cached wrapper
            // (DefaultHealthCheckServicePlus.Dispose()).
            return await _externalCheckinstance.CheckHealthAsync(context, cancellationToken);
        }
    }

}
