// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using System.Text.Json;
using HealthCheckPlus.Abstractions;
using HealthCheckPlus.Internal;
using HealthCheckPlus.options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlusTests
{
    // Regression coverage for the WriteXxx response-writer methods, now sharing one WriteReport
    // helper instead of six independent copies of the same ContentType+serialize+write tail.
    public class HealthCheckPlusOptionsTests
    {
        private static DefaultHttpContext CreateContext()
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            return context;
        }

        private static HealthReport CreateReport()
        {
            var entries = new Dictionary<string, HealthReportEntry>
            {
                ["Test1"] = new HealthReportEntry(HealthStatus.Healthy, "ok", TimeSpan.FromMilliseconds(5), null, null)
            };
            return new HealthReport(entries, TimeSpan.FromMilliseconds(5));
        }

        private static IStateHealthChecksPlus CreateStateCache()
        {
            var cache = new CacheHealthCheckPlus();
            cache.InitCache(["Test1"]);
            cache.Update("Test1", HealthCheckTrigger.Background, new HealthCheckResult(HealthStatus.Healthy, "ok"), DateTime.UtcNow, TimeSpan.FromMilliseconds(5));
            return cache;
        }

        private static async Task<string> ReadBody(DefaultHttpContext context)
        {
            context.Response.Body.Position = 0;
            using var reader = new StreamReader(context.Response.Body);
            return await reader.ReadToEndAsync();
        }

        // Regression test: WriteShortDetails used to set a bare "application/json" ContentType
        // while every other WriteXxx overload set "application/json; charset=utf-8" - a
        // duplication-caused drift, not a deliberate difference, only found while auditing the
        // six methods for duplicated logic. All six must now agree, since they share one
        // WriteReport helper.
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task WriteShortDetails_ShouldSetContentTypeWithCharset(bool usePlusOverload)
        {
            var context = CreateContext();

            if (usePlusOverload)
            {
                await HealthCheckPlusOptions.WriteShortDetailsPlus(context, CreateReport(), CreateStateCache());
            }
            else
            {
                await HealthCheckPlusOptions.WriteShortDetails(context, CreateReport());
            }

            Assert.Equal("application/json; charset=utf-8", context.Response.ContentType);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task WriteDetailsWithoutException_ShouldSetContentTypeWithCharset(bool usePlusOverload)
        {
            var context = CreateContext();

            if (usePlusOverload)
            {
                await HealthCheckPlusOptions.WriteDetailsWithoutExceptionPlus(context, CreateReport(), CreateStateCache());
            }
            else
            {
                await HealthCheckPlusOptions.WriteDetailsWithoutException(context, CreateReport());
            }

            Assert.Equal("application/json; charset=utf-8", context.Response.ContentType);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task WriteDetailsWithException_ShouldSetContentTypeWithCharset(bool usePlusOverload)
        {
            var context = CreateContext();

            if (usePlusOverload)
            {
                await HealthCheckPlusOptions.WriteDetailsWithExceptionPlus(context, CreateReport(), CreateStateCache());
            }
            else
            {
                await HealthCheckPlusOptions.WriteDetailsWithException(context, CreateReport());
            }

            Assert.Equal("application/json; charset=utf-8", context.Response.ContentType);
        }

        // Guards the field projection itself, not just the shared ContentType tail - a mistake
        // made while routing each method's own entries projection through the new WriteReport
        // helper would show up here.
        [Fact]
        public async Task WriteDetailsWithoutException_ShouldSerializeStatusAndEntryFields()
        {
            var context = CreateContext();

            await HealthCheckPlusOptions.WriteDetailsWithoutException(context, CreateReport());

            var body = await ReadBody(context);
            using var json = JsonDocument.Parse(body);

            Assert.Equal("Healthy", json.RootElement.GetProperty("status").GetString());
            var entry = json.RootElement.GetProperty("entries")[0];
            Assert.Equal("Test1", entry.GetProperty("name").GetString());
            Assert.Equal("ok", entry.GetProperty("description").GetString());
        }
    }
}
