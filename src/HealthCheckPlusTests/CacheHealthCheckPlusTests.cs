// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Abstractions;
using HealthCheckPlus.Internal;
using HealthCheckPlus.options;
using Microsoft.Extensions.Diagnostics.HealthChecks;

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

            _cacheHealthCheckPlus.AddStatusName(options);

            Assert.Throws<ArgumentException>(() => _cacheHealthCheckPlus.AddStatusName(options));
        }

        [Fact]
        public void InitCache_ShouldInitializeStatusDeps()
        {
            var names = new List<string> { "Test1", "Test2" };

            _cacheHealthCheckPlus.InitCache(names);

            Assert.Equal("Test1", _cacheHealthCheckPlus.FullStatus("Test1").Name);
            Assert.Equal("Test2", _cacheHealthCheckPlus.FullStatus("Test2").Name);
        }

        [Fact]
        public void UpdateStatusName_ShouldUpdateStatusName()
        {
            var options = new HealthCheckPlusOptions
            {
                HealthCheckName = "Test",
                StatusHealthReport = report => HealthStatus.Healthy
            };

            _cacheHealthCheckPlus.AddStatusName(options);
            _cacheHealthCheckPlus.InitCache(["Test"]);
            _cacheHealthCheckPlus.UpdateStatusName();

            Assert.Equal(HealthStatus.Healthy, _cacheHealthCheckPlus.Status("Test"));
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
            _cacheHealthCheckPlus.AddStatusName(options);
            _cacheHealthCheckPlus.UpdateStatusName();

            Assert.Equal(HealthStatus.Healthy, _cacheHealthCheckPlus.Status("Named"));

            _cacheHealthCheckPlus.SwitchToUnhealthy("Test1");

            Assert.Equal(HealthStatus.Unhealthy, _cacheHealthCheckPlus.Status("Named"));
        }

        [Fact]
        public void LastReport_ShouldReturnMaxDateRef()
        {
            var names = new List<string> { "Test1", "Test2" };

            _cacheHealthCheckPlus.InitCache(names);

            Assert.NotNull(_cacheHealthCheckPlus.LastReport());
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
    }
}
