using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;

namespace Lab.ClientService.Diagnostics;

/// <summary>
/// Writes a diagnostics zip. Content comes only from fixed sources, never from the command, and deliberately
/// excludes machine and user names.
/// </summary>
public sealed class DiagnosticsCollector(
    IOptions<DiagnosticsOptions> options,
    IOptions<HeartbeatOptions> heartbeatOptions,
    TimeProvider timeProvider)
{
    private readonly DateTimeOffset _startedUtc = timeProvider.GetUtcNow();

    /// <returns>The full path of the zip that was written.</returns>
    public string Write(ValidDiagnosticsCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var root = Path.GetFullPath(options.Value.OutputDirectory);
        var rootWithSeparator = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(rootWithSeparator, command.FileName));

        // Defence in depth: the validator already restricts the name, but the check that matters is on the
        // resolved path, so a future change to the validator can't quietly reopen traversal.
        if (!path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("The resolved diagnostics path is outside the output directory.");
        }

        Directory.CreateDirectory(root);

        // CreateNew: a repeated or replayed command can't overwrite an earlier bundle.
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);

        foreach (var section in command.Sections)
        {
            var (entryName, lines) = section switch
            {
                DiagnosticsSection.Environment => ("environment.txt", EnvironmentLines()),
                DiagnosticsSection.Service => ("service.txt", ServiceLines(command.Id)),
                _ => throw new ArgumentOutOfRangeException(nameof(command), section, "Unknown diagnostics section."),
            };

            using var writer = new StreamWriter(zip.CreateEntry(entryName).Open());
            foreach (var line in lines)
            {
                writer.WriteLine(line);
            }
        }

        return path;
    }

    private static IEnumerable<string> EnvironmentLines() =>
    [
        $"OS: {RuntimeInformation.OSDescription}",
        $"Framework: {RuntimeInformation.FrameworkDescription}",
        $"Architecture: {RuntimeInformation.ProcessArchitecture}",
    ];

    private IEnumerable<string> ServiceLines(Guid commandId)
    {
        var now = timeProvider.GetUtcNow();
        var version = typeof(DiagnosticsCollector).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

        return
        [
            $"DeviceId: {heartbeatOptions.Value.DeviceId}",
            $"Version: {version}",
            $"CommandId: {commandId}",
            string.Create(CultureInfo.InvariantCulture, $"CollectedUtc: {now:O}"),
            string.Create(CultureInfo.InvariantCulture, $"Uptime: {now - _startedUtc:c}"),
        ];
    }
}
