using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lab.ClientService;

public static class HeartbeatServiceCollectionExtensions
{
    /// <summary>
    /// Registers the heartbeat pipeline. Kept separate from <c>Program</c> so tests exercise
    /// exactly the same wiring and validation the service runs with.
    /// </summary>
    public static IServiceCollection AddHeartbeat(this IServiceCollection services)
    {
        services.AddOptions<HeartbeatOptions>()
            .BindConfiguration(HeartbeatOptions.SectionName)
            .Validate(HeartbeatOptions.IsValid, "Heartbeat:Interval must be positive and Heartbeat:DeviceId must be set.")
            .ValidateOnStart(); // Fail at startup, not on the first tick hours later.

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<HeartbeatFactory>();
        services.AddHostedService<HeartbeatWorker>();

        return services;
    }
}
