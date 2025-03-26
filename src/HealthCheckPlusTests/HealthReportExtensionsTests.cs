// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlusTests
{
    public class HealthReportExtensionsTests
    {
        private static HealthReport CreateHealthReport(Dictionary<string, HealthReportEntry> entries)
        {
            return new HealthReport(entries, TimeSpan.Zero);
        }

        [Fact]
        public void StatusResult_StringKey_Found()
        {
            var entries = new Dictionary<string, HealthReportEntry>
            {
                { "test", new HealthReportEntry(HealthStatus.Healthy, "Healthy", TimeSpan.Zero, null, null) }
            };
            var report = CreateHealthReport(entries);

            var result = report.StatusResult("test");

            Assert.Equal(HealthStatus.Healthy, result);
        }

        [Fact]
        public void StatusResult_StringKey_NotFound()
        {
            var entries = new Dictionary<string, HealthReportEntry>();
            var report = CreateHealthReport(entries);

            var result = report.StatusResult("test");

            Assert.Equal(HealthStatus.Unhealthy, result);
        }

        [Fact]
        public void StatusResult_EnumKey_Found()
        {
            var entries = new Dictionary<string, HealthReportEntry>
            {
                { "TestEnum", new HealthReportEntry(HealthStatus.Healthy, "Healthy", TimeSpan.Zero, null, null) }
            };
            var report = CreateHealthReport(entries);

            var result = report.StatusResult(TestEnum.TestEnum);

            Assert.Equal(HealthStatus.Healthy, result);
        }

        [Fact]
        public void StatusResult_EnumKey_NotFound()
        {
            var entries = new Dictionary<string, HealthReportEntry>();
            var report = CreateHealthReport(entries);

            var result = report.StatusResult(TestEnum.TestEnum);

            Assert.Equal(HealthStatus.Unhealthy, result);
        }

        [Fact]
        public void TryGetNotHealthy_Found()
        {
            var entries = new Dictionary<string, HealthReportEntry>
            {
                { "test", new HealthReportEntry(HealthStatus.Unhealthy, "Unhealthy", TimeSpan.Zero, null, null) }
            };
            var report = CreateHealthReport(entries);

            var result = report.TryGetNotHealthy(out var notHealthyEntries);

            Assert.True(result);
            Assert.Single(notHealthyEntries);
            Assert.Equal(HealthStatus.Unhealthy, notHealthyEntries["test"].Status);
        }

        [Fact]
        public void TryGetNotHealthy_NotFound()
        {
            var entries = new Dictionary<string, HealthReportEntry>
            {
                { "test", new HealthReportEntry(HealthStatus.Healthy, "Healthy", TimeSpan.Zero, null, null) }
            };
            var report = CreateHealthReport(entries);

            var result = report.TryGetNotHealthy(out var notHealthyEntries);

            Assert.False(result);
            Assert.Empty(notHealthyEntries);
        }

        [Fact]
        public void TryGetHealthy_Found()
        {
            var entries = new Dictionary<string, HealthReportEntry>
            {
                { "test", new HealthReportEntry(HealthStatus.Healthy, "Healthy", TimeSpan.Zero, null, null) }
            };
            var report = CreateHealthReport(entries);

            var result = report.TryGetHealthy(out var healthyEntries);

            Assert.True(result);
            Assert.Single(healthyEntries);
            Assert.Equal(HealthStatus.Healthy, healthyEntries["test"].Status);
        }

        [Fact]
        public void TryGetHealthy_NotFound()
        {
            var entries = new Dictionary<string, HealthReportEntry>
            {
                { "test", new HealthReportEntry(HealthStatus.Unhealthy, "Unhealthy", TimeSpan.Zero, null, null) }
            };
            var report = CreateHealthReport(entries);

            var result = report.TryGetHealthy(out var healthyEntries);

            Assert.False(result);
            Assert.Empty(healthyEntries);
        }

        [Fact]
        public void TryGetDegraded_Found()
        {
            var entries = new Dictionary<string, HealthReportEntry>
            {
                { "test", new HealthReportEntry(HealthStatus.Degraded, "Degraded", TimeSpan.Zero, null, null) }
            };
            var report = CreateHealthReport(entries);

            var result = report.TryGetDegraded(out var degradedEntries);

            Assert.True(result);
            Assert.Single(degradedEntries);
            Assert.Equal(HealthStatus.Degraded, degradedEntries["test"].Status);
        }

        [Fact]
        public void TryGetDegraded_NotFound()
        {
            var entries = new Dictionary<string, HealthReportEntry>
            {
                { "test", new HealthReportEntry(HealthStatus.Healthy, "Healthy", TimeSpan.Zero, null, null) }
            };
            var report = CreateHealthReport(entries);

            var result = report.TryGetDegraded(out var degradedEntries);

            Assert.False(result);
            Assert.Empty(degradedEntries);
        }

        [Fact]
        public void TryGetUnhealthy_Found()
        {
            var entries = new Dictionary<string, HealthReportEntry>
            {
                { "test", new HealthReportEntry(HealthStatus.Unhealthy, "Unhealthy", TimeSpan.Zero, null, null) }
            };
            var report = CreateHealthReport(entries);

            var result = report.TryGetUnhealthy(out var unhealthyEntries);

            Assert.True(result);
            Assert.Single(unhealthyEntries);
            Assert.Equal(HealthStatus.Unhealthy, unhealthyEntries["test"].Status);
        }

        [Fact]
        public void TryGetUnhealthy_NotFound()
        {
            var entries = new Dictionary<string, HealthReportEntry>
            {
                { "test", new HealthReportEntry(HealthStatus.Healthy, "Healthy", TimeSpan.Zero, null, null) }
            };
            var report = CreateHealthReport(entries);

            var result = report.TryGetUnhealthy(out var unhealthyEntries);

            Assert.False(result);
            Assert.Empty(unhealthyEntries);
        }

        private enum TestEnum
        {
            TestEnum
        }
    }
}