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

        // Regression coverage for the "at least one second, never infinite" rule now centralized
        // in PeriodValidation and shared by every property below - previously each setter
        // reimplemented this rule independently with no test at all exercising its exception
        // paths, so a slip introduced while extracting the shared helper would have gone
        // unnoticed by the existing suite.
        public static IEnumerable<object[]> PeriodPropertiesRejectingBelowOneSecondAndInfinite()
        {
            yield return [(Action<HealthCheckPlusBackGroundOptions, TimeSpan>)((o, v) => o.HealthyPeriod = v)];
            yield return [(Action<HealthCheckPlusBackGroundOptions, TimeSpan>)((o, v) => o.DegradedPeriod = v)];
            yield return [(Action<HealthCheckPlusBackGroundOptions, TimeSpan>)((o, v) => o.UnhealthyPeriod = v)];
            yield return [(Action<HealthCheckPlusBackGroundOptions, TimeSpan>)((o, v) => o.Idle = v)];
            yield return [(Action<HealthCheckPlusBackGroundOptions, TimeSpan>)((o, v) => o.AllStatusPeriod(v))];
        }

        [Theory]
        [MemberData(nameof(PeriodPropertiesRejectingBelowOneSecondAndInfinite))]
        public void PeriodProperty_ShouldRejectValueBelowOneSecond(Action<HealthCheckPlusBackGroundOptions, TimeSpan> setValue)
        {
            var options = new HealthCheckPlusBackGroundOptions();

            Assert.Throws<ArgumentException>(() => setValue(options, TimeSpan.FromMilliseconds(500)));
        }

        [Theory]
        [MemberData(nameof(PeriodPropertiesRejectingBelowOneSecondAndInfinite))]
        public void PeriodProperty_ShouldRejectInfiniteTimeSpan(Action<HealthCheckPlusBackGroundOptions, TimeSpan> setValue)
        {
            var options = new HealthCheckPlusBackGroundOptions();

            Assert.Throws<ArgumentException>(() => setValue(options, System.Threading.Timeout.InfiniteTimeSpan));
        }

        [Theory]
        [MemberData(nameof(PeriodPropertiesRejectingBelowOneSecondAndInfinite))]
        public void PeriodProperty_ShouldAcceptExactlyOneSecond(Action<HealthCheckPlusBackGroundOptions, TimeSpan> setValue)
        {
            var options = new HealthCheckPlusBackGroundOptions();

            setValue(options, TimeSpan.FromSeconds(1));
            // No exception thrown is the assertion; nothing further to observe since each
            // delegate targets a different property.
        }

        // AllStatusPeriod's own contract (distinct from the single-property setters above): it
        // fans one accepted value out to all three status-specific fields.
        [Fact]
        public void AllStatusPeriod_ShouldSetHealthyDegradedAndUnhealthyPeriods()
        {
            var options = new HealthCheckPlusBackGroundOptions();

            options.AllStatusPeriod(TimeSpan.FromSeconds(42));

            Assert.Equal(TimeSpan.FromSeconds(42), options.HealthyPeriod);
            Assert.Equal(TimeSpan.FromSeconds(42), options.DegradedPeriod);
            Assert.Equal(TimeSpan.FromSeconds(42), options.UnhealthyPeriod);
        }

        // Delay's contract is deliberately different from the properties above: no one-second
        // minimum, and sub-second/zero values stay valid - unlike them, it is a one-shot startup
        // delay, not a recurring period. It still rejects Infinite and negative values.
        [Fact]
        public void Delay_ShouldRejectInfiniteTimeSpan()
        {
            var options = new HealthCheckPlusBackGroundOptions();

            var ex = Assert.Throws<ArgumentException>(() => options.Delay = System.Threading.Timeout.InfiniteTimeSpan);
            Assert.Contains("must not be infinite", ex.Message);
        }

        // Regression test: Delay only validated "not infinite", so a negative-but-finite value
        // (e.g. TimeSpan.FromSeconds(-2)) was silently accepted and later reached
        // Task.Delay(_optionsBackGround.Value.Delay, ...) in the background service, which throws
        // ArgumentOutOfRangeException and faults the background loop permanently and silently -
        // the same failure class as the already-fixed H1 bug, via a different validation gap.
        [Fact]
        public void Delay_ShouldRejectNegativeTimeSpan()
        {
            var options = new HealthCheckPlusBackGroundOptions();

            var ex = Assert.Throws<ArgumentException>(() => options.Delay = TimeSpan.FromSeconds(-2));
            Assert.Contains("must not be negative", ex.Message);
        }

        [Fact]
        public void Delay_ShouldAllowValuesBelowOneSecond()
        {
            var options = new HealthCheckPlusBackGroundOptions
            {
                Delay = TimeSpan.FromMilliseconds(1)
            };

            Assert.Equal(TimeSpan.FromMilliseconds(1), options.Delay);
        }

        [Fact]
        public void Delay_ShouldAllowZero()
        {
            var options = new HealthCheckPlusBackGroundOptions
            {
                Delay = TimeSpan.Zero
            };

            Assert.Equal(TimeSpan.Zero, options.Delay);
        }
    }
}
