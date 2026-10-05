using Lab.ClientService;
using Microsoft.Extensions.Options;

namespace Lab.Tests.ClientService;

public sealed class HeartbeatFactoryTests
{
    [Fact]
    public void Next_IncrementsSequence_AndStampsTimeFromTimeProvider()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero));
        var options = Options.Create(new HeartbeatOptions { DeviceId = "LAB-DEVICE-001" });
        var factory = new HeartbeatFactory(options, clock);

        var first = factory.Next();
        clock.Now = clock.Now.AddSeconds(5);
        var second = factory.Next();

        Assert.Equal(new Heartbeat("LAB-DEVICE-001", 1, new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero)), first);
        Assert.Equal(2, second.Sequence);
        Assert.Equal(first.TimestampUtc.AddSeconds(5), second.TimestampUtc);
    }
}
