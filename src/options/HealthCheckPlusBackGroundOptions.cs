// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HealthCheckPlus.options
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
        public TimeSpan Delay
        {
            get => _delay;
            set
            {
                if (value == System.Threading.Timeout.InfiniteTimeSpan)
                {
                    throw new ArgumentException($"The {nameof(Delay)} must not be infinite.", nameof(value));
                }

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
                if (value < TimeSpan.FromSeconds(1))
                {
                    throw new ArgumentException($"The {nameof(HealthyPeriod)} must be greater than or equal to one second.", nameof(value));
                }

                if (value == System.Threading.Timeout.InfiniteTimeSpan)
                {
                    throw new ArgumentException($"The {nameof(HealthyPeriod)} must not be infinite.", nameof(value));
                }

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
                if (value < TimeSpan.FromSeconds(1))
                {
                    throw new ArgumentException($"The {nameof(DegradedPeriod)} must be greater than or equal to one second.", nameof(value));
                }

                if (value == System.Threading.Timeout.InfiniteTimeSpan)
                {
                    throw new ArgumentException($"The {nameof(DegradedPeriod)} must not be infinite.", nameof(value));
                }

                _degradedperiod = value;
            }
        }

        /// <summary>
        /// Gets or sets the period when HealthCheck's period property is null and last status is of <see cref="HealthStatus.Unhealthy"/>. The default value is
        /// 30 seconds.
        /// </summary>
        /// <remarks>
        /// The <see cref="UnhealthyPeriod"/> cannot be set to a value lower than 1 second.
        /// </remarks>
        public TimeSpan UnhealthyPeriod
        {
            get => _unhealthyperiod;
            set
            {
                if (value < TimeSpan.FromSeconds(1))
                {
                    throw new ArgumentException($"The {nameof(UnhealthyPeriod)} must be greater than or equal to one second.", nameof(value));
                }

                if (value == System.Threading.Timeout.InfiniteTimeSpan)
                {
                    throw new ArgumentException($"The {nameof(UnhealthyPeriod)} must not be infinite.", nameof(value));
                }

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
                if (value < TimeSpan.FromSeconds(1))
                {
                    throw new ArgumentException($"The {nameof(Idle)} must be greater than or equal to one second.", nameof(value));
                }

                if (value == System.Threading.Timeout.InfiniteTimeSpan)
                {
                    throw new ArgumentException($"The {nameof(Idle)} must not be infinite.", nameof(value));
                }

                _idle = value;
            }
        }

        /// <summary>
        /// Gets or sets the timeout for executing the health checks an all HealthCheckPlus background service.
        /// Use <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> to execute with no timeout.
        /// The default value is 30 seconds.
        /// </summary>
        /// <remarks>
        /// The <see cref="Timeout"/> cannot be set to a value lower than 1 second.
        /// </remarks>
        public TimeSpan Timeout
        {
            get => _timeout;
            set
            {
                if (value < TimeSpan.FromSeconds(1))
                {
                    throw new ArgumentException($"The {nameof(Timeout)} must be greater than or equal to one second.", nameof(value));
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
            if (value < TimeSpan.FromSeconds(1))
            {
                throw new ArgumentException($"The {nameof(AllStatusPeriod)} must be greater than or equal to one second.", nameof(value));
            }

            if (value == System.Threading.Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentException($"The {nameof(AllStatusPeriod)} must not be infinite.", nameof(value));
            }
            _healthyperiod = value;
            _degradedperiod = value;
            _unhealthyperiod = value;
        }

        /// <summary>
        /// Gets or sets a predicate that is used to filter the set of health checks executed.
        /// </summary>
        /// <remarks>
        /// If <see cref="Predicate"/> is <c>null</c>, will run all
        /// registered health checks - this is the default behavior. To run a subset of health checks,
        /// provide a function that filters the set of checks.
        /// </remarks>
        public Func<HealthCheckRegistration, bool>? Predicate { get; set; }
    }
}
