using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HealthCheckPlus.WrapperMicrosoft
{
    internal partial class DefaultHealthCheckServicePlus : HealthCheckService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptions<HealthCheckServiceOptions> _options;
        public DefaultHealthCheckServicePlus(
            IServiceScopeFactory scopeFactory,
            IOptions<HealthCheckServiceOptions> options)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            // We're specifically going out of our way to do this at startup time. We want to make sure you
            // get any kind of health-check related error as early as possible. Waiting until someone
            // actually tries to **run** health checks would be real baaaaad.
            ValidateRegistrations(_options.Value.Registrations);
        }
        public override async Task<HealthReport> CheckHealthAsync(
            Func<HealthCheckRegistration, bool>? predicate,
            CancellationToken cancellationToken = default)
        {
            var registrations = _options.Value.Registrations;
            if (predicate != null)
            {
                registrations = registrations.Where(predicate).ToArray();
            }

            var totalTime = ValueStopwatchPlus.StartNew();

            var tasks = new Task<HealthReportEntry>[registrations.Count];
            var index = 0;

            foreach (var registration in registrations)
            {
                tasks[index++] = Task.Run(() => RunCheckAsync(registration, cancellationToken), cancellationToken);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            index = 0;
            var entries = new Dictionary<string, HealthReportEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var registration in registrations)
            {
                entries[registration.Name] = tasks[index++].Result;
            }

            var totalElapsedTime = totalTime.GetElapsedTime();
            var report = new HealthReport(entries, totalElapsedTime);
            return report;
        }

        private async Task<HealthReportEntry> RunCheckAsync(HealthCheckRegistration registration, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var scope = _scopeFactory.CreateScope();
            var healthCheck = registration.Factory(scope.ServiceProvider);

            // If the health check does things like make Database queries using EF or backend HTTP calls,
            // it may be valuable to know that logs it generates are part of a health check. So we start a scope.
            var stopwatch = ValueStopwatchPlus.StartNew();
            var context = new HealthCheckContext { Registration = registration };

            HealthReportEntry entry;
            CancellationTokenSource? timeoutCancellationTokenSource = null;
            try
            {
                HealthCheckResult result;

                var checkCancellationToken = cancellationToken;
                if (registration.Timeout > TimeSpan.Zero)
                {
                    timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutCancellationTokenSource.CancelAfter(registration.Timeout);
                    checkCancellationToken = timeoutCancellationTokenSource.Token;
                }

                result = await healthCheck.CheckHealthAsync(context, checkCancellationToken).ConfigureAwait(false);

                var duration = stopwatch.GetElapsedTime();

                entry = new HealthReportEntry(
                    status: result.Status,
                    description: result.Description,
                    duration: duration,
                    exception: result.Exception,
                    data: result.Data,
                    tags: registration.Tags);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                var duration = stopwatch.GetElapsedTime();
                entry = new HealthReportEntry(
                    status: registration.FailureStatus,
                    description: "A timeout occurred while running check.",
                    duration: duration,
                    exception: ex,
                    data: null,
                    tags: registration.Tags);
            }

            // Allow cancellation to propagate if it's not a timeout.
            catch (Exception ex) when (ex as OperationCanceledException == null)
            {
                var duration = stopwatch.GetElapsedTime();
                entry = new HealthReportEntry(
                    status: registration.FailureStatus,
                    description: ex.Message,
                    duration: duration,
                    exception: ex,
                    data: null,
                    tags: registration.Tags);
            }

            finally
            {
                timeoutCancellationTokenSource?.Dispose();
            }
            return entry;
        }

        private static void ValidateRegistrations(IEnumerable<HealthCheckRegistration> registrations)
        {
            // Scan the list for duplicate names to provide a better error if there are duplicates.

            StringBuilder? builder = null;
            var distinctRegistrations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var registration in registrations)
            {
                if (!distinctRegistrations.Add(registration.Name))
                {
                    builder ??= new StringBuilder("Duplicate health checks were registered with the name(s): ");

                    builder.Append(registration.Name).Append(", ");
                }
            }

            if (builder is not null)
            {
                throw new ArgumentException(builder.ToString(0, builder.Length - 2), nameof(registrations));
            }
        }
    }

}
