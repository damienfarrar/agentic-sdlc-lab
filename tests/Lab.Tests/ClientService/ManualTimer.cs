namespace Lab.Tests.ClientService;

/// <summary>A timer driven by <see cref="ManualTimeProvider.Advance"/> instead of the system clock.</summary>
internal sealed class ManualTimer(ManualTimeProvider provider, TimerCallback callback, object? state) : ITimer
{
    private readonly Lock _gate = new();
    private DateTimeOffset? _dueAt;
    private TimeSpan _period = Timeout.InfiniteTimeSpan;

    public DateTimeOffset? DueAt
    {
        get { lock (_gate) { return _dueAt; } }
    }

    public bool Change(TimeSpan dueTime, TimeSpan period)
    {
        // Read the clock before taking this timer's lock: the provider takes its lock and then each timer's,
        // so taking them in the other order here could deadlock.
        var now = provider.GetUtcNow();
        lock (_gate)
        {
            _dueAt = dueTime == Timeout.InfiniteTimeSpan ? null : now + dueTime;
            _period = period;
        }

        return true;
    }

    /// <returns>True if the callback ran.</returns>
    internal bool Fire()
    {
        lock (_gate)
        {
            if (_dueAt is not { } due)
            {
                return false;
            }

            _dueAt = _period == Timeout.InfiniteTimeSpan || _period == TimeSpan.Zero ? null : due + _period;
        }

        callback(state);
        return true;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _dueAt = null;
        }

        provider.Remove(this);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
