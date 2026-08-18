// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace HealthCheckPlusTests
{
    // Integration tests covering the AddCheckLinkTo mechanism, which adopts an existing
    // registration via the public Options pipeline instead of reaching into internal ASP.NET Core
    // types.
    public class AddCheckLinkToTests
    {
        private sealed class AlwaysHealthyCheck : IHealthCheck
        {
            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(HealthCheckResult.Healthy());
            }
        }

        [Fact]
        public void AddCheckLinkTo_ShouldReplaceOriginalRegistration_WithTheWrappedName()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var ihb = services.AddHealthChecksPlus();
            ihb.Add(new HealthCheckRegistration("Original", _ => new AlwaysHealthyCheck(), null, null));
            ihb.AddCheckLinkTo("Adopted", "Original");

            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

            Assert.Contains(options.Registrations, r => r.Name == "Adopted");
            Assert.DoesNotContain(options.Registrations, r => r.Name == "Original");
        }

        [Fact]
        public void AddCheckLinkTo_ShouldThrowClearException_WhenNamedCheckWasNeverRegistered()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var ihb = services.AddHealthChecksPlus();
            ihb.AddCheckLinkTo("Adopted", "DoesNotExist");

            using var provider = services.BuildServiceProvider();

            var ex = Assert.Throws<InvalidOperationException>(
                () => provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value);
            Assert.Contains("DoesNotExist", ex.Message, StringComparison.Ordinal);
        }

        private sealed class CountingFactoryCheck : IHealthCheck
        {
            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(HealthCheckResult.Healthy());
            }
        }

        // Regression test: the adopted check's factory caches the constructed
        // WrapperBaseHealthCheckPlus via ConcurrentDictionary.GetOrAdd(key, valueFactory) — but
        // GetOrAdd's valueFactory has no once-only guarantee under contention. Two overlapping
        // calls to registration.Factory (e.g. an HTTP request and a
        // background cycle both finding the check "due" for its first run at the same time) could
        // each construct a real underlying check instance, with the loser silently discarded and
        // never disposed. This test drives many concurrent factory calls at once, before the check
        // has been cached, to reproduce that race.
        [Fact]
        public async Task AddCheckLinkTo_ShouldConstructTheAdoptedCheckOnlyOnce_UnderConcurrentFactoryInvocations()
        {
            var constructionCount = 0;

            var services = new ServiceCollection();
            services.AddLogging();
            var ihb = services.AddHealthChecksPlus();
            ihb.Add(new HealthCheckRegistration("Original", _ =>
            {
                Interlocked.Increment(ref constructionCount);
                return new CountingFactoryCheck();
            }, null, null));
            ihb.AddCheckLinkTo("Adopted", "Original");

            using var provider = services.BuildServiceProvider();
            var registration = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
                .Single(r => r.Name == "Adopted");

            const int concurrency = 20;
            using var barrier = new Barrier(concurrency);
            var tasks = Enumerable.Range(0, concurrency)
                .Select(_ => Task.Run(() =>
                {
                    barrier.SignalAndWait(TestContext.Current.CancellationToken);
                    return registration.Factory(provider);
                }))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            Assert.Equal(1, constructionCount);
            Assert.All(results, r => Assert.Same(results[0], r));
        }

        private sealed class ScopedDependency : IDisposable
        {
            public bool Disposed { get; private set; }
            public void Dispose() => Disposed = true;
        }

        private sealed class ScopedDependencyCheck(ScopedDependency dependency) : IHealthCheck
        {
            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(dependency.Disposed
                    ? HealthCheckResult.Unhealthy("The scoped dependency was already disposed.")
                    : HealthCheckResult.Healthy());
            }
        }

        // Regression test: the adopted check's factory used to be built with whichever
        // IServiceProvider the very first caller happened to pass in - in production, that's the
        // per-execution scope DefaultHealthCheckServicePlus.RunCheckAsync creates and disposes
        // around each single call. Since the constructed check is then cached and reused forever
        // (by design - see WrapperBaseHealthCheckPlus), any check whose construction resolves a
        // scoped dependency (the canonical case being the native AddDbContextCheck<T>) worked once
        // and then ran against an already-disposed dependency on every later execution. This
        // simulates that real lifecycle - a fresh scope created and disposed around each call to
        // registration.Factory, exactly like RunCheckAsync does - instead of calling the factory
        // with a scope that outlives the test.
        [Fact]
        public async Task AddCheckLinkTo_ShouldKeepAdoptedCheckWorking_AfterItsFirstExecutionScopeIsDisposed()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddScoped<ScopedDependency>();
            var ihb = services.AddHealthChecksPlus();
            ihb.Add(new HealthCheckRegistration("Original", sp => new ScopedDependencyCheck(sp.GetRequiredService<ScopedDependency>()), null, null));
            ihb.AddCheckLinkTo("Adopted", "Original");

            using var provider = services.BuildServiceProvider();
            var registration = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
                .Single(r => r.Name == "Adopted");

            IHealthCheck adoptedCheck;
            using (var firstExecutionScope = provider.CreateScope())
            {
                adoptedCheck = (IHealthCheck)registration.Factory(firstExecutionScope.ServiceProvider);
            }
            // firstExecutionScope is now disposed - if the adopted check's dependency came from it,
            // it's disposed too.

            using var secondExecutionScope = provider.CreateScope();
            var sameCheck = (IHealthCheck)registration.Factory(secondExecutionScope.ServiceProvider);
            Assert.Same(adoptedCheck, sameCheck);

            var result = await sameCheck.CheckHealthAsync(new HealthCheckContext { Registration = registration }, TestContext.Current.CancellationToken);

            Assert.Equal(HealthStatus.Healthy, result.Status);
        }

        private sealed class ScopedDisposalMarker : IDisposable
        {
            public bool Disposed { get; private set; }
            public void Dispose() => Disposed = true;
        }

        // Regression test: the adoption factory creates its own scope (ownedScope) before invoking
        // the original registration's factory inside it. If that original factory throws, the
        // WrapperBaseHealthCheckPlus that would take ownership of ownedScope is never constructed,
        // so nothing disposes it - leaking whatever it resolved (e.g. a scoped DbContext) for the
        // rest of the process's life, once per failed construction attempt.
        [Fact]
        public void AddCheckLinkTo_ShouldNotLeakTheOwnedScope_WhenConstructingTheAdoptedCheckThrows()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddScoped<ScopedDisposalMarker>();

            ScopedDisposalMarker? capturedMarker = null;

            var ihb = services.AddHealthChecksPlus();
            ihb.Add(new HealthCheckRegistration("Original", sp =>
            {
                capturedMarker = sp.GetRequiredService<ScopedDisposalMarker>();
                throw new InvalidOperationException("simulated construction failure");
            }, null, null));
            ihb.AddCheckLinkTo("Adopted", "Original");

            using var provider = services.BuildServiceProvider();
            var registration = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
                .Single(r => r.Name == "Adopted");

            using var callerScope = provider.CreateScope();
            Assert.Throws<InvalidOperationException>(() => registration.Factory(callerScope.ServiceProvider));

            Assert.NotNull(capturedMarker);
            Assert.True(capturedMarker!.Disposed,
                "The scope owned by the adoption factory was not disposed after constructing the adopted check failed, leaking it.");
        }
    }
}
