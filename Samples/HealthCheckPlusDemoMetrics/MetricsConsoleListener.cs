using System.Diagnostics.Metrics;

namespace HealthCheckPlusDemoMetrics
{
    // Demonstrates observing HealthCheckPlus's metrics without adopting an OpenTelemetry exporter -
    // System.Diagnostics.Metrics.MeterListener is all a consumer needs to see measurements as they
    // happen, and is the same mechanism the library's own tests use. A production app would more
    // likely wire a real OTel/Prometheus/App Insights exporter instead; this just logs each
    // measurement so running this sample makes the instrumentation visible in the console.
    public class MetricsConsoleListener(ILogger<MetricsConsoleListener> logger) : IHostedService, IDisposable
    {
        private MeterListener? _listener;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == "HealthCheckPlus")
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) => Log(instrument.Name, measurement, tags));
            _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) => Log(instrument.Name, measurement, tags));
            _listener.Start();
            return Task.CompletedTask;
        }

        private void Log(string instrumentName, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var parts = new List<string>();
            foreach (var tag in tags)
            {
                parts.Add($"{tag.Key}={tag.Value}");
            }
            logger.LogInformation("metric {Instrument}={Value} [{Tags}]", instrumentName, value, string.Join(", ", parts));
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _listener?.Dispose();
            return Task.CompletedTask;
        }

        public void Dispose() => _listener?.Dispose();
    }
}
