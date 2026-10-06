using System.IO.Compression;
using Lab.ClientService;
using Lab.ClientService.Diagnostics;
using Microsoft.Extensions.Options;

namespace Lab.Tests.ClientService;

public sealed class DiagnosticsCollectorTests : IDisposable
{
    private static readonly Guid CommandId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("lab-diagnostics-");

    // The output folder sits one level down, so a traversal to its parent stays inside the temp folder we delete.
    private string OutputDirectory => Path.Combine(_root.FullName, "out");

    [Fact]
    public void Write_ValidCommand_WritesZipWithRequestedSectionsOnly()
    {
        var collector = CreateCollector();

        var path = collector.Write(new ValidDiagnosticsCommand(CommandId, "case-1042.zip", [DiagnosticsSection.Service]));

        Assert.Equal(Path.Combine(OutputDirectory, "case-1042.zip"), path);
        using var zip = ZipFile.OpenRead(path);
        var entry = Assert.Single(zip.Entries);
        Assert.Equal("service.txt", entry.Name);
        using var reader = new StreamReader(entry.Open());
        var text = reader.ReadToEnd();
        Assert.Contains("DeviceId: LAB-DEVICE-001", text, StringComparison.Ordinal);
        Assert.Contains($"CommandId: {CommandId}", text, StringComparison.Ordinal);
    }

    // Privacy rule: bundles leave the machine, so they must not identify the machine or the user.
    [Fact]
    public void Write_AllSections_ExcludesMachineAndUserName()
    {
        var collector = CreateCollector();

        var path = collector.Write(new ValidDiagnosticsCommand(CommandId, "case-1042.zip", [DiagnosticsSection.Environment, DiagnosticsSection.Service]));

        using var zip = ZipFile.OpenRead(path);
        Assert.Equal(2, zip.Entries.Count);
        foreach (var entry in zip.Entries)
        {
            using var reader = new StreamReader(entry.Open());
            var text = reader.ReadToEnd();
            Assert.DoesNotContain(Environment.MachineName, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Environment.UserName, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Write_ExistingFile_ThrowsAndKeepsOriginal()
    {
        var collector = CreateCollector();
        var command = new ValidDiagnosticsCommand(CommandId, "case-1042.zip", [DiagnosticsSection.Service]);
        var path = collector.Write(command);
        var original = File.ReadAllBytes(path);

        Assert.Throws<IOException>(() => collector.Write(command));
        Assert.Equal(original, File.ReadAllBytes(path));
    }

    // Bypasses the validator on purpose: the collector's own resolved-path check must hold on its own.
    [Theory]
    [InlineData(@"..\escape.zip")]
    [InlineData("../escape.zip")]
    public void Write_PathOutsideOutputDirectory_Throws(string fileName)
    {
        var collector = CreateCollector();

        Assert.Throws<UnauthorizedAccessException>(() =>
            collector.Write(new ValidDiagnosticsCommand(CommandId, fileName, [DiagnosticsSection.Service])));
        Assert.False(File.Exists(Path.Combine(_root.FullName, "escape.zip")));
    }

    // "out-evil" starts with "out": a prefix check without the trailing separator would let this through.
    // The sibling folder exists, so without the check the write would succeed rather than fail for another reason.
    [Fact]
    public void Write_SiblingFolderSharingPrefix_Throws()
    {
        var sibling = Directory.CreateDirectory(Path.Combine(_root.FullName, "out-evil"));
        var collector = CreateCollector();

        Assert.Throws<UnauthorizedAccessException>(() =>
            collector.Write(new ValidDiagnosticsCommand(CommandId, @"..\out-evil\x.zip", [DiagnosticsSection.Service])));
        Assert.Empty(sibling.EnumerateFiles());
    }

    public void Dispose() => _root.Delete(recursive: true);

    private DiagnosticsCollector CreateCollector() => new(
        Options.Create(new DiagnosticsOptions { OutputDirectory = OutputDirectory }),
        Options.Create(new HeartbeatOptions { DeviceId = "LAB-DEVICE-001" }),
        new ManualTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero)));
}
