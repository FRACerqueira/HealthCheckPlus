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
    }
}
