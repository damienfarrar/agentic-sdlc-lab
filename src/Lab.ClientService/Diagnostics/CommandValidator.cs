using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Lab.ClientService.Diagnostics;

/// <summary>
/// Turns an untrusted <see cref="DeviceCommandDto"/> into a <see cref="ValidDiagnosticsCommand"/>, or rejects it.
/// Allowlists only: anything not explicitly known is refused. Rejection reasons are fixed strings and never
/// echo the command's values, so they're safe to log.
/// </summary>
public static partial class CommandValidator
{
    private const string CollectDiagnosticsType = "CollectDiagnostics";

    // Windows treats these as devices, not files, on older versions even with an extension (CON.zip).
    private static readonly FrozenSet<string> ReservedDeviceNames = new[]
    {
        "CON", "PRN", "AUX", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, DiagnosticsSection> SectionsByName =
        Enum.GetValues<DiagnosticsSection>().ToFrozenDictionary(section => section.ToString(), StringComparer.Ordinal);

    public static bool TryValidate(
        DeviceCommandDto command,
        [NotNullWhen(true)] out ValidDiagnosticsCommand? valid,
        [NotNullWhen(false)] out string? rejection)
    {
        ArgumentNullException.ThrowIfNull(command);
        valid = null;

        if (command.Id == Guid.Empty)
        {
            rejection = "Missing command id.";
            return false;
        }

        if (!string.Equals(command.Type, CollectDiagnosticsType, StringComparison.Ordinal))
        {
            rejection = "Unknown command type.";
            return false;
        }

        if (command.OutputName is not { } outputName
            || !OutputNamePattern().IsMatch(outputName)
            || ReservedDeviceNames.Contains(outputName))
        {
            rejection = "Invalid output name.";
            return false;
        }

        if (!TryParseSections(command.Sections, out var sections))
        {
            rejection = "Missing or unknown diagnostics section.";
            return false;
        }

        valid = new ValidDiagnosticsCommand(command.Id, outputName + ".zip", sections);
        rejection = null;
        return true;
    }

    private static bool TryParseSections(IReadOnlyList<string?>? values, out List<DiagnosticsSection> sections)
    {
        sections = [];
        if (values is null || values.Count == 0)
        {
            return false;
        }

        foreach (var value in values)
        {
            if (value is null || !SectionsByName.TryGetValue(value, out var section))
            {
                return false;
            }

            if (!sections.Contains(section))
            {
                sections.Add(section);
            }
        }

        return true;
    }

    // ASCII letters, digits and hyphens; no dots, separators, colons or spaces, so the name can't climb out of the
    // output folder, pick a drive or stream, or end in the dot/space Windows silently strips.
    // \z, not $: in .NET, $ also matches before a trailing "\n". Explicit [0-9], not \d, which matches any Unicode digit.
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9-]{0,63}\z", RegexOptions.CultureInvariant)]
    private static partial Regex OutputNamePattern();
}
