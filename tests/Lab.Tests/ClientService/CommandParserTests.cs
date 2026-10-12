using System.Text.Json;
using Lab.ClientService.Commands;
using Lab.ClientService.Diagnostics;

namespace Lab.Tests.ClientService;

public sealed class CommandParserTests
{
    private static readonly Guid CommandId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly PollIntervalBounds DefaultBounds = new(TimeSpan.FromSeconds(10), TimeSpan.FromHours(1));

    [Fact]
    public void TryParse_ValidDiagnostics_ReturnsValidDiagnosticsCommand()
    {
        var command = new DeviceCommandDto(CommandId, "CollectDiagnostics", "case-1042", ["Environment", "Service"]);

        Assert.True(CommandParser.TryParse(command, DefaultBounds, out var valid, out _));
        var diagnostics = Assert.IsType<ValidDiagnosticsCommand>(valid);
        Assert.Equal("case-1042.zip", diagnostics.FileName);
    }

    [Fact]
    public void TryParse_ValidPollInterval_ReturnsValidPollIntervalCommand()
    {
        var command = new DeviceCommandDto(CommandId, "SetPollInterval", null, null, Raw("60"));

        Assert.True(CommandParser.TryParse(command, DefaultBounds, out var valid, out _));
        var pollInterval = Assert.IsType<ValidPollIntervalCommand>(valid);
        Assert.Equal(CommandId, pollInterval.Id);
        Assert.Equal(TimeSpan.FromMinutes(1), pollInterval.Interval);
    }

    [Theory]
    [InlineData("setpollinterval")]
    [InlineData(" SetPollInterval")]
    [InlineData("SetPollInterval ")]
    [InlineData("RunScript")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_UnknownType_Rejected(string? type)
    {
        var command = new DeviceCommandDto(CommandId, type, null, null, Raw("60"));

        Assert.False(CommandParser.TryParse(command, DefaultBounds, out var valid, out var rejection));
        Assert.Null(valid);
        Assert.Equal("Unknown command type.", rejection);
    }

    [Fact]
    public void TryParse_EmptyId_Rejected()
    {
        var command = new DeviceCommandDto(Guid.Empty, "SetPollInterval", null, null, Raw("60"));

        Assert.False(CommandParser.TryParse(command, DefaultBounds, out _, out var rejection));
        Assert.Equal("Missing command id.", rejection);
    }

    private static JsonElement Raw(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
