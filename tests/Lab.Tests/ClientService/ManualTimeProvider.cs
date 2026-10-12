namespace Lab.Tests.ClientService;

/// <summary>
/// Minimal controllable clock. Subclassing the BCL's TimeProvider avoids pulling in
/// Microsoft.Extensions.TimeProvider.Testing. Timers it creates only fire when a test calls <see cref="Advance"/>.
/// </summary>
internal sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
    private readonly Lock _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = now;

    public DateTimeOffset Now
    {
        get { lock (_gate) { return _now; } }
        set { lock (_gate) { _now = value; } }
    }

    public override DateTimeOffset GetUtcNow() => Now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        lock (_gate)
        {
            _timers.Add(timer);
        }

        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>Moves the clock forward and fires every timer that falls due on the way.</summary>
    /// <returns>How many timer callbacks fired, so a test can assert on the schedule without racing the worker.</returns>
    public int Advance(TimeSpan by)
    {
        DateTimeOffset target;
        lock (_gate)
        {
            target = _now + by;
        }

        var fired = 0;
        while (NextDue(target) is { } next)
        {
            Now = next.DueAt;
            fired += next.Timer.Fire() ? 1 : 0;
        }

        Now = target;
        return fired;
    }

    internal void Remove(ManualTimer timer)
    {
        lock (_gate)
        {
            _timers.Remove(timer);
        }
    }

    // The earliest timer due at or before the target, so callbacks fire in time order across timers.
    private (ManualTimer Timer, DateTimeOffset DueAt)? NextDue(DateTimeOffset target)
    {
        lock (_gate)
        {
            (ManualTimer Timer, DateTimeOffset DueAt)? next = null;
            foreach (var timer in _timers)
            {
                if (timer.DueAt is { } due && due <= target && (next is null || due < next.Value.DueAt))
                {
                    next = (timer, due);
                }
            }

            return next;
        }
    }
}
