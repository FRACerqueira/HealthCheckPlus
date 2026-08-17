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

        // Implement IDisposable.
        // Do not make this method virtual.
        // A derived class should not be able to override this method.
        public void Dispose()
        {
            Dispose(disposing: true);
            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SuppressFinalize to
            // take this object off the finalization queue
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        // Dispose(bool disposing) executes in two distinct scenarios.
        // If disposing equals true, the method has been called directly
        // or indirectly by a user's code. Managed and unmanaged resources
        // can be disposed.
        // If disposing equals false, the method has been called by the
        // runtime from inside the finalizer and you should not reference
        // other objects. Only unmanaged resources can be disposed.
        protected virtual void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!disposed)
            {
                // If disposing equals true, dispose all managed
                // and unmanaged resources.
                if (disposing)
                {
                    //Dispose managed resources.
                    if (_externalCheckinstance is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    _ownedScope?.Dispose();
                }
                // Note disposing has been done.
                disposed = true;
            }
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
