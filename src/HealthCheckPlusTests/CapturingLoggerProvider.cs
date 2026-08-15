// ********************************************************************************************
// MIT LICENCE
// The maintenance and evolution is maintained by the HealthCheckPlus project under MIT license
// ********************************************************************************************

using Microsoft.Extensions.Logging;

namespace HealthCheckPlusTests
{
    // Minimal ILoggerProvider test double to assert that a specific log entry was actually
    // emitted, instead of only inferring behavior from side effects (metrics, call counts).
    internal sealed record CapturedLogEntry(LogLevel Level, EventId EventId, string Message, Exception? Exception);

    internal sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<CapturedLogEntry> _entries = [];
        private readonly object _lock = new();

        public IReadOnlyList<CapturedLogEntry> Entries
        {
            get
            {
                lock (_lock)
                {
                    return [.. _entries];
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        private void Add(CapturedLogEntry entry)
        {
            lock (_lock)
            {
                _entries.Add(entry);
            }
        }

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                owner.Add(new CapturedLogEntry(logLevel, eventId, formatter(state, exception), exception));
            }
        }
    }
}
