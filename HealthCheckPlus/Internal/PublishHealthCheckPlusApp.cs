using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HealthCheckPlus.Internal
{
    internal sealed class PublishHealthCheckPlusApp<T> : IHealthCheck, IHealthCheckPublisher where T : Enum
    {
        private readonly IStateHealthChecksPlus _staeApp;
        private readonly IStateHealthCheckPlusInternal _staAppInternal;
        private readonly IOptions<HealthCheckPlusOptions> _defaultoptions;
        public PublishHealthCheckPlusApp(IServiceProvider serviceProvider)
        {
            _staeApp = serviceProvider.GetRequiredService<IStateHealthChecksPlus>();
            _staAppInternal = (IStateHealthCheckPlusInternal)_staeApp;
            _defaultoptions = serviceProvider.GetRequiredService<IOptions<HealthCheckPlusOptions>>();
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(_staeApp.StatusApp);
        }

        public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
        {
            UpdateStatus(report);
            return Task.CompletedTask;
        }

        public void UpdateStatus(HealthReport report)
        {
            Dictionary<string, object> data = new();
            var tp = typeof(T);
            var enumValues = Enum.GetValues(tp);
            foreach (var item in enumValues)
            {
                KeyValuePair<string, HealthReportEntry> sta = report.Entries.FirstOrDefault(x => x.Key.Equals(item.ToString(), StringComparison.CurrentCultureIgnoreCase));
                if (sta.Key != null)
                {
                    _staAppInternal.UpdateStatusDep(
                        item.ToString()!,
                        new HealthCheckResult(sta.Value.Status, sta.Value.Description, sta.Value.Exception, sta.Value.Data));
                }
                data.Add(item.ToString()!, new DataResutStatus { Name = sta.Key, Status = sta.Value.Status, Error = sta.Value.Exception?.Message.ToString() } );

            }
            ReadOnlyDictionary<string, object> aux = new(data);
            _staAppInternal.StatusApp = report.Entries.Any(e => e.Value.Status != HealthStatus.Healthy)
                ? new HealthCheckResult(_defaultoptions!.Value.StatusFail, data: aux)
                : new HealthCheckResult(HealthStatus.Healthy, data: aux);
        }
    }
}
