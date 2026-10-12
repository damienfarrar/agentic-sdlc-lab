namespace Lab.ClientService.Diagnostics;

public sealed class DiagnosticsOptions
{
    public const string SectionName = "Diagnostics";

    /// <summary>Off by default: a privileged service acting on remote commands should be an explicit opt-in.</summary>
    public bool Enabled { get; set; }

    public Uri? PlatformBaseAddress { get; set; }

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>The shortest interval the platform may set. The device's administrator decides, not the platform.</summary>
    public TimeSpan MinPollInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>The longest interval the platform may set.</summary>
    public TimeSpan MaxPollInterval { get; set; } = TimeSpan.FromHours(1);

    public string OutputDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LabClientService", "diagnostics");

    // PeriodicTimer throws for periods of about 49.7 days or more. Capping the maximum well below that means a
    // misconfigured bound can't let an accepted command crash the poll loop.
    internal static readonly TimeSpan MaxPollIntervalLimit = TimeSpan.FromHours(24);

    // Only checked when enabled: a disabled feature shouldn't stop the service starting.
    // https only, so commands can't be read or rewritten on the wire. IsPathFullyQualified rather than
    // IsPathRooted: "C:temp" and "\temp" are rooted but resolve against the current drive or directory.
    // The bounds chain also makes PollInterval positive: 0 < Min <= PollInterval <= Max <= 24 h.
    internal static bool IsValid(DiagnosticsOptions options) =>
        !options.Enabled
        || (options.PlatformBaseAddress is { IsAbsoluteUri: true } address
            && address.Scheme == Uri.UriSchemeHttps
            && options.MinPollInterval > TimeSpan.Zero
            && options.MinPollInterval <= options.PollInterval
            && options.PollInterval <= options.MaxPollInterval
            && options.MaxPollInterval <= MaxPollIntervalLimit
            && Path.IsPathFullyQualified(options.OutputDirectory));
}
