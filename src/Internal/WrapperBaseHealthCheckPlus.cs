// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HealthCheckPlus.Internal
{
    internal class WrapperBaseHealthCheckPlus : BaseHealthCheckPlus, IDisposable
    {
        private readonly IHealthCheck _externalCheckinstance;
        private bool disposed = false;

        public WrapperBaseHealthCheckPlus(IHealthCheck healthCheckisnt, IServiceProvider serviceProvider) : base(serviceProvider)
        {
            _externalCheckinstance = healthCheckisnt;
        }

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
                }
                // Note disposing has been done.
                disposed = true;
            }
        }

        public override async Task<HealthCheckResult> DoHealthCheck(HealthCheckContext context, CancellationToken cancellationToken)
        {
            var aux = await _externalCheckinstance.CheckHealthAsync(context, cancellationToken);
            if (_externalCheckinstance is IDisposable disposable)
            {
                disposable.Dispose();
            }
            return aux;
        }
    }

}
