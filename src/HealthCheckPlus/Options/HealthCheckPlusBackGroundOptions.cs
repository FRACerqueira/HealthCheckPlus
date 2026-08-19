// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Internal;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlus.Options
{
    /// <summary>
    /// Options for the HealthCheckPlus background service instance.
    /// </summary>
    public class HealthCheckPlusBackGroundOptions
    {
        private TimeSpan _delay;
        private TimeSpan _healthyperiod;
        private TimeSpan _degradedperiod;
        private TimeSpan _unhealthyperiod;
        private TimeSpan _timeout;
        private TimeSpan _idle;

        /// <summary>
        /// Creates a new instance of <see cref="HealthCheckPlusBackGroundOptions"/>.
        /// <br>Default Values:</br>
        /// <br>Delay = 5 seconds.</br>
        /// <br>HealthyPeriod = 30 seconds.</br>
        /// <br>DegradedPeriod = 30 seconds.</br>
        /// <br>UnhealthyPeriod = 30 seconds.</br>
        /// <br>Timeout = 30 seconds.</br>
        /// <br>Idle = 1 second.</br>
        /// <br>Predicate = All HealthCheck.</br>
        /// </summary>
        public HealthCheckPlusBackGroundOptions()
        {
            _delay = TimeSpan.FromSeconds(5);
            _idle = TimeSpan.FromSeconds(1);
            _healthyperiod = TimeSpan.FromSeconds(30);
            _degradedperiod = TimeSpan.FromSeconds(30);
            _unhealthyperiod = TimeSpan.FromSeconds(30);
            _timeout = TimeSpan.FromSeconds(30);
            Predicate = (_)=> true;
        }

        /// <summary>
        /// Gets or sets the initial delay applied after the application starts before executing
        /// HealthCheckPlus background service. The delay is applied once at startup, and does
        /// not apply to subsequent iterations. The default value is 5 seconds.
        /// </summary>
        /// <remarks>
        /// The <see cref="Delay"/> cannot be set to a negative value. Unlike the period
        /// properties, sub-second and zero values are valid here - this is a one-shot startup
        /// delay, not a recurring poll interval.
        /// </remarks>
        public TimeSpan Delay
        {
            get => _delay;
            set
            {
                PeriodValidation.EnsureNotInfinite(value, nameof(Delay), nameof(value));
                PeriodValidation.EnsureNonNegative(value, nameof(Delay), nameof(value));
                _delay = value;
            }
        }

        /// <summary>
        /// Gets or sets the period when HealthCheck's period property is null and last status is of <see cref="HealthStatus.Healthy"/>. The default value is
        /// 30 seconds.
        /// </summary>
        /// <remarks>
        /// The <see cref="HealthyPeriod"/> cannot be set to a value lower than 1 second.
        /// </remarks>
        public TimeSpan HealthyPeriod
        {
            get => _healthyperiod;
            set
            {
                PeriodValidation.EnsureAtLeastOneSecond(value, nameof(HealthyPeriod), nameof(value));
                PeriodValidation.EnsureNotInfinite(value, nameof(HealthyPeriod), nameof(value));
                _healthyperiod = value;
            }
        }


        /// <summary>
        /// Gets or sets the period when HealthCheck's period property is null and last status is of <see cref="HealthStatus.Degraded"/>. The default value is
        /// 30 seconds.
        /// </summary>
        /// <remarks>
        /// The <see cref="DegradedPeriod"/> cannot be set to a value lower than 1 second.
        /// </remarks>
        public TimeSpan DegradedPeriod
        {
            get => _degradedperiod;
            set
            {
                PeriodValidation.EnsureAtLeastOneSecond(value, nameof(DegradedPeriod), nameof(value));
                PeriodValidation.EnsureNotInfinite(value, nameof(DegradedPeriod), nameof(value));
                _degradedperiod = value;
            }
        }

        /// <summary>
        /// Gets or sets the period when HealthCheck's period property is null and last status is of <see cref="HealthStatus.Unhealthy"/>. 
        /// The default value is 30 seconds.
        /// </summary>
        /// <remarks>
        /// The <see cref="UnhealthyPeriod"/> cannot be set to a value lower than 1 second.
        /// </remarks>
        public TimeSpan UnhealthyPeriod
        {
            get => _unhealthyperiod;
            set
            {
                PeriodValidation.EnsureAtLeastOneSecond(value, nameof(UnhealthyPeriod), nameof(value));
                PeriodValidation.EnsureNotInfinite(value, nameof(UnhealthyPeriod), nameof(value));
                _unhealthyperiod = value;
            }
        }

        /// <summary>
        /// Gets or sets the idle after try execute HealthChecks on background service. The default value is
        /// 1 seconds.
        /// </summary>
        /// <remarks>
        /// The <see cref="Idle"/> cannot be set to a value lower than 1 second.
        /// </remarks>
        public TimeSpan Idle
        {
            get => _idle;
            set
            {
                PeriodValidation.EnsureAtLeastOneSecond(value, nameof(Idle), nameof(value));
                PeriodValidation.EnsureNotInfinite(value, nameof(Idle), nameof(value));
                _idle = value;
            }
        }

        /// <summary>
        /// Gets or sets the timeout for a single background cycle - it bounds both running the due
        /// health checks and, separately, dispatching that cycle's publishers, so neither a slow
        /// check nor a publisher with no timeout of its own can block the background service
        /// indefinitely.
        /// Use <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> to execute with no timeout.
        /// The default value is 30 seconds.
        /// </summary>
        /// <remarks>
        /// The <see cref="Timeout"/> cannot be set to a value lower than 1 second, except for
        /// <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> itself, which disables the
        /// timeout entirely.
        /// </remarks>
        public TimeSpan Timeout
        {
            get => _timeout;
            set
            {
                if (value != System.Threading.Timeout.InfiniteTimeSpan && value < TimeSpan.FromSeconds(1))
                {
                    throw new ArgumentException($"The {nameof(Timeout)} must be greater than or equal to one second, or Timeout.InfiniteTimeSpan for no timeout.", nameof(value));
                }
                _timeout = value;
            }
        }

        /// <summary>
        /// Sets the all periods when HealthCheck's period property is null. See : <see cref="HealthyPeriod"/>, <see cref="DegradedPeriod"/>, <see cref="UnhealthyPeriod"/>. The default value is
        /// 30 seconds.
        /// </summary>
        /// <remarks>
        /// The <see cref="AllStatusPeriod"/> cannot be set to a value lower than 1 second.
        /// </remarks>
        public void AllStatusPeriod(TimeSpan value)
        {
            PeriodValidation.EnsureAtLeastOneSecond(value, nameof(AllStatusPeriod), nameof(value));
            PeriodValidation.EnsureNotInfinite(value, nameof(AllStatusPeriod), nameof(value));
            _healthyperiod = value;
            _degradedperiod = value;
            _unhealthyperiod = value;
        }

        /// <summary>
        /// Gets or sets a predicate that is used to filter the set of health checks executed.
        /// </summary>
        /// <remarks>
        /// Defaults to a predicate that matches every registered health check. Explicitly setting
        /// <see cref="Predicate"/> to <c>null</c> is also treated as "run every check" everywhere
        /// it's consulted. To run a subset of health checks, provide a function that filters the
        /// set of checks.
        /// </remarks>
        public Func<HealthCheckRegistration, bool>? Predicate { get; set; }

        /// <summary>
        /// Gets or sets the usage of publishers registered 
        /// with the <see cref="IHealthCheckPublisher"/> interface      
        /// using <see cref="PublishingOptions"/>. 
        /// <br>Default values:</br>
        /// <br>Enabled = false</br>
        /// <br>WhenReportChange = true</br>
        /// <br>AfterIdleCount = 1</br>
        /// </summary>
        public PublishingOptions Publishing { get; set; } = new(false);

    }
}
