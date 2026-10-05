using Microsoft.Extensions.Options;

namespace Lab.ClientService;

/// <summary>
/// Creates sequenced heartbeats. Time comes from <see cref="TimeProvider"/> rather than
/// <c>DateTimeOffset.UtcNow</c> so tests can pin the clock.
/// </summary>
public sealed class HeartbeatFactory(IOptions<HeartbeatOptions> options, TimeProvider timeProvider)
{
    private long _sequence;

    public Heartbeat Next() =>
        new(options.Value.DeviceId, Interlocked.Increment(ref _sequence), timeProvider.GetUtcNow());
}
