using System.Diagnostics.CodeAnalysis;
using Lab.ClientService.Diagnostics;

namespace Lab.ClientService.Commands;

/// <summary>
/// The single allowlist of command types. Checks the id, matches the type exactly, and hands the command to that
/// type's validator. Anything not explicitly known is refused.
/// </summary>
public static class CommandParser
{
    private const string CollectDiagnosticsType = "CollectDiagnostics";
    private const string SetPollIntervalType = "SetPollInterval";

    public static bool TryParse(
        DeviceCommandDto command,
        PollIntervalBounds bounds,
        [NotNullWhen(true)] out ValidCommand? valid,
        [NotNullWhen(false)] out string? rejection)
    {
        ArgumentNullException.ThrowIfNull(command);
        valid = null;

        if (command.Id == Guid.Empty)
        {
            rejection = "Missing command id.";
            return false;
        }

        // Exact, ordinal matches: "setpollinterval" or " SetPollInterval" are unknown types.
        if (string.Equals(command.Type, CollectDiagnosticsType, StringComparison.Ordinal))
        {
            if (!CommandValidator.TryValidate(command, out var diagnostics, out rejection))
            {
                return false;
            }

            valid = diagnostics;
            return true;
        }

        if (string.Equals(command.Type, SetPollIntervalType, StringComparison.Ordinal))
        {
            if (!PollIntervalValidator.TryValidate(command, bounds, out var pollInterval, out rejection))
            {
                return false;
            }

            valid = pollInterval;
            return true;
        }

        rejection = "Unknown command type.";
        return false;
    }
}
