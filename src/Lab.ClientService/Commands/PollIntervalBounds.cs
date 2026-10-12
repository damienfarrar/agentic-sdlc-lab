using Lab.ClientService.Diagnostics;

namespace Lab.ClientService.Commands;

/// <summary>The inclusive range of poll intervals the platform may choose, set by the device's administrator.</summary>
public sealed record PollIntervalBounds(TimeSpan Min, TimeSpan Max)
{
    public static PollIntervalBounds From(DiagnosticsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new PollIntervalBounds(options.MinPollInterval, options.MaxPollInterval);
    }
}
