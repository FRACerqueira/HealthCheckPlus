// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Abstractions;
using HealthCheckPlus.Internal;
using HealthCheckPlus.Internal.Policies;
using HealthCheckPlus.Internal.WrapperMicrosoft;
using HealthCheckPlus.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
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

        private sealed class AlwaysUnhealthyCheck : IHealthCheck
        {
            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy());
            }
        }

        // Signals once it has actually returned its (real, completed) result - used to guarantee
        // this check's own Update() has already landed before an ambient token is cancelled,
        // instead of racing it (a plain AlwaysUnhealthyCheck could otherwise be swept into
        // AmbientCancellation too, if the shared token happens to be cancelled before its task
        // even starts running on the thread pool).
        private sealed class SignalingUnhealthyCheck : IHealthCheck
        {
            public readonly ManualResetEventSlim Completed = new(false);

            public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                Completed.Set();
                return Task.FromResult(HealthCheckResult.Unhealthy());
            }
        }

        // A check with a configurable, observable duration - used to prove that a fast check's
        // DateRef reflects its OWN duration, not the overall duration of a batch it happens to
        // share with a much slower check.
        private sealed class DelayedHealthyCheck(TimeSpan delay) : IHealthCheck
        {
            public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                return HealthCheckResult.Healthy();
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
            params HealthCheckPlusPolicyStatus[] policies)
        {
            return BuildService(cache, hcOptions, NullLogger<HealthCheckService>.Instance, policies);
        }

        private static DefaultHealthCheckServicePlus BuildService(
            CacheHealthCheckPlus cache,
            HealthCheckServiceOptions hcOptions,
            ILogger<HealthCheckService> logger,
            params HealthCheckPlusPolicyStatus[] policies)
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
                logger,
                Options.Create(hcOptions),
                new HealthChecksPlusRegistrationState());
        }

        // A logging provider that throws on every call - simulates a broken third-party sink
        // (e.g. a misconfigured exporter, a file logger hitting a permission error).
        private sealed class ThrowingLogger : ILogger<HealthCheckService>
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                throw new InvalidOperationException("Simulated broken logging provider.");
            }
        }

        // Throws only for the one named event under test, so a batch-start log (already guarded
        // elsewhere) doesn't throw before the checks it's meant to isolate ever get a chance to run.
        private sealed class SelectivelyThrowingLogger(string throwingEventName) : ILogger<HealthCheckService>
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (eventId.Name == throwingEventName)
                {
                    throw new InvalidOperationException($"Simulated broken logging provider for '{throwingEventName}'.");
                }
            }
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

        // Regression test: the cache's per-check Tags (used by CacheHealthCheckPlus.CreateReport,
        // which a callback registered via AddStatusName consumes through IStateHealthChecksPlus.
        // Status) must be populated from the real registrations at construction time - InitCache
        // alone has no access to them (only the check names it's seeded with).
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

        // Regression test: ValidateHealthyPolicies only catches a registered check with no matching
        // policy - nothing previously caught the opposite direction, a policy naming a check that
        // was never actually registered (e.g. a typo, or a casing mismatch, in AddUnhealthyPolicy/
        // AddDegradedPolicy's target name). This must also fail fast at construction.
        [Fact]
        public void Constructor_ShouldThrowClearException_WhenAPolicyTargetsAnUnregisteredCheckName()
        {
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test1"]);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test1", _ => new AlwaysHealthyCheck(), null, null));

            var healthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(30), "Test1");
            var orphanedPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Unhealthy, TimeSpan.Zero, TimeSpan.FromSeconds(10), "Tset1");

            var ex = Assert.Throws<InvalidOperationException>(() => BuildService(cache, hcOptions, healthyPolicy, orphanedPolicy));
            Assert.Contains("Tset1", ex.Message, StringComparison.Ordinal);
        }

        // Regression test: FindPolicy/ValidateHealthyPolicies/ValidatePolicyTargets used to compare
        // policy names ordinally (case-sensitively), while CacheHealthCheckPlus's own cache
        // (_statusDeps) and HealthReport.Entries already treat a check name case-insensitively - a
        // policy registered for "test1" would silently never match a check actually named "Test1".
        // All three now compare OrdinalIgnoreCase, matching the cache.
        [Fact]
        public void Constructor_ShouldAcceptPolicy_WhenItsNameDiffersOnlyByCasing_FromTheRegisteredCheckName()
        {
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test1"]);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test1", _ => new AlwaysHealthyCheck(), null, null));

            var healthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(30), "test1");

            // No exception thrown is the assertion.
            BuildService(cache, hcOptions, healthyPolicy);
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

        // Regression test: Log.HealthCheckProcessingBegin (and every other Log.* call in this
        // class) now goes through SafeLog, so a broken logging provider must not stop checks from
        // running at all - previously it propagated before the try/finally that normally releases
        // Running (via ApplyBatchResults) ever got a chance to open, leaving the check marked
        // Running forever and turning a purely-diagnostic failure into a 500 on /health. The check
        // must run to completion and be reported normally; only a metric records the logging
        // failure. "Every log call" here means every Log.* call specifically - ThrowingLogger's
        // BeginScope returns null rather than throwing, so RunCheckAsync's own
        // `_logger.BeginScope(...)` (the one logger interaction not behind SafeLog, deliberately
        // out of scope for this fix - see its own comment) is not exercised by this test.
        [Fact]
        public async Task CheckHealthPlusAsync_ShouldRunNormally_WhenEveryGuardedLogCallThrows()
        {
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test1"]);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test1", _ => new AlwaysHealthyCheck(), null, null));

            var healthyPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1000), "Test1");

            var service = BuildService(cache, hcOptions, new ThrowingLogger(), healthyPolicy);

            using var capture = new MetricsCapture();
            var report = await service.CheckHealthPlusAsync(null, null, HealthCheckTrigger.UrlRequest, CancellationToken.None);

            Assert.Equal(HealthStatus.Healthy, report.Entries["Test1"].Status);
            Assert.False(cache.FullStatus("Test1").Running,
                "The check was left marked Running even though it ran to completion.");
            // Origin=UrlRequest (not the InitCache seed's None) proves Update() actually ran for
            // this request, rather than the seeded value merely happening to already be Healthy.
            Assert.Equal(HealthCheckTrigger.UrlRequest, cache.FullStatus("Test1").Origin);

            // Not swallowed with zero signal: SafeLog's catch must actually record the
            // logging_sink_failed anomaly for at least one of the several log calls this request
            // made, not just avoid throwing.
            Assert.Contains(capture.Measurements, m =>
                m.InstrumentName == "healthcheckplus.anomalies" &&
                m.Tags.TryGetValue("healthcheckplus.anomaly.reason", out var reason) &&
                Equals(reason, "logging_sink_failed"));
        }

        // Regression test: ApplyBatchResults's AmbientCancellation branch logs
        // (Log.HealthCheckExecutionAborted) before calling ReleaseRunning - now through SafeLog, so
        // a throwing logger on the FIRST item in the batch must not abort the loop before it ever
        // reaches a LATER item, and must not turn a legitimate ambient cancellation into a
        // fabricated AggregateException that mixes it with the logging provider's own exception.
        // Test1 is the one that hits AmbientCancellation (and the throwing logger); Test2
        // completes normally and is only ever reached if the loop survives Test1's log failure.
        [Fact]
        public async Task CheckHealthPlusAsync_ShouldContinueTheBatch_WhenTheExecutionAbortedLogThrows()
        {
            var check1 = new CancelableCheck();
            var check2 = new AlwaysHealthyCheck();
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test1", "Test2"]);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test1", _ => check1, null, null));
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test2", _ => check2, null, null));

            var healthyPolicy1 = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1000), "Test1");
            var healthyPolicy2 = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1000), "Test2");

            var service = BuildService(cache, hcOptions, new SelectivelyThrowingLogger("HealthCheckExecutionAborted"), healthyPolicy1, healthyPolicy2);

            using var cts = new CancellationTokenSource();
            var callTask = service.CheckHealthPlusAsync(null, null, HealthCheckTrigger.UrlRequest, cts.Token);

            Assert.True(check1.Started.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken), "The check never started.");
            cts.Cancel();

            // Only the real ambient cancellation surfaces - not an AggregateException mixing it
            // with the logging provider's own exception, which SafeLog now swallows (recording a
            // metric instead).
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => callTask);

            // Both items must be released - if the loop had aborted at Test1 (the old bug),
            // Test2's iteration in ApplyBatchResults's per-item loop would never run at all,
            // leaving it stuck Running=true forever regardless of what its own task's outcome
            // was. (Test2 is also cancelled by the shared ambient token by the time its turn
            // comes up, so it takes the same AmbientCancellation - and same throwing-logger -
            // path as Test1; the point is that path completes for it too instead of the loop
            // dying on Test1's first log failure.)
            Assert.False(cache.FullStatus("Test1").Running,
                "Test1 (whose AmbientCancellation log call threw) was left permanently marked Running.");
            Assert.False(cache.FullStatus("Test2").Running,
                "Test2 (a later item in the same batch) was left permanently marked Running because Test1's logging failure aborted the loop before ever reaching it.");
        }

        // Equivalence regression test: a named status aggregate (AddStatusName/Status(name)) must
        // reflect a check's result the same cycle Update() commits it, regardless of whether the
        // overall batch is about to be reported as faulted to THIS caller - the same guarantee
        // BackGroudCheckHealthPlusAsync already gave (it calls UpdateStatusName() before its own
        // equivalent rethrow). Test1 is ambiently cancelled (this request's caller went away);
        // Test2 completes normally, genuinely Unhealthy, before the cancellation even happens.
        // Confirmed this used to diverge: CheckHealthPlusAsync rethrew whenAllFailure BEFORE ever
        // reaching UpdateStatusName(), so Test2's already-committed Unhealthy result never made it
        // into any named aggregate this cycle - Status("agg") stayed stuck at whatever it was
        // before this (failed) request, even though the cache itself was already up to date.
        [Fact]
        public async Task CheckHealthPlusAsync_ShouldRefreshNamedAggregate_EvenWhenTheBatchIsReportedAsFaulted()
        {
            var check1 = new CancelableCheck();
            var check2 = new SignalingUnhealthyCheck();
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test1", "Test2"]);
            cache.AddStatusName(new HealthCheckPlusOptions
            {
                HealthCheckName = "agg",
                StatusHealthReport = report => report.Entries.TryGetValue("Test2", out var entry) ? entry.Status : HealthStatus.Healthy
            }, includeName: null);

            // Warm _statusName["agg"] with the current (Healthy) state BEFORE the faulted request
            // below - Status(name) recomputes on its own the very first time a name is ever read
            // (see Status(name)'s own cold-compute path), which would mask this exact bug: with
            // "agg" never cached at all, Status("agg") would independently recompute from the live
            // cache regardless of whether UpdateStatusName() itself ran during the request below.
            // Only a name that's already cached can actually go stale.
            cache.UpdateStatusName();
            Assert.Equal(HealthStatus.Healthy, cache.Status("agg"));

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test1", _ => check1, null, null));
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test2", _ => check2, null, null));

            var healthyPolicy1 = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1000), "Test1");
            var healthyPolicy2 = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1000), "Test2");

            var service = BuildService(cache, hcOptions, healthyPolicy1, healthyPolicy2);

            using var cts = new CancellationTokenSource();
            var callTask = service.CheckHealthPlusAsync(null, null, HealthCheckTrigger.UrlRequest, cts.Token);

            Assert.True(check1.Started.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken), "Test1 never started.");
            Assert.True(check2.Completed.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken), "Test2 never completed.");
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => callTask);

            Assert.Equal(HealthStatus.Unhealthy, cache.Status("agg"));
        }

        // Regression test: the fix above (running UpdateStatusName() before the rethrow) moved the
        // call earlier, but left it OUTSIDE the try/catch that combines a batch failure with a
        // second, independent failure - so a throwing StatusHealthReport delegate propagated
        // directly from UpdateStatusName(), bypassing whenAllFailure?.Throw() entirely and silently
        // discarding the real ambient-cancellation failure it was capturing. BackGroudCheckHealthPlusAsync
        // never had this gap (its own UpdateStatusName() call already sat inside the guarded try).
        // Found by a ninth independent audit round applying the same equivalence-test discipline
        // (Gate 2) that caught the original asymmetry this fix was for - confirmed red against the
        // version with UpdateStatusName() outside the try (it surfaced only InvalidOperationException,
        // silently losing the OperationCanceledException), green with it moved inside.
        [Fact]
        public async Task CheckHealthPlusAsync_ShouldCombineBothFailures_WhenTheBatchFaultsAndUpdateStatusNameAlsoThrows()
        {
            var check1 = new CancelableCheck();
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test1"]);
            cache.AddStatusName(new HealthCheckPlusOptions
            {
                HealthCheckName = "agg",
                StatusHealthReport = _ => throw new InvalidOperationException("BOOM")
            }, includeName: null);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Test1", _ => check1, null, null));

            var healthyPolicy1 = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1000), "Test1");

            var service = BuildService(cache, hcOptions, healthyPolicy1);

            using var cts = new CancellationTokenSource();
            var callTask = service.CheckHealthPlusAsync(null, null, HealthCheckTrigger.UrlRequest, cts.Token);

            Assert.True(check1.Started.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken), "Test1 never started.");
            cts.Cancel();

            var ex = await Assert.ThrowsAsync<AggregateException>(() => callTask);
            var inner = ex.Flatten().InnerExceptions;
            Assert.Contains(inner, e => e is OperationCanceledException);
            Assert.Contains(inner, e => e is InvalidOperationException && e.Message == "BOOM");
        }

        // BackGroudCheckHealthPlusAsync gets the same release-on-throw guard around StartBatch as
        // CheckHealthPlusAsync above, for consistency, but it has no realistic trigger today:
        // BackGroudCheckHealthPlusAsync never logs directly in that span, and Task.Run itself does
        // not throw synchronously even when handed a token from an already-disposed
        // CancellationTokenSource (confirmed empirically - it simply schedules the work). Left as a
        // defensive backstop, the same category as InternalCast's null-value handling: no test
        // manufactures a scenario that doesn't currently exist just to exercise it.

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

        // Regression guard for ApplyBatchResults' success branch (dtref.Add(task.Result.Duration)):
        // a fast check's DateRef must reflect the batch's start time plus ITS OWN duration, not
        // the release time of the whole batch - which a much slower check running concurrently in
        // the same batch would inflate. Nothing else pins this after centralizing the
        // finally-block logic shared by CheckHealthPlusAsync and BackGroudCheckHealthPlusAsync.
        [Fact]
        public async Task CheckHealthPlusAsync_ShouldSetDateRefToBatchStartPlusOwnDuration_OnSuccess_EvenWhenAnotherCheckInTheSameBatchIsSlower()
        {
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Fast", "Slow"]);

            var hcOptions = new HealthCheckServiceOptions();
            hcOptions.Registrations.Add(new HealthCheckRegistration("Fast", _ => new AlwaysHealthyCheck(), null, null));
            hcOptions.Registrations.Add(new HealthCheckRegistration("Slow", _ => new DelayedHealthyCheck(TimeSpan.FromSeconds(2)), null, null));

            var fastPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1000), "Fast");
            var slowPolicy = new HealthCheckPlusPolicyStatus(HealthStatus.Healthy, TimeSpan.Zero, TimeSpan.FromSeconds(1000), "Slow");

            var service = BuildService(cache, hcOptions, fastPolicy, slowPolicy);

            var batchStart = DateTime.UtcNow;
            await service.CheckHealthPlusAsync(null, null, HealthCheckTrigger.UrlRequest, CancellationToken.None);

            var fastStatus = cache.FullStatus("Fast");
            Assert.True(fastStatus.DateRef < batchStart.AddMilliseconds(800),
                $"Fast check's DateRef ({fastStatus.DateRef:o}, batch started {batchStart:o}) reflects the slow check's ~2s duration instead of its own near-instant one.");
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

        // Direct, synchronous coverage of the classification rule shared by CheckHealthPlusAsync's
        // and BackGroudCheckHealthPlusAsync's finally blocks - the exact piece that a variant of
        // the same bug kept reappearing in across independent reviews, once per call site, before
        // it was centralized into DefaultHealthCheckServicePlus.ClassifyBatchTask. These use
        // synthetic Task states (Task.FromResult/FromCanceled/FromException) instead of exercising
        // real async timing, so they exist alongside - not instead of - the integration-style
        // tests above that already cover the real RunCheckAsync plumbing.
        [Fact]
        public void ClassifyBatchTask_ReturnsSuccess_WhenTaskCompletedSuccessfully()
        {
            var task = Task.FromResult(0);

            var outcome = DefaultHealthCheckServicePlus.ClassifyBatchTask(task, CancellationToken.None);

            Assert.Equal(DefaultHealthCheckServicePlus.BatchTaskOutcome.Success, outcome);
        }

        [Fact]
        public void ClassifyBatchTask_ReturnsAmbientCancellation_WhenTaskCanceled_AndAmbientTokenWasRequested()
        {
            var task = Task.FromCanceled(new CancellationToken(canceled: true));

            var outcome = DefaultHealthCheckServicePlus.ClassifyBatchTask(task, new CancellationToken(canceled: true));

            Assert.Equal(DefaultHealthCheckServicePlus.BatchTaskOutcome.AmbientCancellation, outcome);
        }

        // Guards the exact gap the third narrow-review round closed one layer down inside
        // RunCheckAsync: a task ending up Canceled is not, by itself, proof that the ambient token
        // caused it. This classifier only treats it as ambient cancellation when the ambient token
        // was ALSO actually requested - it relies on RunCheckAsync's own invariant (documented on
        // ClassifyBatchTask) that no other kind of OperationCanceledException can reach this point
        // as a Canceled task.
        [Fact]
        public void ClassifyBatchTask_ReturnsFailure_WhenTaskCanceled_ButAmbientTokenWasNotRequested()
        {
            var task = Task.FromCanceled(new CancellationToken(canceled: true));

            var outcome = DefaultHealthCheckServicePlus.ClassifyBatchTask(task, CancellationToken.None);

            Assert.Equal(DefaultHealthCheckServicePlus.BatchTaskOutcome.Failure, outcome);
        }

        [Fact]
        public void ClassifyBatchTask_ReturnsFailure_WhenTaskFaulted()
        {
            var task = Task.FromException(new InvalidOperationException("simulated failure"));

            var outcome = DefaultHealthCheckServicePlus.ClassifyBatchTask(task, new CancellationToken(canceled: true));

            Assert.Equal(DefaultHealthCheckServicePlus.BatchTaskOutcome.Failure, outcome);
        }
    }
}
