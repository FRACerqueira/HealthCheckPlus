// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using System.Diagnostics.Metrics;

namespace HealthCheckPlusTests
{
    // Shared test helper for the action plan (doc/plano-acao-healthcheckplus.md), step P4.5 —
    // captures measurements emitted on the "HealthCheckPlus" Meter via MeterListener, the standard
    // way to test System.Diagnostics.Metrics instrumentation without a real exporter.
    internal sealed record CapturedMeasurement(string InstrumentName, double Value, IReadOnlyDictionary<string, object?> Tags);

    internal sealed class MetricsCapture : IDisposable
    {
        private readonly MeterListener _listener;
        private readonly List<CapturedMeasurement> _measurements = [];
        private readonly object _lock = new();

        public MetricsCapture()
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
            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) => Record(instrument.Name, measurement, tags));
            _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) => Record(instrument.Name, measurement, tags));
            _listener.Start();
        }

        private void Record(string instrumentName, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var tagDictionary = new Dictionary<string, object?>();
            foreach (var tag in tags)
            {
                tagDictionary[tag.Key] = tag.Value;
            }

            lock (_lock)
            {
                _measurements.Add(new CapturedMeasurement(instrumentName, value, tagDictionary));
            }
        }

        public IReadOnlyList<CapturedMeasurement> Measurements
        {
            get
            {
                lock (_lock)
                {
                    return [.. _measurements];
                }
            }
        }

        public void Dispose()
        {
            _listener.Dispose();
        }
    }
}
