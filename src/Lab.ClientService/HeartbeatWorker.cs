using Microsoft.Extensions.Options;

namespace Lab.ClientService;

public sealed partial class HeartbeatWorker(
    HeartbeatFactory factory,
    IOptions<HeartbeatOptions> options,
    TimeProvider timeProvider,
    ILogger<HeartbeatWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // PeriodicTimer doesn't drift or overlap ticks the way `Task.Delay` in a loop can.
        using var timer = new PeriodicTimer(options.Value.Interval, timeProvider);

        do
        {
            var heartbeat = factory.Next();
            LogHeartbeat(logger, heartbeat.Sequence, heartbeat.DeviceId, heartbeat.TimestampUtc);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    // Source-generated logging: no boxing or template parsing per call, and the event shape is fixed at compile time.
    [LoggerMessage(Level = LogLevel.Information, Message = "Heartbeat {Sequence} from {DeviceId} at {TimestampUtc:O}")]
    private static partial void LogHeartbeat(ILogger logger, long sequence, string deviceId, DateTimeOffset timestampUtc);
}
