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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HealthCheckPlusTests
{
    // Regression tests covering DefaultHealthCheckServicePlus's policy resolution and scheduling.
    public class DefaultHealthCheckServicePlusTests
    {
        private sealed class AlwaysHealthyCheck : IHealthCheck
        {
            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(HealthCheckResult.Healthy());
            }
        }

        private sealed class SlowCountingCheck : IHealthCheck
        {
            public int CallCount;

            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref CallCount);
                Thread.Sleep(50); // widen the race window so overlapping callers actually overlap
                return Task.FromResult(HealthCheckResult.Healthy());
            }
        }

        // Blocks on the ambient cancellationToken passed in by RunCheckAsync (not a per-check
        // timeout token) until externally cancelled, so a test can control exactly when the
        // ambient token fires mid-execution.
        private sealed class CancelableCheck : IHealthCheck
        {
            public readonly ManualResetEventSlim Started = new(false);

            public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                Started.Set();
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return HealthCheckResult.Healthy();
            }
        }

        // Simulates a check whose own dependency (e.g. an HttpClient with its own internal timeout,
        // or a driver with its own internal deadline) throws an OperationCanceledException that has
        // nothing to do with the checkCancellationToken RunCheckAsync handed it - the exception
        // carries a default/unrelated token, not the one passed to CheckHealthAsync. Deliberately
        // does not observe cancellationToken at all (like a dependency that ignores it), so a test
        // can control precisely when the throw happens relative to the ambient token being cancelled.
        private sealed class ThrowsUnrelatedOperationCanceledExceptionCheck : IHealthCheck
        {
            public readonly ManualResetEventSlim Started = new(false);
            private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public void Release() => _release.TrySetResult();

            public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                Started.Set();
                await _release.Task.ConfigureAwait(false);
                throw new OperationCanceledException("simulated internal timeout unrelated to the ambient token");
            }
        }

        private static DefaultHealthCheckServicePlus BuildService(
            CacheHealthCheckPlus cache,
            HealthCheckServiceOptions hcOptions,
            params IHealthCheckPlusPolicyStatus[] policies)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IStateHealthChecksPlus>(cache);
            foreach (var policy in policies)
            {
                services.AddSingleton(policy);
            }
            var provider = services.BuildServiceProvider();

            return new DefaultHealthCheckServicePlus(
                provider.GetRequiredService<IServiceScopeFactory>(),
                provider,
                NullLogger<HealthCheckService>.Instance,
                Options.Create(hcOptions),
                new HealthChecksPlusRegistrationState());
        }

        // The Degraded status must resolve to its own policy on the HTTP request path, not the
        // Healthy policy. Setup: check "Test1" has been Degraded for 10s. The Healthy policy has a
        // 1000s period (must NOT trigger a rerun on its own). The Degraded policy has a 3s period
        // (must trigger). No Unhealthy policy is registered, ruling out a Degraded-falls-through-to-
        // Unhealthy-lookup regression too.
        [Fact]
        public async Task CheckHealthPlusAsync_ShouldUseDegradedPolicy_WhenLastStatusIsDegraded()
        {
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test1"]);
            cache.Running("Test1", true);
            cache.Update("Test1", HealthCheckTrigger.Background, new HealthCheckResult(HealthStatus.Degraded), DateTime.UtcNow.AddSeconds(-10), TimeSpan.Zero);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test1", _ => new AlwaysHealthyCheck(), null, null));

            var healthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1000), "Test1");
            var degradedPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Degraded, TimeSpan.Zero, TimeSpan.FromSeconds(3), "Test1");

            var service = BuildService(cache, hcOptions, healthyPolicy, degradedPolicy);

            await service.CheckHealthPlusAsync(null, null, HealthCheckTrigger.UrlRequest, CancellationToken.None);

            var status = cache.FullStatus("Test1");
            Assert.Equal(HealthCheckTrigger.UrlRequest, status.Origin);
            Assert.Equal(HealthStatus.Healthy, status.LastResult.Status);
        }

        // A health check registered without a matching Healthy policy must fail fast and clearly at
        // service construction time, instead of throwing a NullReferenceException later, at
        // runtime, when health is evaluated. Setup: "NativeCheck" is registered in
        // HealthCheckServiceOptions (as would happen via the native IHealthChecksBuilder) but no
        // Healthy policy was registered for it (as would happen if a developer forgot
        // AddCheckPlus/AddCheckLinkTo).
        [Fact]
        public void Constructor_ShouldThrowClearException_WhenRegistrationHasNoHealthyPolicy()
        {
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["NativeCheck"]);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("NativeCheck", _ => new AlwaysHealthyCheck(), null, null));

            var ex = Assert.Throws<InvalidOperationException>(() => BuildService(cache, hcOptions));
            Assert.Contains("NativeCheck", ex.Message, StringComparison.Ordinal);
        }

        // A health check registered with a Healthy policy (as AddCheckPlus/AddCheckLinkTo always
        // do) but whose name was left out of the `names` list passed to AddHealthChecksPlus has no
        // entry in the cache - ValidateHealthyPolicies alone doesn't catch this, since it only
        // checks the opposite direction (a name without a matching policy). Left unchecked, this
        // reaches CacheHealthCheckPlus.FullStatus's raw dictionary indexer on every later
        // request/cycle and crashes with an unhandled KeyNotFoundException - exactly the kind of
        // failure the constructor-time fail-fast for the original NRE finding was meant to
        // eliminate, just reachable from the other direction. Setup: "Kafka" has a Healthy policy
        // but only "OtherCheck" was passed to AddHealthChecksPlus's names list (simulated here via
        // cache.InitCache).
        [Fact]
        public void Constructor_ShouldThrowClearException_WhenRegistrationNameIsMissingFromCache()
        {
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["OtherCheck"]);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Kafka", _ => new AlwaysHealthyCheck(), null, null));

            var healthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(30), "Kafka");

            var ex = Assert.Throws<InvalidOperationException>(() => BuildService(cache, hcOptions, healthyPolicy));
            Assert.Contains("Kafka", ex.Message, StringComparison.Ordinal);
        }

        // The opposite direction of the same misconfiguration: a name passed to
        // AddHealthChecksPlus's `names` list with no matching health check registration - it would
        // otherwise sit seeded Healthy in the cache forever, with nothing ever updating it, and
        // (if a background Predicate is explicitly set to null) could even reach publishers as a
        // permanently-Healthy phantom check.
        [Fact]
        public void Constructor_ShouldThrowClearException_WhenCacheNameHasNoRegistration()
        {
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test1", "Phantom"]);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test1", _ => new AlwaysHealthyCheck(), null, null));

            var healthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(30), "Test1");

            var ex = Assert.Throws<InvalidOperationException>(() => BuildService(cache, hcOptions, healthyPolicy));
            Assert.Contains("Phantom", ex.Message, StringComparison.Ordinal);
        }

        // Regression test: the cache's per-check Tags (used by CacheHealthCheckPlus.CreateReport,
        // which a callback registered via AddStatusName consumes through IStateHealthChecksPlus.
        // Status) must be populated from the real registrations at construction time - InitCache
        // alone has no access to them (only the plain `names` list passed to AddHealthChecksPlus).
        [Fact]
        public void Constructor_ShouldPopulateCacheTags_FromRegistrations()
        {
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test1"]);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test1", _ => new AlwaysHealthyCheck(), null, ["tag-a", "tag-b"]));

            var healthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(30), "Test1");

            BuildService(cache, hcOptions, healthyPolicy);

            Assert.Equal(["tag-a", "tag-b"], cache.CreateReport().Entries["Test1"].Tags);
        }

        // Regression test: two policies registered for the same check and the same status (e.g.
        // AddUnhealthyPolicy called twice for "Test1") used to collide silently - FindPolicy's
        // FirstOrDefault always picks whichever was registered first, so the second call's period
        // was ignored with no warning at all. This must fail fast at construction, the same way a
        // missing Healthy policy does.
        [Fact]
        public void Constructor_ShouldThrowClearException_WhenTheSameCheckHasTwoPoliciesForTheSameStatus()
        {
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test1"]);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test1", _ => new AlwaysHealthyCheck(), null, null));

            var healthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(30), "Test1");
            var firstUnhealthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Unhealthy, TimeSpan.Zero, TimeSpan.FromSeconds(10), "Test1");
            var secondUnhealthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Unhealthy, TimeSpan.Zero, TimeSpan.FromSeconds(20), "Test1");

            var ex = Assert.Throws<InvalidOperationException>(() => BuildService(cache, hcOptions, healthyPolicy, firstUnhealthyPolicy, secondUnhealthyPolicy));
            Assert.Contains("Test1", ex.Message, StringComparison.Ordinal);
        }

        // Characterization test confirming the Unhealthy branch of the foreground/HTTP path behaves
        // correctly through ResolveForegroundPolicy.
        [Fact]
        public async Task CheckHealthPlusAsync_ShouldUseUnhealthyPolicy_WhenLastStatusIsUnhealthy()
        {
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test1"]);
            cache.Running("Test1", true);
            cache.Update("Test1", HealthCheckTrigger.Background, new HealthCheckResult(HealthStatus.Unhealthy), DateTime.UtcNow.AddSeconds(-10), TimeSpan.Zero);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test1", _ => new AlwaysHealthyCheck(), null, null));

            var healthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1000), "Test1");
            var unhealthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Unhealthy, TimeSpan.Zero, TimeSpan.FromSeconds(3), "Test1");

            var service = BuildService(cache, hcOptions, healthyPolicy, unhealthyPolicy);

            await service.CheckHealthPlusAsync(null, null, HealthCheckTrigger.UrlRequest, CancellationToken.None);

            var status = cache.FullStatus("Test1");
            Assert.Equal(HealthCheckTrigger.UrlRequest, status.Origin);
            Assert.Equal(HealthStatus.Healthy, status.LastResult.Status);
        }

        // Characterization test: unlike the foreground path, the background path
        // (ResolveBackgroundPolicy) falls back to the background service's own per-status defaults
        // (HealthCheckPlusBackGroundOptions), not to the check's Healthy policy, when no explicit
        // policy is registered for the current status. This is an intentional difference between
        // the two paths, not a bug — see ResolveForegroundPolicy/ResolveBackgroundPolicy.
        [Fact]
        public async Task BackGroudCheckHealthPlusAsync_ShouldFallBackToBackgroundOptionsDefault_WhenNoExplicitPolicy()
        {
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test2"]);
            cache.Running("Test2", true);
            cache.Update("Test2", HealthCheckTrigger.Background, new HealthCheckResult(HealthStatus.Degraded), DateTime.UtcNow.AddSeconds(-10), TimeSpan.Zero);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test2", _ => new AlwaysHealthyCheck(), null, null));

            // Only a Healthy policy is registered — no Degraded policy — so the fallback must come
            // from backgroundOptions.DegradedPeriod, not from the (much longer) Healthy period.
            var healthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1000), "Test2");

            var service = BuildService(cache, hcOptions, healthyPolicy);

            var backgroundOptions = new HealthCheckPlusBackGroundOptions
            {
                DegradedPeriod = TimeSpan.FromSeconds(3)
            };

            await service.BackGroudCheckHealthPlusAsync(backgroundOptions, CancellationToken.None);

            var status = cache.FullStatus("Test2");
            Assert.Equal(HealthStatus.Healthy, status.LastResult.Status);
        }

        // Regression test for a scheduling race: reading "due" and marking Running as two separate
        // steps would let concurrent callers deciding the same check is due at the same time (e.g.
        // an HTTP request racing a background cycle) both schedule and run it. This drives many
        // concurrent CheckHealthPlusAsync calls at the exact same instant (via Barrier) while the
        // check is due, and expects the underlying IHealthCheck to run once.
        [Fact]
        public async Task CheckHealthPlusAsync_ShouldRunTheCheckOnlyOnce_WhenCalledConcurrentlyWhileDue()
        {
            var check = new SlowCountingCheck();
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test1"]);
            cache.Running("Test1", true);
            cache.Update("Test1", HealthCheckTrigger.Background, new HealthCheckResult(HealthStatus.Healthy), DateTime.UtcNow.AddSeconds(-10), TimeSpan.Zero);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test1", _ => check, null, null));

            var healthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1), "Test1");

            var service = BuildService(cache, hcOptions, healthyPolicy);

            const int concurrency = 20;
            using var barrier = new Barrier(concurrency);
            var tasks = Enumerable.Range(0, concurrency).Select(_ => Task.Run(async () =>
            {
                barrier.SignalAndWait();
                await service.CheckHealthPlusAsync(null, null, HealthCheckTrigger.UrlRequest, CancellationToken.None);
            })).ToArray();

            await Task.WhenAll(tasks);

            Assert.Equal(1, check.CallCount);
        }

        // Regression test: TryBeginRun marks a check Running before its task runs, and only
        // Update() ever clears that flag. If the ambient cancellationToken (not a per-check
        // timeout) fires while the check is in flight - e.g. an HTTP client disconnecting, which
        // surfaces here as httpContext.RequestAborted - RunCheckAsync deliberately lets that
        // OperationCanceledException propagate uncaught, which used to make Task.WhenAll skip the
        // loop that calls Update entirely, wedging the check as Running forever. It must still be
        // released even though the exception itself is still expected to propagate to the caller.
        [Fact]
        public async Task CheckHealthPlusAsync_ShouldReleaseRunning_WhenAmbientTokenIsCancelledMidFlight()
        {
            var check = new CancelableCheck();
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test1"]);
            cache.Running("Test1", true);
            cache.Update("Test1", HealthCheckTrigger.Background, new HealthCheckResult(HealthStatus.Healthy), DateTime.UtcNow.AddSeconds(-10), TimeSpan.Zero);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test1", _ => check, null, null));

            var healthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1), "Test1");

            var service = BuildService(cache, hcOptions, healthyPolicy);

            using var cts = new CancellationTokenSource();
            var callTask = service.CheckHealthPlusAsync(null, null, HealthCheckTrigger.UrlRequest, cts.Token);

            // Generous timeout: the 3 target frameworks' test processes run concurrently in CI/local
            // full-suite runs, and under that contention the background Task.Run here can take a
            // few seconds to get scheduled even though nothing is actually stuck.
            Assert.True(check.Started.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken), "The check never started.");
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => callTask);

            var status = cache.FullStatus("Test1");
            Assert.False(status.Running, "The check was left permanently marked Running after the ambient token was cancelled.");

            // Releasing Running must not manufacture a synthetic result for an attempt that never
            // actually completed - the check's last known real result (seeded Healthy above) must
            // still be what every other reader (other requests, publishers, Status()) sees, not an
            // Unhealthy status invented for a client that merely disconnected.
            Assert.Equal(HealthStatus.Healthy, status.LastResult.Status);
        }

        // Same regression as above, for the background path's identical finally block.
        [Fact]
        public async Task BackGroudCheckHealthPlusAsync_ShouldReleaseRunning_WithoutOverwritingLastResult_WhenLinkedTokenTimesOut()
        {
            var check = new CancelableCheck();
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test1"]);
            cache.Running("Test1", true);
            cache.Update("Test1", HealthCheckTrigger.Background, new HealthCheckResult(HealthStatus.Healthy), DateTime.UtcNow.AddSeconds(-10), TimeSpan.Zero);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test1", _ => check, null, null));

            var healthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1), "Test1");

            var service = BuildService(cache, hcOptions, healthyPolicy);

            var backgroundOptions = new HealthCheckPlusBackGroundOptions();

            using var cts = new CancellationTokenSource();
            var callTask = service.BackGroudCheckHealthPlusAsync(backgroundOptions, cts.Token);

            Assert.True(check.Started.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken), "The check never started.");
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => callTask);

            var status = cache.FullStatus("Test1");
            Assert.False(status.Running, "The check was left permanently marked Running after the linked token was cancelled.");
            Assert.Equal(HealthStatus.Healthy, status.LastResult.Status);
        }

        // Regression test: releasing Running on any task that didn't complete successfully (not
        // just ambient cancellation) used to also cover a genuine, non-cancellation failure - e.g.
        // the registration's Factory itself throwing while resolving the check instance (this
        // happens outside RunCheckAsync's own try/catch, so it faults the task rather than being
        // converted to a FailureStatus result). Treating that the same as "the caller went away"
        // left the check seeded Healthy forever, since nothing ever calls Update() for it again -
        // the opposite of what a health check is for. A check that can't even be constructed must
        // be reported Unhealthy (or the registration's own FailureStatus), not silently ignored.
        [Fact]
        public async Task CheckHealthPlusAsync_ShouldReportFailureStatus_WhenTheCheckFactoryThrows()
        {
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test1"]);
            cache.Running("Test1", true);
            cache.Update("Test1", HealthCheckTrigger.Background, new HealthCheckResult(HealthStatus.Healthy), DateTime.UtcNow.AddSeconds(-10), TimeSpan.Zero);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test1", _ => throw new InvalidOperationException("simulated factory failure"), null, null));

            var healthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1), "Test1");

            var service = BuildService(cache, hcOptions, healthyPolicy);

            await Assert.ThrowsAnyAsync<Exception>(() => service.CheckHealthPlusAsync(null, null, HealthCheckTrigger.UrlRequest, CancellationToken.None));

            var status = cache.FullStatus("Test1");
            Assert.False(status.Running);
            Assert.Equal(HealthStatus.Unhealthy, status.LastResult.Status);
        }

        // Regression test: CacheHealthCheckPlus's internal dictionary used a case-sensitive
        // (ordinal) comparer, while ValidateRegistrations, HealthReport.Entries and AddCheckLinkTo's
        // own name matching all treat check names case-insensitively. A name in the `names` list
        // passed to AddHealthChecksPlus that differed only in casing from its registration's Name
        // was treated as a phantom/missing entry by the fail-fast validations, rejecting a
        // configuration the rest of the library would otherwise accept.
        [Fact]
        public async Task CheckHealthPlusAsync_ShouldTreatCheckNames_AsCaseInsensitive()
        {
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["redis"]);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Redis", _ => new AlwaysHealthyCheck(), null, null));

            var healthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(30), "Redis");

            var service = BuildService(cache, hcOptions, healthyPolicy);

            var report = await service.CheckHealthPlusAsync(null, null, HealthCheckTrigger.UrlRequest, CancellationToken.None);

            Assert.Equal(HealthStatus.Healthy, report.Status);
        }

        // Regression test: dtref is captured once, before the batch's tasks are started - the
        // success branch correctly adds the task's own Duration to it, but the ambient-cancellation
        // branch passed dtref straight through to ReleaseRunning, unadjusted. Since an ambient
        // cancellation can only be observed *after* however long the batch actually ran (up to the
        // configured Timeout), this left DateRef stuck at the batch's start time instead of the
        // release time - defeating the whole point of advancing it (throttling the retry to the
        // check's normal period instead of hot-looping).
        [Fact]
        public async Task CheckHealthPlusAsync_ShouldAdvanceDateRefToReleaseTime_NotBatchStartTime_OnAmbientCancellation()
        {
            var check = new CancelableCheck();
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test1"]);
            cache.Running("Test1", true);
            cache.Update("Test1", HealthCheckTrigger.Background, new HealthCheckResult(HealthStatus.Healthy), DateTime.UtcNow.AddSeconds(-10), TimeSpan.Zero);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test1", _ => check, null, null));

            var healthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1), "Test1");

            var service = BuildService(cache, hcOptions, healthyPolicy);

            using var cts = new CancellationTokenSource();
            var callTask = service.CheckHealthPlusAsync(null, null, HealthCheckTrigger.UrlRequest, cts.Token);

            Assert.True(check.Started.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken), "The check never started.");

            // Let real time pass between the batch starting (dtref) and when it's actually
            // released, so a DateRef stuck at the batch's start time is distinguishable from one
            // correctly advanced to the release time.
            await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            var justBeforeCancel = DateTime.UtcNow;
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => callTask);

            var status = cache.FullStatus("Test1");
            Assert.True(status.DateRef >= justBeforeCancel.AddMilliseconds(-200),
                $"DateRef ({status.DateRef:o}) was stuck at the batch's start time instead of being advanced to the release time (~{justBeforeCancel:o}).");
        }

        // Regression test: .NET's async machinery routes ANY OperationCanceledException thrown by
        // an async delegate to the Canceled task state - regardless of which token it's tied to, or
        // even if it's a bespoke exception with no token at all. A registration Factory throwing an
        // OperationCanceledException that has nothing to do with the ambient token being cancelled
        // (e.g. a constructor's own internal timeout) must not be silently absorbed as "the caller
        // went away" - it's a genuine check failure, just like any other unhandled exception from a
        // Factory, and must be reported as such.
        [Fact]
        public async Task CheckHealthPlusAsync_ShouldReportFailureStatus_WhenTheCheckFactoryThrowsOperationCanceledException_WithoutRealCancellation()
        {
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test1"]);
            cache.Running("Test1", true);
            cache.Update("Test1", HealthCheckTrigger.Background, new HealthCheckResult(HealthStatus.Healthy), DateTime.UtcNow.AddSeconds(-10), TimeSpan.Zero);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test1", _ => throw new OperationCanceledException("simulated non-cancellation OCE"), null, null));

            var healthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1), "Test1");

            var service = BuildService(cache, hcOptions, healthyPolicy);

            // CancellationToken.None - the ambient token is never cancelled, so an
            // OperationCanceledException thrown here cannot legitimately mean "the caller went away".
            await Assert.ThrowsAnyAsync<Exception>(() => service.CheckHealthPlusAsync(null, null, HealthCheckTrigger.UrlRequest, CancellationToken.None));

            var status = cache.FullStatus("Test1");
            Assert.False(status.Running);
            Assert.Equal(HealthStatus.Unhealthy, status.LastResult.Status);
        }

        // Regression test: the previous fix's discriminator - task.IsCanceled &&
        // cancellationToken.IsCancellationRequested - still misclassifies a genuine construction
        // failure as ambient cancellation whenever some OTHER check in the same batch is the one
        // that actually causes the ambient token to fire. cancellationToken.IsCancellationRequested
        // is a batch-wide flag, not proof that THIS task's OperationCanceledException was caused by
        // that token - and a registration's Factory (Func<IServiceProvider, IHealthCheck>) has no
        // CancellationToken parameter at all, so it can never legitimately observe the ambient
        // token in the first place. Setup: "Broken"'s factory throws its own unrelated OCE (and
        // completes almost immediately); "Slow" blocks on the real ambient token. Cancelling the
        // token only after "Broken" has already finished reproduces the exact interleaving where
        // cancellationToken.IsCancellationRequested becomes true for a reason that has nothing to
        // do with "Broken" - it must still be reported Unhealthy, not silently released with its
        // stale Healthy result intact.
        [Fact]
        public async Task CheckHealthPlusAsync_ShouldReportFailureStatus_WhenACheckFactoryThrowsOperationCanceledException_WhileAnotherCheckIsGenuinelyCancelledInTheSameBatch()
        {
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Broken", "Slow"]);
            cache.Running("Broken", true);
            cache.Update("Broken", HealthCheckTrigger.Background, new HealthCheckResult(HealthStatus.Healthy), DateTime.UtcNow.AddSeconds(-10), TimeSpan.Zero);
            cache.Running("Slow", true);
            cache.Update("Slow", HealthCheckTrigger.Background, new HealthCheckResult(HealthStatus.Healthy), DateTime.UtcNow.AddSeconds(-10), TimeSpan.Zero);

            var slowCheck = new CancelableCheck();
            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Broken", _ => throw new OperationCanceledException("simulated non-cancellation OCE"), null, null));
            hcOptions.Registrations.Add(new HealthCheckRegistration("Slow", _ => slowCheck, null, null));

            var brokenPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1), "Broken");
            var slowPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1), "Slow");

            var service = BuildService(cache, hcOptions, brokenPolicy, slowPolicy);

            using var cts = new CancellationTokenSource();
            var callTask = service.CheckHealthPlusAsync(null, null, HealthCheckTrigger.UrlRequest, cts.Token);

            Assert.True(slowCheck.Started.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken), "The slow check never started.");

            // Give "Broken"'s task time to actually finish (it has nothing to await) before the
            // ambient token fires, so cancellationToken.IsCancellationRequested becoming true is
            // unambiguously caused by "Slow", not "Broken".
            await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
            cts.Cancel();

            // Task.WhenAll surfaces a Faulted task's exception over a merely Canceled one, so once
            // "Broken" is correctly routed to Faulted this throws its exception rather than "Slow"'s
            // OperationCanceledException - the exact exception type isn't the point of this test,
            // only that "Broken" ends up reported Unhealthy below.
            await Assert.ThrowsAnyAsync<Exception>(() => callTask);

            var status = cache.FullStatus("Broken");
            Assert.False(status.Running);
            Assert.Equal(HealthStatus.Unhealthy, status.LastResult.Status);
        }

        // Regression test: the same batch-wide-flag-as-causation-proxy defect also lives one layer
        // deeper, in RunCheckAsync's own guard: `catch (OperationCanceledException ex) when
        // (!cancellationToken.IsCancellationRequested)`. cancellationToken.IsCancellationRequested is
        // true for the whole batch once ANY check triggers real ambient cancellation - it is not
        // proof that THIS check's own OperationCanceledException (e.g. an HttpClient's internal
        // timeout, unrelated to the token CheckHealthAsync was actually handed) was caused by that
        // same cancellation. When some other check in the batch causes the ambient token to fire,
        // this guard goes false, the exception is neither caught here nor by the next catch (which
        // explicitly excludes OperationCanceledException), so it propagates uncaught - the task ends
        // up Canceled, and the outer finally's IsCanceled/IsCancellationRequested check misclassifies
        // it as "the caller went away", silently releasing it with its stale result intact instead of
        // reporting the real failure.
        [Fact]
        public async Task CheckHealthPlusAsync_ShouldReportFailureStatus_WhenACheckThrowsAnUnrelatedOperationCanceledException_WhileAnotherCheckIsGenuinelyCancelledInTheSameBatch()
        {
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Broken", "Slow"]);
            cache.Running("Broken", true);
            cache.Update("Broken", HealthCheckTrigger.Background, new HealthCheckResult(HealthStatus.Healthy), DateTime.UtcNow.AddSeconds(-10), TimeSpan.Zero);
            cache.Running("Slow", true);
            cache.Update("Slow", HealthCheckTrigger.Background, new HealthCheckResult(HealthStatus.Healthy), DateTime.UtcNow.AddSeconds(-10), TimeSpan.Zero);

            var brokenCheck = new ThrowsUnrelatedOperationCanceledExceptionCheck();
            var slowCheck = new CancelableCheck();
            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Broken", _ => brokenCheck, null, null));
            hcOptions.Registrations.Add(new HealthCheckRegistration("Slow", _ => slowCheck, null, null));

            var brokenPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1), "Broken");
            var slowPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1), "Slow");

            var service = BuildService(cache, hcOptions, brokenPolicy, slowPolicy);

            using var cts = new CancellationTokenSource();
            var callTask = service.CheckHealthPlusAsync(null, null, HealthCheckTrigger.UrlRequest, cts.Token);

            Assert.True(brokenCheck.Started.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken), "The broken check never started.");
            Assert.True(slowCheck.Started.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken), "The slow check never started.");

            // Cancel the ambient token first (caused entirely by "Slow" blocking on it), THEN let
            // "Broken" throw its own unrelated OCE - so cancellationToken.IsCancellationRequested is
            // unambiguously true for a reason that has nothing to do with "Broken"'s own exception.
            cts.Cancel();
            brokenCheck.Release();

            await Assert.ThrowsAnyAsync<Exception>(() => callTask);

            var status = cache.FullStatus("Broken");
            Assert.False(status.Running);
            Assert.Equal(HealthStatus.Unhealthy, status.LastResult.Status);
        }
    }
}
