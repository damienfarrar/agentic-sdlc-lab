using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Lab.Tests.ClientService;

/// <summary>
/// Records log entries, with their structured state, so tests can check what was logged. Written by hand rather
/// than adding Microsoft.Extensions.Diagnostics.Testing for one helper.
/// </summary>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    // A plain object, not System.Threading.Lock: WaitFor needs Monitor.Wait, which only works on monitor locks.
    private readonly object _gate = new();
    private readonly List<LogEntry> _entries = [];

    public IReadOnlyList<LogEntry> Entries
    {
        get { lock (_gate) { return [.. _entries]; } }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        var values = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];
        var entry = new LogEntry(logLevel, formatter(state, exception), values);

        lock (_gate)
        {
            _entries.Add(entry);
            Monitor.PulseAll(_gate);
        }
    }

    /// <summary>Blocks until an entry matches or the timeout passes. A bounded wait on a condition, not a sleep.</summary>
    public LogEntry? WaitFor(Func<LogEntry, bool> match, TimeSpan timeout)
    {
        // Real elapsed time on purpose (a wait between threads), measured monotonically.
        var started = Stopwatch.GetTimestamp();
        lock (_gate)
        {
            while (true)
            {
                var found = _entries.Find(entry => match(entry));
                var remaining = timeout - Stopwatch.GetElapsedTime(started);
                if (found is not null || remaining <= TimeSpan.Zero)
                {
                    return found;
                }

                Monitor.Wait(_gate, remaining);
            }
        }
    }
}
