using Lab.ClientService.Diagnostics;

namespace Lab.Tests.ClientService;

public sealed class CommandValidatorTests
{
    private static readonly Guid CommandId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void TryValidate_WellFormedCommand_ReturnsZipFileNameAndSections()
    {
        var command = Command(sections: ["Service", "Environment", "Service"]);

        Assert.True(CommandValidator.TryValidate(command, out var valid, out _));
        Assert.Equal(CommandId, valid.Id);
        Assert.Equal("case-1042.zip", valid.FileName);
        // Duplicates collapse; order follows the command.
        Assert.Equal([DiagnosticsSection.Service, DiagnosticsSection.Environment], valid.Sections);
    }

    [Theory]
    [InlineData(@"..\x")]
    [InlineData("../x")]
    [InlineData("..")]
    [InlineData(@"C:\x")]
    [InlineData("C:x")]
    [InlineData(@"\\srv\share")]
    [InlineData("a/b")]
    [InlineData("case:stream")] // NTFS alternate data stream
    [InlineData("x.exe")]
    [InlineData("x.")] // Windows strips trailing dots and spaces
    [InlineData("x ")]
    [InlineData("case-1042\n")] // $ would match before this newline; \z doesn't
    [InlineData("-leading-hyphen")]
    [InlineData("\uFF0E\uFF0E")] // full-width dots
    [InlineData("\uFF43ase")] // full-width letter
    [InlineData("\u0661\u0662")] // Arabic-Indic digits: \d would accept these
    [InlineData("CON")]
    [InlineData("nul")]
    [InlineData("Com1")]
    [InlineData("LPT9")]
    [InlineData("")]
    public void TryValidate_HostileOutputName_Rejected(string outputName)
    {
        Assert.False(CommandValidator.TryValidate(Command(outputName: outputName), out var valid, out var rejection));
        Assert.Null(valid);
        Assert.Equal("Invalid output name.", rejection);
    }

    [Fact]
    public void TryValidate_OutputNameLengthBoundary_Accepts64Rejects65()
    {
        Assert.True(CommandValidator.TryValidate(Command(outputName: new string('a', 64)), out _, out _));
        Assert.False(CommandValidator.TryValidate(Command(outputName: new string('a', 65)), out _, out _));
    }

    [Fact]
    public void TryValidate_NullOutputName_Rejected()
    {
        Assert.False(CommandValidator.TryValidate(Command() with { OutputName = null }, out _, out var rejection));
        Assert.Equal("Invalid output name.", rejection);
    }

    [Theory]
    [InlineData("RunScript")]
    [InlineData("collectdiagnostics")] // exact match only: the platform sends the exact name
    [InlineData("")]
    [InlineData(null)]
    public void TryValidate_UnknownType_Rejected(string? type)
    {
        Assert.False(CommandValidator.TryValidate(Command() with { Type = type }, out _, out var rejection));
        Assert.Equal("Unknown command type.", rejection);
    }

    [Fact]
    public void TryValidate_EmptyId_Rejected()
    {
        Assert.False(CommandValidator.TryValidate(Command() with { Id = Guid.Empty }, out _, out var rejection));
        Assert.Equal("Missing command id.", rejection);
    }

    // One bad section alongside a good one must still reject the whole command.
    [Theory]
    [InlineData("Registry")]
    [InlineData("0")] // enum ordinal
    [InlineData("environment")] // exact match only
    [InlineData(null)]
    public void TryValidate_UnknownSection_Rejected(string? section)
    {
        Assert.False(CommandValidator.TryValidate(Command() with { Sections = ["Environment", section] }, out _, out var rejection));
        Assert.Equal("Missing or unknown diagnostics section.", rejection);
    }

    [Fact]
    public void TryValidate_MissingSections_Rejected()
    {
        Assert.False(CommandValidator.TryValidate(Command() with { Sections = null }, out _, out var nullRejection));
        Assert.False(CommandValidator.TryValidate(Command() with { Sections = [] }, out _, out var emptyRejection));
        Assert.Equal("Missing or unknown diagnostics section.", nullRejection);
        Assert.Equal("Missing or unknown diagnostics section.", emptyRejection);
    }

    [Fact]
    public void TryValidate_Rejection_DoesNotEchoInput()
    {
        const string marker = "LabEchoProbe/..";

        CommandValidator.TryValidate(Command(outputName: marker), out _, out var rejection);

        Assert.DoesNotContain("LabEchoProbe", rejection, StringComparison.Ordinal);
    }

    private static DeviceCommandDto Command(string outputName = "case-1042", string?[]? sections = null) =>
        new(CommandId, "CollectDiagnostics", outputName, sections ?? ["Environment", "Service"]);
}
