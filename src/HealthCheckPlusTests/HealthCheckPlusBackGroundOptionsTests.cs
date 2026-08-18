// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.options;

namespace HealthCheckPlusTests
{
    public class HealthCheckPlusBackGroundOptionsTests
    {
        // Regression test: the XML doc on Timeout explicitly documents Timeout.InfiniteTimeSpan as
        // the way to disable the per-cycle timeout, but the setter's "< 1 second" guard rejected it
        // too (InfiniteTimeSpan is -1ms) - every attempt to use the documented escape hatch threw.
        [Fact]
        public void Timeout_ShouldAllowInfiniteTimeSpan()
        {
            var options = new HealthCheckPlusBackGroundOptions
            {
                Timeout = System.Threading.Timeout.InfiniteTimeSpan
            };

            Assert.Equal(System.Threading.Timeout.InfiniteTimeSpan, options.Timeout);
        }

        [Fact]
        public void Timeout_ShouldStillRejectValuesBelowOneSecond()
        {
            var options = new HealthCheckPlusBackGroundOptions();

            Assert.Throws<ArgumentException>(() => options.Timeout = TimeSpan.FromMilliseconds(500));
        }
    }
}
