using System.Text.Json;
using Lab.ClientService.Commands;
using Lab.ClientService.Diagnostics;

namespace Lab.Tests.ClientService;

public sealed class PollIntervalValidatorTests
{
    private static readonly Guid CommandId = Guid.Parse("0d3b7e52-9a41-4f6c-8b2e-7c1d5a9f3e60");
    private static readonly PollIntervalBounds DefaultBounds = new(TimeSpan.FromSeconds(10), TimeSpan.FromHours(1));

    [Fact]
    public void TryValidate_InRange_ReturnsInterval()
    {
        Assert.True(PollIntervalValidator.TryValidate(Command("60"), DefaultBounds, out var valid, out _));
        Assert.Equal(CommandId, valid.Id);
        Assert.Equal(TimeSpan.FromMinutes(1), valid.Interval);
    }

    // Both bounds are inclusive.
    [Theory]
    [InlineData("10")]
    [InlineData("3600")]
    public void TryValidate_OnBound_Accepted(string rawInterval)
    {
        Assert.True(PollIntervalValidator.TryValidate(Command(rawInterval), DefaultBounds, out _, out _));
    }

    [Theory]
    [InlineData("9")]
    [InlineData("3601")]
    public void TryValidate_JustOutsideBound_Rejected(string rawInterval)
    {
        Assert.False(PollIntervalValidator.TryValidate(Command(rawInterval), DefaultBounds, out var valid, out var rejection));
        Assert.Null(valid);
        Assert.Equal("Poll interval out of range.", rejection);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    public void TryValidate_NonPositive_Rejected(string rawInterval)
    {
        Assert.False(PollIntervalValidator.TryValidate(Command(rawInterval), DefaultBounds, out _, out var rejection));
        Assert.Equal("Poll interval out of range.", rejection);
    }

    // Never rounded or coerced: anything but a whole JSON number that fits an int is malformed.
    [Theory]
    [InlineData("1.5")]
    [InlineData("60.0")]
    [InlineData("6e1")]
    [InlineData("\"60\"")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("99999999999")] // larger than int.MaxValue
    public void TryValidate_Malformed_Rejected(string rawInterval)
    {
        Assert.False(PollIntervalValidator.TryValidate(Command(rawInterval), DefaultBounds, out var valid, out var rejection));
        Assert.Null(valid);
        Assert.Equal("Malformed poll interval.", rejection);
    }

    [Fact]
    public void TryValidate_MissingInterval_Rejected()
    {
        Assert.False(PollIntervalValidator.TryValidate(Command(rawInterval: null), DefaultBounds, out _, out var rejection));
        Assert.Equal("Malformed poll interval.", rejection);
    }

    // A valid interval doesn't excuse arguments that belong to another command type.
    [Theory]
    [InlineData("case-1042", null)]
    [InlineData(null, "Service")]
    public void TryValidate_ForeignArguments_Rejected(string? outputName, string? section)
    {
        var command = Command("60", outputName, section is null ? null : [section]);

        Assert.False(PollIntervalValidator.TryValidate(command, DefaultBounds, out _, out var rejection));
        Assert.Equal("Unexpected arguments for command type.", rejection);
    }

    // Equal bounds are how an administrator pins the interval so the platform can't change it.
    [Fact]
    public void TryValidate_PinnedBounds_AcceptsOnlyThatValue()
    {
        var pinned = new PollIntervalBounds(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        Assert.True(PollIntervalValidator.TryValidate(Command("30"), pinned, out _, out _));
        Assert.False(PollIntervalValidator.TryValidate(Command("29"), pinned, out _, out var below));
        Assert.False(PollIntervalValidator.TryValidate(Command("31"), pinned, out _, out var above));
        Assert.Equal("Poll interval out of range.", below);
        Assert.Equal("Poll interval out of range.", above);
    }

    [Fact]
    public void From_Options_CopiesMinAndMax()
    {
        var bounds = PollIntervalBounds.From(new DiagnosticsOptions { MinPollInterval = TimeSpan.FromSeconds(15), MaxPollInterval = TimeSpan.FromMinutes(5) });

        Assert.Equal(new PollIntervalBounds(TimeSpan.FromSeconds(15), TimeSpan.FromMinutes(5)), bounds);
    }

    private static DeviceCommandDto Command(string? rawInterval, string? outputName = null, string?[]? sections = null) =>
        new(CommandId, "SetPollInterval", outputName, sections, rawInterval is null ? null : Raw(rawInterval));

    private static JsonElement Raw(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
