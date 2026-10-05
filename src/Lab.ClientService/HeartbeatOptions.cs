namespace Lab.ClientService;

public sealed class HeartbeatOptions
{
    public const string SectionName = "Heartbeat";

    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(5);

    public string DeviceId { get; set; } = string.Empty;

    internal static bool IsValid(HeartbeatOptions options) =>
        options.Interval > TimeSpan.Zero && !string.IsNullOrWhiteSpace(options.DeviceId);
}
