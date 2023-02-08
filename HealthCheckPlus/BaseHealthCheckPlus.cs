using HealthCheckPlus.Internal;
using HealthCheckPlus.WrapperMicrosoft;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HealthCheckPlus
{
    /// <summary>
    /// HealthCheckPlus : Abstract class for create HealthCheck class. Inherit <see cref="IHealthCheck"/>.
    /// </summary>
    public abstract class BaseHealthCheckPlus : IHealthCheck
    {
        private readonly IStateHealthCheckPlusInternal _stateHealthCheckPlus;
        private readonly IEnumerable<IHealthCheckPlusPolicyUnhealth> _healthCheckPlusPolicyUnhealths;
        private readonly ILogger? _logger = null;
        private readonly IOptions<HealthCheckPlusOptions> _optionshc;
        /// <summary>
        /// HealthCheckPlus: Abstract class for create HealthCheck class. Inherit <see cref="IHealthCheck"/>.
        /// </summary>
        /// <param name="serviceProvider"><see cref="IServiceProvider"/></param>
        public BaseHealthCheckPlus(IServiceProvider serviceProvider)
        {
            _healthCheckPlusPolicyUnhealths = serviceProvider.GetServices<IHealthCheckPlusPolicyUnhealth>();
            _stateHealthCheckPlus = (IStateHealthCheckPlusInternal)serviceProvider.GetRequiredService<IStateHealthChecksPlus>();
            _logger = serviceProvider.GetService<ILoggerFactory>().CreateLogger("BaseHealthCheckPlus");
            _optionshc = serviceProvider.GetRequiredService<IOptions<HealthCheckPlusOptions>>();
        }

        /// <summary>
        /// HealthCheckPlus: Runs the health check, returning the status of the component being checked.        
        /// </summary>
        /// <param name="context">A context object associated with the current execution.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the health check.</param>
        /// <returns>A <see cref="Task{HealthCheckResult}"/> that completes when the health check has finished, yielding the status of the component being checked.</returns>

        public virtual async Task<HealthCheckResult> DoHealthCheck(HealthCheckContext context, CancellationToken cancellationToken)
        {
            return await Task.FromResult(HealthCheckResult.Healthy());
        }

        /// <summary>
        /// HealthCheckPlus: Not visibinble Runs the method DoHealthCheck, returning the status of the component being checked.        
        /// </summary>
        /// <param name="context">A context object associated with the current execution.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the health check.</param>
        /// <returns>A <see cref="Task{HealthCheckResult}"/> that completes when the health check has finished, yielding the status of the component being checked.</returns>
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var key = context.Registration.Name;
            var delay = _stateHealthCheckPlus.Delay(key);
            var interval = _stateHealthCheckPlus.Interval(key);
            var policy = FindPolicy(key);
            if (_stateHealthCheckPlus.LastCheck(key) == null)
            {
                _stateHealthCheckPlus.UpdateLastCheck(key, DateTime.Now.Add(delay));
            }
            if (!_stateHealthCheckPlus.IsRunning(key) && _stateHealthCheckPlus.StatusDep(key).Status == HealthStatus.Unhealthy && policy != null && policy.PolicyTime != null)
            {
                var sw = ValueStopwatchPlus.StartNew();
                var level = LogLevel.None;
                HealthCheckResult result;
                _stateHealthCheckPlus.SetIsRunning(key, true);
                try
                {
                    result = await DoHealthCheck(context, cancellationToken);
                }
                catch (Exception ex)
                {
                    result = new HealthCheckResult(
                            context.Registration.FailureStatus,
                            "error", ex);
                }
                switch (result.Status)
                {
                    case HealthStatus.Unhealthy:
                        level = _optionshc.Value.LogLevelCheckUnhealthy;
                        break;
                    case HealthStatus.Degraded:
                        level = _optionshc.Value.LogLevelCheckDegraded;
                        break;
                    case HealthStatus.Healthy:
                        level = _optionshc.Value.LogLevelCheckHealthy;
                        break;
                    default:
                        break;
                }
                var msg = $"{result.Status} for {key}. Elapsed Time: {sw.GetElapsedTime()}.";
                _logger?.Log(level, msg);
                _stateHealthCheckPlus.UpdateStatusDep(key, result);
                _stateHealthCheckPlus.UpdateLastCheck(key, DateTime.Now.Add(policy.PolicyTime.Value));
                _stateHealthCheckPlus.SetIsRunning(key, false);
            }
            else
            {
                if (!_stateHealthCheckPlus.IsRunning(key) && DateTime.Now >= _stateHealthCheckPlus.LastCheck(key))
                {
                    var sw = ValueStopwatchPlus.StartNew();
                    var level = LogLevel.None;
                    HealthCheckResult result;
                    _stateHealthCheckPlus.SetIsRunning(key, true);
                    try
                    {
                        result = await DoHealthCheck(context, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        result = new HealthCheckResult(
                                context.Registration.FailureStatus,
                                "error", ex);
                    }
                    switch (result.Status)
                    {
                        case HealthStatus.Unhealthy:
                            level = _optionshc.Value.LogLevelCheckUnhealthy;
                            break;
                        case HealthStatus.Degraded:
                            level = _optionshc.Value.LogLevelCheckDegraded;
                            break;
                        case HealthStatus.Healthy:
                            level = _optionshc.Value.LogLevelCheckHealthy;
                            break;
                        default:
                            break;
                    }
                    var msg = $"{result.Status} for {key}. Elapsed Time: {sw.GetElapsedTime()}.";
                    _logger?.Log(level, msg);
                    _stateHealthCheckPlus.UpdateStatusDep(key, result);
                    _stateHealthCheckPlus.UpdateLastCheck(key, DateTime.Now.Add(interval));
                    _stateHealthCheckPlus.SetIsRunning(key, false);
                }
            }
            return _stateHealthCheckPlus.StatusDep(key);
        }

        private IHealthCheckPlusPolicyUnhealth? FindPolicy(string name)
        {
            return _healthCheckPlusPolicyUnhealths.FirstOrDefault(x => x.PolicyNameDep == name);
        }
    }
}
