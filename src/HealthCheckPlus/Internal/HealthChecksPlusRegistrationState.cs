// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using System.Collections.Concurrent;

namespace HealthCheckPlus.Internal
{
    // Per-IServiceCollection registration-time state for HealthChecksPlusExtension. Replaces the
    // previous process-wide static fields (doc/progresso-plano-acao.md, step P1.1) so that
    // multiple hosts built in the same process (WebApplicationFactory, .NET Aspire, parallel
    // tests) each get their own isolated state, instead of leaking the "AddHealthChecksPlus was
    // called" flag and adopted external check instances across unrelated containers.
    internal sealed class HealthChecksPlusRegistrationState
    {
        public bool AddedHealthChecksPlus { get; set; }

        public ConcurrentDictionary<string, WrapperBaseHealthCheckPlus> ExternalCheck { get; } = new();
    }
}
