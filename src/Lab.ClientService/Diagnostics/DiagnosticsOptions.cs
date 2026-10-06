namespace Lab.ClientService.Diagnostics;

public sealed class DiagnosticsOptions
{
    public const string SectionName = "Diagnostics";

    /// <summary>Off by default: a privileged service acting on remote commands should be an explicit opt-in.</summary>
    public bool Enabled { get; set; }

    public Uri? PlatformBaseAddress { get; set; }

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(30);

    public string OutputDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LabClientService", "diagnostics");

    // Only checked when enabled: a disabled feature shouldn't stop the service starting.
    // https only, so commands can't be read or rewritten on the wire. IsPathFullyQualified rather than
    // IsPathRooted: "C:temp" and "\temp" are rooted but resolve against the current drive or directory.
    internal static bool IsValid(DiagnosticsOptions options) =>
        !options.Enabled
        || (options.PlatformBaseAddress is { IsAbsoluteUri: true } address
            && address.Scheme == Uri.UriSchemeHttps
            && options.PollInterval > TimeSpan.Zero
            && Path.IsPathFullyQualified(options.OutputDirectory));
}
