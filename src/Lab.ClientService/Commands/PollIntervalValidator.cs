using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Lab.ClientService.Diagnostics;

namespace Lab.ClientService.Commands;

/// <summary>
/// Turns an untrusted SetPollInterval command into a <see cref="ValidPollIntervalCommand"/>, or rejects it. The
/// value is never rounded or clamped: acting on a value nobody sent would hide a broken or hostile platform.
/// Rejection reasons are fixed strings and never echo the command's values, so they're safe to log.
/// </summary>
public static class PollIntervalValidator
{
    public static bool TryValidate(
        DeviceCommandDto command,
        PollIntervalBounds bounds,
        [NotNullWhen(true)] out ValidPollIntervalCommand? valid,
        [NotNullWhen(false)] out string? rejection)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(bounds);
        valid = null;

        // A valid interval doesn't excuse arguments that belong to another command type.
        if (command.OutputName is not null || command.Sections is not null)
        {
            rejection = "Unexpected arguments for command type.";
            return false;
        }

        // TryGetInt32 only succeeds when the whole token is an integer, so 1.5, 60.0 and 6e1 fail here.
        // A JSON null or a missing field counts as absent, and absent is malformed.
        if (command.IntervalSeconds is not { ValueKind: JsonValueKind.Number } raw || !raw.TryGetInt32(out var seconds))
        {
            rejection = "Malformed poll interval.";
            return false;
        }

        var interval = TimeSpan.FromSeconds(seconds);
        if (interval < bounds.Min || interval > bounds.Max)
        {
            rejection = "Poll interval out of range.";
            return false;
        }

        valid = new ValidPollIntervalCommand(command.Id, interval);
        rejection = null;
        return true;
    }
}
