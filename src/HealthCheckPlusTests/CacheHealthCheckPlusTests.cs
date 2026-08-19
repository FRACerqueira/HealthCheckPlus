// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Abstractions;
using HealthCheckPlus.Internal;
using HealthCheckPlus.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace HealthCheckPlusTests
{
    public class CacheHealthCheckPlusTests
    {
        private readonly CacheHealthCheckPlus _cacheHealthCheckPlus;

        public CacheHealthCheckPlusTests()
        {
            _cacheHealthCheckPlus = new CacheHealthCheckPlus();
        }

        [Fact]
        public void AddStatusName_ShouldAddStatusFunction()
        {
            var options = new HealthCheckPlusOptions
            {
                HealthCheckName = "Test",
                StatusHealthReport = report => HealthStatus.Healthy
            };

            _cacheHealthCheckPlus.AddStatusName(options, includeName: null);

            Assert.Throws<ArgumentException>(() => _cacheHealthCheckPlus.AddStatusName(options, includeName: null));
        }

        [Fact]
        public void InitCache_ShouldInitializeStatusDeps()
        {
            var names = new List<string> { "Test1", "Test2" };

            _cacheHealthCheckPlus.InitCache(names);

            Assert.Equal("Test1", _cacheHealthCheckPlus.FullStatus("Test1").Name);
            Assert.Equal("Test2", _cacheHealthCheckPlus.FullStatus("Test2").Name);
        }

        // Regression test: CreateReport(includeName) excludes a check that hasn't actually run yet
        // (InitCache's seed - Healthy, Origin=None) even when includeName accepts its name - used by
        // HealthCheckPlusBackGroundService to keep a not-yet-run check out of what it
        // publishes/hashes instead of reporting the seed as a real Healthy observation. Deciding
        // this from the same Snapshot read used to build the entry (rather than a separate
        // HasEverRun-style call made after a plain CreateReport() already snapshotted the report)
        // is what closes the phantom-seed window described on CreateReport(includeName) itself.
        [Fact]
        public void CreateReportWithIncludeName_ShouldExcludeACheckThatHasNeverRun()
        {
            _cacheHealthCheckPlus.InitCache(["Test1"]);

            var report = _cacheHealthCheckPlus.CreateReport(_ => true);
            Assert.False(report.Entries.ContainsKey("Test1"));

            _cacheHealthCheckPlus.Running("Test1", true);
            _cacheHealthCheckPlus.Update("Test1", HealthCheckTrigger.Background, new HealthCheckResult(HealthStatus.Healthy), DateTime.UtcNow, TimeSpan.Zero);

            report = _cacheHealthCheckPlus.CreateReport(_ => true);
            Assert.True(report.Entries.ContainsKey("Test1"));
        }

        [Fact]
        public void CreateReportWithIncludeName_ShouldExcludeANameIncludeNameRejects()
        {
            _cacheHealthCheckPlus.InitCache(["Test1", "Test2"]);
            _cacheHealthCheckPlus.Running("Test1", true);
            _cacheHealthCheckPlus.Update("Test1", HealthCheckTrigger.Background, new HealthCheckResult(HealthStatus.Healthy), DateTime.UtcNow, TimeSpan.Zero);
            _cacheHealthCheckPlus.Running("Test2", true);
            _cacheHealthCheckPlus.Update("Test2", HealthCheckTrigger.Background, new HealthCheckResult(HealthStatus.Healthy), DateTime.UtcNow, TimeSpan.Zero);

            var report = _cacheHealthCheckPlus.CreateReport(name => name == "Test1");

            Assert.True(report.Entries.ContainsKey("Test1"));
            Assert.False(report.Entries.ContainsKey("Test2"));
        }

        [Fact]
        public void UpdateStatusName_ShouldUpdateStatusName()
        {
            var options = new HealthCheckPlusOptions
            {
                HealthCheckName = "Test",
                StatusHealthReport = report => HealthStatus.Healthy
            };

            _cacheHealthCheckPlus.AddStatusName(options, includeName: null);
            _cacheHealthCheckPlus.InitCache(["Test"]);
            _cacheHealthCheckPlus.UpdateStatusName();

            Assert.Equal(HealthStatus.Healthy, _cacheHealthCheckPlus.Status("Test"));
        }

        // Regression test: UpdateStatusName() used to always invoke a registered StatusHealthReport
        // delegate with the full, unfiltered cache - regardless of the includeName filter the same
        // registration's own Predicate translates to (see AddStatusName's own comment). "Excluded"
        // is genuinely Unhealthy here, but never enters the report this delegate is invoked with,
        // because includeName excludes it - the same scoping the registration's own endpoint would
        // apply to its HTTP response.
        [Fact]
        public void UpdateStatusName_ShouldScopeTheReport_ByTheRegistrationsOwnIncludeName()
        {
            _cacheHealthCheckPlus.InitCache(["Included", "Excluded"]);
            _cacheHealthCheckPlus.Running("Excluded", true);
            _cacheHealthCheckPlus.Update("Excluded", HealthCheckTrigger.Background, new HealthCheckResult(HealthStatus.Unhealthy), DateTime.UtcNow, TimeSpan.Zero);

            var options = new HealthCheckPlusOptions
            {
                HealthCheckName = "live",
                StatusHealthReport = report => report.Entries.ContainsKey("Excluded") ? HealthStatus.Unhealthy : HealthStatus.Healthy
            };
            _cacheHealthCheckPlus.AddStatusName(options, includeName: name => name == "Included");

            _cacheHealthCheckPlus.UpdateStatusName();

            Assert.Equal(HealthStatus.Healthy, _cacheHealthCheckPlus.Status("live"));
        }

        // Regression test: a mix of unfiltered names (includeName: null) and a genuinely filtered
        // name, all updated in the same UpdateStatusName() call, must each compute independently
        // and correctly - a tempting optimization (sharing one built HealthReport instance across
        // every unfiltered name, since they'd read identical content) was tried and reverted: that
        // report's Entries is, at runtime, the same mutable Dictionary its constructor was handed
        // (HealthReport.Entries is only IReadOnlyDictionary at compile time), so sharing it would
        // let one misbehaving StatusHealthReport delegate (one that downcasts and writes to it)
        // silently corrupt every other unfiltered name sharing that instance - exactly the
        // cross-aggregate leak includeName's own isolation exists to prevent. This test doesn't
        // exercise that failure mode directly (each name still gets its own report instance); it
        // exists to keep the mixed-name case covered now that it was considered.
        [Fact]
        public void UpdateStatusName_ShouldComputeEachNameIndependently_WhenMixingFilteredAndUnfilteredNames()
        {
            _cacheHealthCheckPlus.InitCache(["Included", "Excluded"]);
            _cacheHealthCheckPlus.Running("Excluded", true);
            _cacheHealthCheckPlus.Update("Excluded", HealthCheckTrigger.Background, new HealthCheckResult(HealthStatus.Unhealthy), DateTime.UtcNow, TimeSpan.Zero);

            _cacheHealthCheckPlus.AddStatusName(new HealthCheckPlusOptions
            {
                HealthCheckName = "default1",
                StatusHealthReport = report => report.Entries.ContainsKey("Excluded") ? HealthStatus.Unhealthy : HealthStatus.Healthy
            }, includeName: null);
            _cacheHealthCheckPlus.AddStatusName(new HealthCheckPlusOptions
            {
                HealthCheckName = "default2",
                StatusHealthReport = report => report.Entries.ContainsKey("Excluded") ? HealthStatus.Unhealthy : HealthStatus.Healthy
            }, includeName: null);
            _cacheHealthCheckPlus.AddStatusName(new HealthCheckPlusOptions
            {
                HealthCheckName = "scoped",
                StatusHealthReport = report => report.Entries.ContainsKey("Excluded") ? HealthStatus.Unhealthy : HealthStatus.Healthy
            }, includeName: name => name == "Included");

            _cacheHealthCheckPlus.UpdateStatusName();

            Assert.Equal(HealthStatus.Unhealthy, _cacheHealthCheckPlus.Status("default1"));
            Assert.Equal(HealthStatus.Unhealthy, _cacheHealthCheckPlus.Status("default2"));
            Assert.Equal(HealthStatus.Healthy, _cacheHealthCheckPlus.Status("scoped"));
        }

        // Regression test: a StatusHealthReport delegate that calls SwitchToUnhealthy/
        // SwitchToDegraded re-enters UpdateStatusName() through SwithState's own call to it -
        // and again, and again, since the same delegate runs every time - which would otherwise
        // recurse until an uncatchable StackOverflowException kills the process, with no log, no
        // metric, and no way for any try/catch to intervene. Found by a ninth independent audit
        // round. A same-thread reentrancy guard now converts this into a clear, catchable
        // InvalidOperationException instead.
        [Fact]
        public void UpdateStatusName_ShouldThrowClearException_RatherThanRecurseForever_WhenStatusHealthReportCallsSwitchTo()
        {
            _cacheHealthCheckPlus.InitCache(["Test1"]);
            _cacheHealthCheckPlus.AddStatusName(new HealthCheckPlusOptions
            {
                HealthCheckName = "agg",
                StatusHealthReport = _ =>
                {
                    _cacheHealthCheckPlus.SwitchToUnhealthy("Test1");
                    return HealthStatus.Healthy;
                }
            }, includeName: null);

            var ex = Assert.Throws<InvalidOperationException>(() => _cacheHealthCheckPlus.UpdateStatusName());
            Assert.Contains("StatusHealthReport", ex.Message, StringComparison.Ordinal);
        }

        // Same regression as above, for Status(name)'s own cold-compute path (the first-ever call
        // for a name, before any UpdateStatusName() cycle has populated _statusName for it) -
        // Update() alone (unlike SwithState) never calls UpdateStatusName(), so this reaches
        // Status(name)'s own report-building code, not UpdateStatusName()'s.
        [Fact]
        public void Status_ShouldScopeTheReport_ByTheRegistrationsOwnIncludeName_OnItsFirstColdCompute()
        {
            _cacheHealthCheckPlus.InitCache(["Included", "Excluded"]);
            _cacheHealthCheckPlus.Running("Excluded", true);
            _cacheHealthCheckPlus.Update("Excluded", HealthCheckTrigger.Background, new HealthCheckResult(HealthStatus.Unhealthy), DateTime.UtcNow, TimeSpan.Zero);

            var options = new HealthCheckPlusOptions
            {
                HealthCheckName = "live",
                StatusHealthReport = report => report.Entries.ContainsKey("Excluded") ? HealthStatus.Unhealthy : HealthStatus.Healthy
            };
            _cacheHealthCheckPlus.AddStatusName(options, includeName: name => name == "Included");

            Assert.Equal(HealthStatus.Healthy, _cacheHealthCheckPlus.Status("live"));
        }

        // Regression test: SwitchToUnhealthy/SwitchToDegraded used to update the check's own last
        // result via Update() but never call UpdateStatusName() - so a named aggregate registered
        // via AddStatusName (Status(name)) kept reporting the pre-override value until the next
        // request or background cycle happened to call UpdateStatusName() on its own, however long
        // that took. A consumer following the documented pattern (catch an exception,
        // SwitchToUnhealthy, then gate traffic on Status("someName")) could see stale traffic
        // decisions for an unbounded amount of time with no other symptom.
        [Fact]
        public void SwitchToUnhealthy_ShouldImmediatelyRefreshNamedStatus_WithoutAnExplicitUpdateStatusNameCall()
        {
            _cacheHealthCheckPlus.InitCache(["Test1"]);

            var options = new HealthCheckPlusOptions
            {
                HealthCheckName = "Named",
                StatusHealthReport = report => report.Entries["Test1"].Status
            };
            _cacheHealthCheckPlus.AddStatusName(options, includeName: null);
            _cacheHealthCheckPlus.UpdateStatusName();

            Assert.Equal(HealthStatus.Healthy, _cacheHealthCheckPlus.Status("Named"));

            _cacheHealthCheckPlus.SwitchToUnhealthy("Test1");

            Assert.Equal(HealthStatus.Unhealthy, _cacheHealthCheckPlus.Status("Named"));
        }

        // Regression test for a real, empirically-reproduced race: two concurrent UpdateStatusName()
        // calls each capture their own report and invoke their own (consumer-supplied, possibly
        // slow) StatusHealthReport delegate independently, then used to write _statusName directly
        // with no ordering between the two writes - whichever call's delegate happened to finish
        // LAST won, even if it started first and is working from a now-stale report. A slow call
        // that started BEFORE a manual override, but finishes AFTER the override's own (fast)
        // UpdateStatusName() call already wrote the fresh value, must not be allowed to overwrite it.
        // The delegate below blocks on its first invocation (simulating the slow, stale call) and
        // returns immediately on every later one (simulating the override's own fresh call).
        [Fact]
        public void UpdateStatusName_ShouldNotLetAStaleConcurrentCall_OverwriteAFresherOverride()
        {
            _cacheHealthCheckPlus.InitCache(["Test1"]);

            using var firstCallStarted = new ManualResetEventSlim(false);
            using var releaseFirstCall = new ManualResetEventSlim(false);
            var callCount = 0;

            var options = new HealthCheckPlusOptions
            {
                HealthCheckName = "Named",
                StatusHealthReport = _ =>
                {
                    if (Interlocked.Increment(ref callCount) == 1)
                    {
                        firstCallStarted.Set();
                        // Bounded, not an unbounded Wait(): if SwitchToUnhealthy below ever threw
                        // before releasing this (it doesn't today, but shouldn't be relied on), an
                        // unbounded wait here would hang this thread forever instead of just
                        // failing this test.
                        if (!releaseFirstCall.Wait(TimeSpan.FromSeconds(10)))
                        {
                            throw new TimeoutException("releaseFirstCall was never signaled.");
                        }
                        return HealthStatus.Healthy; // stale - computed before the override below
                    }
                    return HealthStatus.Unhealthy; // the override's own, fresh computation
                }
            };
            _cacheHealthCheckPlus.AddStatusName(options, includeName: null);

            // A dedicated Thread, not Task.Run: this call blocks synchronously for up to 10s
            // inside the delegate above, and the shared thread pool - already under pressure
            // from every other test running concurrently in the same process - isn't guaranteed
            // to grow fast enough to hand out a worker for it within that window, which made an
            // earlier version of this test using Task.Run intermittently fail on thread-pool
            // starvation alone, with nothing actually wrong in CacheHealthCheckPlus.
            //
            // Two hazards a bare Thread has that Task.Run didn't, both guarded against below:
            // IsBackground = true, so this thread can never be what keeps the test host process
            // alive past the end of the run if it somehow never reaches the Join() below; and the
            // delegate itself is wrapped in try/catch, since an unhandled exception on a raw
            // Thread (unlike a Task's, which is just observed by whoever awaits it) crashes the
            // entire process rather than merely failing this one test - the exception is captured
            // and rethrown after Join() instead, on the test's own thread.
            Exception? staleCallException = null;
            var staleCall = new Thread(() =>
            {
                try
                {
                    _cacheHealthCheckPlus.UpdateStatusName();
                }
                catch (Exception ex)
                {
                    staleCallException = ex;
                }
            })
            { IsBackground = true };
            staleCall.Start();
            Assert.True(firstCallStarted.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken),
                "The stale UpdateStatusName call's delegate never started.");

            // Update() (called by SwitchToUnhealthy) bumps the version, then SwithState's own
            // UpdateStatusName() call captures that newer version and writes Unhealthy immediately -
            // its delegate is the "second call" branch above, which returns without blocking.
            _cacheHealthCheckPlus.SwitchToUnhealthy("Test1");

            Assert.Equal(HealthStatus.Unhealthy, _cacheHealthCheckPlus.Status("Named"));

            // Now let the stale call's delegate finally return its old value and try to write it.
            releaseFirstCall.Set();
            Assert.True(staleCall.Join(TimeSpan.FromSeconds(10)), "The stale UpdateStatusName call never completed.");
            if (staleCallException != null)
            {
                throw staleCallException;
            }

            Assert.Equal(HealthStatus.Unhealthy, _cacheHealthCheckPlus.Status("Named"));
        }

        [Fact]
        public void LastReport_ShouldReturnMaxDateRef()
        {
            var names = new List<string> { "Test1", "Test2" };

            _cacheHealthCheckPlus.InitCache(names);

            Assert.NotNull(_cacheHealthCheckPlus.LastReport());
        }

        // Regression test: with zero registrations (AddHealthChecksPlus() called but no
        // AddCheckPlus/AddCheckLinkTo ever registered), _statusDeps is legitimately empty - LastReport
        // used to call .Max() on it directly, throwing InvalidOperationException ("Sequence contains
        // no elements") instead of reporting "no report yet" via the already-nullable return type.
        [Fact]
        public void LastReport_ShouldReturnNull_WhenNoChecksAreRegistered()
        {
            Assert.Null(_cacheHealthCheckPlus.LastReport());
        }

        // Regression test: same empty-sequence problem as LastReport, but for AggregateStatus (via
        // Status()) - .Min() on an empty _statusDeps.Values used to throw instead of returning a
        // sensible vacuous aggregate. Healthy matches native HealthReport.Status's own default for
        // zero entries.
        [Fact]
        public void Status_ShouldReturnHealthy_WhenNoChecksAreRegistered()
        {
            Assert.Equal(HealthStatus.Healthy, _cacheHealthCheckPlus.Status());
        }

        [Fact]
        public void CreateReport_ShouldReturnHealthReport()
        {
            var names = new List<string> { "Test1", "Test2" };

            _cacheHealthCheckPlus.InitCache(names);

            var report = _cacheHealthCheckPlus.CreateReport();

            Assert.NotNull(report);
        }

        // Regression test: CreateReport() (consumed by publishers and by Status()/UpdateStatusName)
        // used to hardcode null description, zero duration, no exception, no data and no tags for
        // every entry - even though CacheHealthCheckPlus already tracks all of that per check. This
        // meant a StatusHealthReport callback registered via AddStatusName (read back through
        // IStateHealthChecksPlus.Status) silently computed a different answer than the functionally
        // identical callback wired to the HTTP endpoint's own report, which is built with the real
        // values instead.
        [Fact]
        public void CreateReport_ShouldCarryTheSameDataAsTheLastResult()
        {
            _cacheHealthCheckPlus.InitCache(["Test1"]);
            _cacheHealthCheckPlus.Running("Test1", true);
            _cacheHealthCheckPlus.SetTags("Test1", ["tag-a", "tag-b"]);

            var data = new Dictionary<string, object> { ["key"] = "value" };
            var result = new HealthCheckResult(HealthStatus.Degraded, "custom description", new InvalidOperationException("boom"), data);
            _cacheHealthCheckPlus.Update("Test1", HealthCheckTrigger.UrlRequest, result, DateTime.UtcNow, TimeSpan.FromMilliseconds(250));

            var entry = _cacheHealthCheckPlus.CreateReport().Entries["Test1"];

            Assert.Equal(HealthStatus.Degraded, entry.Status);
            Assert.Equal("custom description", entry.Description);
            Assert.Equal(TimeSpan.FromMilliseconds(250), entry.Duration);
            Assert.IsType<InvalidOperationException>(entry.Exception);
            Assert.Equal("value", entry.Data["key"]);
            Assert.Equal(["tag-a", "tag-b"], entry.Tags);
        }

        // Regression test: FullStatus/StatusResult/SwithState/ConvertToPlus used to hit
        // ConcurrentDictionary's raw indexer for an unknown check name, throwing an unhelpful
        // KeyNotFoundException instead of a clear error naming what went wrong.
        [Fact]
        public void FullStatus_ShouldThrowClearException_WhenNameIsNotRegistered()
        {
            var ex = Assert.Throws<ArgumentException>(() => _cacheHealthCheckPlus.FullStatus("DoesNotExist"));
            Assert.Contains("DoesNotExist", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void StatusResult_ShouldThrowClearException_WhenNameIsNotRegistered()
        {
            var ex = Assert.Throws<ArgumentException>(() => _cacheHealthCheckPlus.StatusResult("DoesNotExist"));
            Assert.Contains("DoesNotExist", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void SwithState_ShouldThrowClearException_WhenNameIsNotRegistered()
        {
            var ex = Assert.Throws<ArgumentException>(() => _cacheHealthCheckPlus.SwithState("DoesNotExist", HealthStatus.Unhealthy));
            Assert.Contains("DoesNotExist", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ConvertToPlus_ShouldThrowClearException_WhenReportNameIsNotRegistered()
        {
            var entries = new Dictionary<string, HealthReportEntry>
            {
                ["DoesNotExist"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.Zero, null, null)
            };
            var report = new HealthReport(entries, TimeSpan.Zero);

            var ex = Assert.Throws<ArgumentException>(() => _cacheHealthCheckPlus.ConvertToPlus(report).ToArray());
            Assert.Contains("DoesNotExist", ex.Message, StringComparison.Ordinal);
        }

        // Regression test: ConvertToPlus used to return a lazy Select - every "Plus" response
        // writer enumerates it while a JSON response is already being serialized to the output
        // stream, so a failure here used to surface mid-write (some bytes of a truncated JSON
        // document already sent) instead of before any of them went out. Calling ConvertToPlus
        // without ever enumerating the result (no .ToArray()/.ToList()/foreach here) proves the
        // exception now happens inside the call itself, not deferred to the caller's enumeration.
        [Fact]
        public void ConvertToPlus_ShouldThrowImmediately_NotOnlyWhenTheResultIsLaterEnumerated()
        {
            var entries = new Dictionary<string, HealthReportEntry>
            {
                ["DoesNotExist"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.Zero, null, null)
            };
            var report = new HealthReport(entries, TimeSpan.Zero);

            Assert.Throws<ArgumentException>(() => _cacheHealthCheckPlus.ConvertToPlus(report));
        }

        [Fact]
        public void Status_ShouldReturnHealthStatus()
        {
            var names = new List<string> { "Test1", "Test2" };

            _cacheHealthCheckPlus.InitCache(names);

            Assert.Equal(HealthStatus.Healthy, _cacheHealthCheckPlus.Status());
        }

        [Fact]
        public void Running_ShouldUpdateRunningStatus()
        {
            var names = new List<string> { "Test1" };

            _cacheHealthCheckPlus.InitCache(names);
            _cacheHealthCheckPlus.Running("Test1", true);

            Assert.True(_cacheHealthCheckPlus.FullStatus("Test1").Running);
        }

        [Fact]
        public void Update_ShouldUpdateItemCacheHealth()
        {
            var names = new List<string> { "Test1" };

            _cacheHealthCheckPlus.InitCache(names);
            _cacheHealthCheckPlus.Running("Test1", true);

            var result = new HealthCheckResult(HealthStatus.Degraded);
            _cacheHealthCheckPlus.Update("Test1", HealthCheckTrigger.UrlRequest, result, DateTime.Now, TimeSpan.Zero);

            Assert.Equal(HealthStatus.Degraded, _cacheHealthCheckPlus.FullStatus("Test1").LastResult.Status);
        }

        [Fact]
        public void SwithState_ShouldSwitchState()
        {
            var names = new List<string> { "Test1" };

            _cacheHealthCheckPlus.InitCache(names);
            _cacheHealthCheckPlus.SwithState("Test1", HealthStatus.Unhealthy);

            Assert.Equal(HealthStatus.Unhealthy, _cacheHealthCheckPlus.FullStatus("Test1").LastResult.Status);
        }

        // A logging provider that throws on every call - simulates a broken third-party sink.
        private sealed class ThrowingLogger : ILogger<CacheHealthCheckPlus>
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                throw new InvalidOperationException("Simulated broken logging provider.");
            }
        }

        // Regression test: Update()'s "dropped" branches (an unregistered key, or no execution
        // marked Running for it) used to log completely unguarded - a throwing ILogger sink made
        // Update() itself throw, on the single point every execution path (foreground/HTTP,
        // background, and SwitchTo) converges on. Dropping a result must stay silent to the
        // caller regardless of whether the drop itself could be logged.
        [Fact]
        public void Update_ShouldNotThrow_WhenKeyIsNotRegistered_AndLoggingThrows()
        {
            var cache = new CacheHealthCheckPlus(new ThrowingLogger());

            var result = new HealthCheckResult(HealthStatus.Healthy);
            cache.Update("Unknown", HealthCheckTrigger.UrlRequest, result, DateTime.UtcNow, TimeSpan.Zero);
        }

        // Not swallowed with zero signal: SafeLog's catch must actually record the
        // logging_sink_failed anomaly, not just avoid throwing - the "no throw" tests above would
        // still pass if SafeLog's catch were reduced to an empty `catch { }`, which is exactly the
        // silent-catch shape this codebase's own doctrine forbids.
        [Fact]
        public void Update_ShouldRecordLoggingSinkFailedAnomaly_WhenKeyIsNotRegistered_AndLoggingThrows()
        {
            using var capture = new MetricsCapture();
            var cache = new CacheHealthCheckPlus(new ThrowingLogger());

            var result = new HealthCheckResult(HealthStatus.Healthy);
            cache.Update("Unknown", HealthCheckTrigger.UrlRequest, result, DateTime.UtcNow, TimeSpan.Zero);

            Assert.Contains(capture.Measurements, m =>
                m.InstrumentName == "healthcheckplus.anomalies" &&
                m.Tags.TryGetValue("healthcheckplus.anomaly.reason", out var reason) &&
                Equals(reason, "logging_sink_failed"));
        }

        [Fact]
        public void Update_ShouldNotThrow_WhenNoExecutionIsMarkedRunning_AndLoggingThrows()
        {
            var cache = new CacheHealthCheckPlus(new ThrowingLogger());
            cache.InitCache(["Test1"]);

            var result = new HealthCheckResult(HealthStatus.Healthy);
            // Running was never set true for "Test1", so this hits the second dropped branch.
            cache.Update("Test1", HealthCheckTrigger.UrlRequest, result, DateTime.UtcNow, TimeSpan.Zero);
        }

        // Same defect, in SwithState()'s own drop branch (a manual override arriving while a
        // scheduled execution is already in flight for the same check).
        [Fact]
        public void SwithState_ShouldNotThrow_WhenOverrideIsDroppedBecauseAlreadyRunning_AndLoggingThrows()
        {
            var cache = new CacheHealthCheckPlus(new ThrowingLogger());
            cache.InitCache(["Test1"]);
            cache.Running("Test1", true);

            cache.SwithState("Test1", HealthStatus.Unhealthy);

            // The override must actually have been dropped (not silently applied), confirming
            // this test exercised the intended branch rather than the happy path.
            Assert.Equal(HealthStatus.Healthy, cache.FullStatus("Test1").LastResult.Status);
        }

        [Fact]
        public void FullStatus_ShouldReturnItemCacheHealth()
        {
            var names = new List<string> { "Test1" };

            _cacheHealthCheckPlus.InitCache(names);

            var status = _cacheHealthCheckPlus.FullStatus("Test1");

            Assert.NotNull(status);
        }

        [Fact]
        public void SwitchToUnhealthy_ShouldSwitchToUnhealthy()
        {
            var names = new List<string> { "Test1" };

            _cacheHealthCheckPlus.InitCache(names);
            _cacheHealthCheckPlus.SwitchToUnhealthy("Test1");

            Assert.Equal(HealthStatus.Unhealthy, _cacheHealthCheckPlus.FullStatus("Test1").LastResult.Status);
        }

        [Fact]
        public void SwitchToDegraded_ShouldSwitchToDegraded()
        {
            var names = new List<string> { "Test1" };

            _cacheHealthCheckPlus.InitCache(names);
            _cacheHealthCheckPlus.SwitchToDegraded("Test1");

            Assert.Equal(HealthStatus.Degraded, _cacheHealthCheckPlus.FullStatus("Test1").LastResult.Status);
        }

        [Fact]
        public void TryGetNotHealthy_ShouldReturnNotHealthyStatuses()
        {
            var names = new List<string> { "Test1" };

            _cacheHealthCheckPlus.InitCache(names);
            _cacheHealthCheckPlus.SwitchToUnhealthy("Test1");

            var result = _cacheHealthCheckPlus.TryGetNotHealthy(out var notHealthy);

            Assert.True(result);
            Assert.Single(notHealthy);
        }

        [Fact]
        public void TryGetHealthy_ShouldReturnHealthyStatuses()
        {
            var names = new List<string> { "Test1" };

            _cacheHealthCheckPlus.InitCache(names);

            var result = _cacheHealthCheckPlus.TryGetHealthy(out var healthy);

            Assert.True(result);
            Assert.Single(healthy);
        }

        [Fact]
        public void TryGetDegraded_ShouldReturnDegradedStatuses()
        {
            var names = new List<string> { "Test1" };

            _cacheHealthCheckPlus.InitCache(names);
            _cacheHealthCheckPlus.SwitchToDegraded("Test1");

            var result = _cacheHealthCheckPlus.TryGetDegraded(out var degraded);

            Assert.True(result);
            Assert.Single(degraded);
        }

        [Fact]
        public void TryGetUnhealthy_ShouldReturnUnhealthyStatuses()
        {
            var names = new List<string> { "Test1" };

            _cacheHealthCheckPlus.InitCache(names);
            _cacheHealthCheckPlus.SwitchToUnhealthy("Test1");

            var result = _cacheHealthCheckPlus.TryGetUnhealthy(out var unhealthy);

            Assert.True(result);
            Assert.Single(unhealthy);
        }

        [Fact]
        public void ConvertToPlus_ShouldConvertToIDataHealthPlus()
        {
            var names = new List<string> { "Test1" };

            _cacheHealthCheckPlus.InitCache(names);
            var report = _cacheHealthCheckPlus.CreateReport();

            var result = _cacheHealthCheckPlus.ConvertToPlus(report);

            Assert.Single(result);
        }

        [Fact]
        public void TryBeginRun_ShouldReturnFalse_WhenPredicateSaysNotDue()
        {
            _cacheHealthCheckPlus.InitCache(["Test1"]);

            var began = _cacheHealthCheckPlus.TryBeginRun("Test1", _ => false);

            Assert.False(began);
            Assert.False(_cacheHealthCheckPlus.FullStatus("Test1").Running);
        }

        [Fact]
        public void TryBeginRun_ShouldMarkRunning_AndReturnTrue_WhenDue()
        {
            _cacheHealthCheckPlus.InitCache(["Test1"]);

            var began = _cacheHealthCheckPlus.TryBeginRun("Test1", _ => true);

            Assert.True(began);
            Assert.True(_cacheHealthCheckPlus.FullStatus("Test1").Running);
        }

        [Fact]
        public void TryBeginRun_ShouldReturnFalse_WhenAlreadyRunning()
        {
            _cacheHealthCheckPlus.InitCache(["Test1"]);
            _cacheHealthCheckPlus.Running("Test1", true);

            var began = _cacheHealthCheckPlus.TryBeginRun("Test1", _ => true);

            Assert.False(began);
        }

        // Regression test for a scheduling race: if the check-then-mark were two separate steps
        // (as it is in DefaultHealthCheckServicePlus.ScheduleIfDue without this method), two
        // concurrent callers could both observe "not running, due" and both proceed. TryBeginRun
        // makes the two one atomic operation; this drives many concurrent calls at the same instant
        // (via Barrier) to prove only one of them can ever win for the same key.
        [Fact]
        public void TryBeginRun_ShouldAllowOnlyOneCaller_WhenCalledConcurrently()
        {
            _cacheHealthCheckPlus.InitCache(["Test1"]);

            const int concurrency = 50;
            using var barrier = new Barrier(concurrency);
            var winners = 0;

            Parallel.For(0, concurrency, _ =>
            {
                barrier.SignalAndWait();
                if (_cacheHealthCheckPlus.TryBeginRun("Test1", _ => true))
                {
                    Interlocked.Increment(ref winners);
                }
            });

            Assert.Equal(1, winners);
        }

        // Regression test for a real, empirically-reproduced concurrency bug: LastResult/
        // DateRef/Duration/Origin used to be four independent mutable properties on
        // ItemCacheHealth, written one assignment at a time inside Update() with no
        // synchronization. A reader landing between two of those writes could observe an
        // inconsistent mix - e.g. the new Status paired with the old Description, or an
        // Exception that doesn't match either. Fixed by bundling all four into one immutable
        // CheckResultSnapshot, swapped with a single reference assignment (never torn on .NET),
        // with every multi-field read site (CreateReport, TryGetByStatus, ConvertToPlus,
        // DefaultHealthCheckServicePlus's own report loop) updated to read that snapshot once
        // instead of the four properties separately. This test alternates Update() between two
        // fully-distinct results on a background thread while continuously reading CreateReport()
        // on the foreground thread, and asserts every observed entry exactly matches one of the
        // two known-good combinations - never a mix of the two.
        [Fact]
        public async Task CreateReport_ShouldNeverExposeATornCombinationOfResultFields_UnderConcurrentUpdates()
        {
            _cacheHealthCheckPlus.InitCache(["Test1"]);

            var resultA = new HealthCheckResult(HealthStatus.Healthy, "healthy-desc", null, null);
            var resultB = new HealthCheckResult(HealthStatus.Unhealthy, "unhealthy-desc", new InvalidOperationException("simulated failure"), null);

            using var cts = new CancellationTokenSource();
            using var firstUpdateDone = new ManualResetEventSlim(false);
            var writer = Task.Run(() =>
            {
                var useA = true;
                while (!cts.IsCancellationRequested)
                {
                    _cacheHealthCheckPlus.Running("Test1", true);
                    _cacheHealthCheckPlus.Update("Test1", HealthCheckTrigger.Background, useA ? resultA : resultB, DateTime.UtcNow, TimeSpan.FromTicks(useA ? 1 : 2));
                    firstUpdateDone.Set();
                    useA = !useA;
                }
            }, TestContext.Current.CancellationToken);

            // Wait for the writer's first real Update() before measuring - otherwise, under
            // heavy contention (e.g. this test running alongside the rest of the suite), the
            // reader loop could spend its first iterations racing InitCache's own seed value
            // (Healthy, null description) before the writer ever runs, which matches neither
            // resultA nor resultB and would be a false positive, not a torn read.
            Assert.True(firstUpdateDone.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken), "The writer never completed its first Update().");

            var tornReads = 0;
            for (var i = 0; i < 500_000; i++)
            {
                var entry = _cacheHealthCheckPlus.CreateReport().Entries["Test1"];

                var matchesA = entry.Status == resultA.Status && entry.Description == resultA.Description && entry.Exception == resultA.Exception;
                var matchesB = entry.Status == resultB.Status && entry.Description == resultB.Description && entry.Exception == resultB.Exception;

                if (!matchesA && !matchesB)
                {
                    tornReads++;
                }
            }

            cts.Cancel();
            await writer;

            Assert.Equal(0, tornReads);
        }

        // Invariant check for ReleaseRunning/TryBeginRun/Update under real contention: the
        // Running flag and the cached result must never both be true (wedged Running) and
        // regressed (a lower sequence number than the highest one actually applied) at the end
        // of a burst of concurrent TryBeginRun-gated attempts. This does not (and, given how
        // narrow the fixed race actually was, could not reliably within test-suite time) force
        // the specific interleaving the pre-fix code was vulnerable to - reading Running=false
        // and then rewriting the snapshot were two adjacent statements with no safepoint between
        // them, so reproducing the exact clobber needs an OS preemption landing in that
        // sub-instruction gap, not something a stress loop can dependably trigger without adding
        // an artificial delay to production code. The fix itself is justified independently:
        // Running is a plain, non-volatile bool, so writing it with no synchronization while
        // TryBeginRun reads-and-writes it under a lock is a data race under the CLR memory model
        // regardless of how rarely it manifests. This test stays as a cheap sanity net that the
        // documented invariants (no wedged Running, no regressed result) still hold under load.
        [Fact]
        public void ReleaseRunning_ShouldNeverWedgeRunning_OrLoseTheHighestAppliedResult_UnderConcurrentTryBeginRun()
        {
            _cacheHealthCheckPlus.InitCache(["Test1"]);

            var workerThreadCount = Environment.ProcessorCount * 2;
            var releaserThreadCount = Environment.ProcessorCount * 2;
            var duration = TimeSpan.FromSeconds(1);
            var seqCounter = 0;
            var maxSeqApplied = 0;
            using var stop = new CancellationTokenSource(duration);

            void WorkerLoop()
            {
                while (!stop.IsCancellationRequested)
                {
                    if (_cacheHealthCheckPlus.TryBeginRun("Test1", _ => true))
                    {
                        var seq = Interlocked.Increment(ref seqCounter);
                        _cacheHealthCheckPlus.Update("Test1", HealthCheckTrigger.Background,
                            new HealthCheckResult(HealthStatus.Healthy, $"seq:{seq}"), DateTime.UtcNow, TimeSpan.Zero);
                        InterlockedMax(ref maxSeqApplied, seq);
                    }
                }
            }

            void ReleaserLoop()
            {
                while (!stop.IsCancellationRequested)
                {
                    if (_cacheHealthCheckPlus.TryBeginRun("Test1", _ => true))
                    {
                        _cacheHealthCheckPlus.ReleaseRunning("Test1", DateTime.UtcNow);
                    }
                }
            }

            var threads = new List<Thread>();
            for (var i = 0; i < workerThreadCount; i++)
            {
                threads.Add(new Thread(WorkerLoop));
            }
            for (var i = 0; i < releaserThreadCount; i++)
            {
                threads.Add(new Thread(ReleaserLoop));
            }
            threads.ForEach(t => t.Start());
            threads.ForEach(t => t.Join());

            var finalItem = _cacheHealthCheckPlus.FullStatus("Test1");
            var finalDescription = finalItem.LastResult.Description;
            var finalSeq = finalDescription is not null && finalDescription.StartsWith("seq:", StringComparison.Ordinal)
                ? int.Parse(finalDescription["seq:".Length..])
                : 0;

            Assert.False(finalItem.Running);
            Assert.Equal(maxSeqApplied, finalSeq);
        }

        private static void InterlockedMax(ref int target, int value)
        {
            int current;
            do
            {
                current = target;
                if (value <= current)
                {
                    return;
                }
            } while (Interlocked.CompareExchange(ref target, value, current) != current);
        }
    }
}
