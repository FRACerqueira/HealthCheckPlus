// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using HealthCheckPlus.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly IEnumerable<IHealthCheckPlusUnhealthyPolicy> _healthCheckPlusUnhealthyPolicy;
        private readonly IEnumerable<IHealthCheckPlusDegradedPolicy> _healthCheckPlusDegradedPolicy;
        private readonly ILogger? _logger = null;
        private readonly IOptions<HealthCheckPlusOptions> _optionshc;
        /// <summary>
        /// HealthCheckPlus: Abstract class for create HealthCheck class. Inherit <see cref="IHealthCheck"/>.
        /// </summary>
        /// <param name="serviceProvider"><see cref="IServiceProvider"/></param>
        public BaseHealthCheckPlus(IServiceProvider serviceProvider)
        {
            _optionshc = serviceProvider.GetRequiredService<IOptions<HealthCheckPlusOptions>>();
            _stateHealthCheckPlus = (IStateHealthCheckPlusInternal)serviceProvider.GetRequiredService<IStateHealthChecksPlus>();
            _healthCheckPlusUnhealthyPolicy = serviceProvider.GetServices<IHealthCheckPlusUnhealthyPolicy>();
            _healthCheckPlusDegradedPolicy = serviceProvider.GetServices<IHealthCheckPlusDegradedPolicy>();
            _logger = serviceProvider.GetService<ILoggerFactory>().CreateLogger(_optionshc.Value.CategoryForLogger ?? "HealthChecksPlus");
        }

        /// <summary>
        /// HealthCheckPlus: NotImplemente!       
        /// </summary>
        /// <param name="context">A context object associated with the current execution.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the health check.</param>
        /// <returns>A <see cref="Task{HealthCheckResult}"/> that completes when the health check has finished, yielding the status of the component being checked.</returns>

        public virtual Task<HealthCheckResult> DoHealthCheck(HealthCheckContext context, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// HealthCheckPlus: Runs the method DoHealthCheck, returning the status of the component being checked.        
        /// </summary>
        /// <param name="context">A context object associated with the current execution.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the health check.</param>
        /// <returns>A <see cref="Task{HealthCheckResult}"/> that completes when the health check has finished, yielding the status of the component being checked.</returns>
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {

            DateTime dtexe = DateTime.Now;
            DateTime dtref;
            var key = context.Registration.Name;
            if (_stateHealthCheckPlus.IsRunning(key))
            {
                return _stateHealthCheckPlus.StatusDep(key);
            }
            var policyUnhealth = FindUnhealthyPolicy(key);
            var policyDegraded = FindDegradedPolicy(key);
            if (_stateHealthCheckPlus.LastCheck(key) == null)
            {
                if (_stateHealthCheckPlus.StatusDep(key).Status == HealthStatus.Unhealthy && policyUnhealth != null)
                {
                    dtref = _stateHealthCheckPlus.DateRegister.Add(policyUnhealth.PolicyTime);
                }
                else if (_stateHealthCheckPlus.StatusDep(key).Status == HealthStatus.Degraded && policyDegraded != null)
                {
                    dtref = _stateHealthCheckPlus.DateRegister.Add(policyDegraded.PolicyTime);
                }
                else
                {
                    dtref = _stateHealthCheckPlus.DateRegister.Add(_stateHealthCheckPlus.Delay(key));
                }
            }
            else
            {
                if (_stateHealthCheckPlus.StatusDep(key).Status == HealthStatus.Unhealthy && policyUnhealth != null)
                {
                    dtref = _stateHealthCheckPlus.LastCheck(key).Value.Add(policyUnhealth.PolicyTime);
                }
                else if (_stateHealthCheckPlus.StatusDep(key).Status == HealthStatus.Degraded && policyDegraded != null)
                {
                    dtref = _stateHealthCheckPlus.LastCheck(key).Value.Add(policyDegraded.PolicyTime);
                }
                else
                {
                    dtref = _stateHealthCheckPlus.LastCheck(key).Value.Add(_stateHealthCheckPlus.Interval(key));
                }
            }
            if (dtexe > dtref)
            {
                _stateHealthCheckPlus.SetIsRunning(key, true);
                var sw = Stopwatch.StartNew();
                HealthCheckResult result;
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
                finally
                {
                    sw.Stop();
                }
                var dtend = DateTime.Now;
                if (_optionshc.Value?.ActionForLog != null && _logger != null)
                {
                    _optionshc.Value.ActionForLog(_logger, new DataResutStatus(key,result.Description, result.Status,result.Exception,dtend,sw.Elapsed));
                }
                _stateHealthCheckPlus.UpdateStatusDep(key, result);
                _stateHealthCheckPlus.UpdateLastCheck(key, dtend, sw.Elapsed);
                _stateHealthCheckPlus.SetIsRunning(key, false);
            }
            return _stateHealthCheckPlus.StatusDep(key);
        }

        private IHealthCheckPlusUnhealthyPolicy? FindUnhealthyPolicy(string name)
        {
            return _healthCheckPlusUnhealthyPolicy.FirstOrDefault(x => x.PolicyNameDep == name);
        }
        private IHealthCheckPlusDegradedPolicy? FindDegradedPolicy(string name)
        {
            return _healthCheckPlusDegradedPolicy.FirstOrDefault(x => x.PolicyNameDep == name);
        }
    }
}
