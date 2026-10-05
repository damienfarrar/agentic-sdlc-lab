using Lab.ClientService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lab.Tests.ClientService;

/// <summary>
/// Exercises the real AddHeartbeat() wiring with in-memory config, so a bad deployment config is
/// caught here rather than when the Windows service starts on a client machine.
/// </summary>
public sealed class HeartbeatOptionsTests
{
    [Fact]
    public void ValidConfig_Binds()
    {
        using var provider = BuildProvider(interval: "00:00:10", deviceId: "LAB-DEVICE-002");

        var options = provider.GetRequiredService<IOptions<HeartbeatOptions>>().Value;

        Assert.Equal(TimeSpan.FromSeconds(10), options.Interval);
        Assert.Equal("LAB-DEVICE-002", options.DeviceId);
    }

    [Theory]
    [InlineData("00:00:00", "LAB-DEVICE-001")] // zero interval
    [InlineData("-00:00:01", "LAB-DEVICE-001")] // negative interval
    [InlineData("00:00:05", "")] // missing device id
    public void InvalidConfig_FailsValidation(string interval, string deviceId)
    {
        using var provider = BuildProvider(interval, deviceId);

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<HeartbeatOptions>>().Value);
    }

    private static ServiceProvider BuildProvider(string interval, string deviceId)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Heartbeat:Interval"] = interval,
                ["Heartbeat:DeviceId"] = deviceId,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddHeartbeat();
        return services.BuildServiceProvider();
    }
}
