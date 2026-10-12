using Lab.ClientService;
using Lab.ClientService.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Lab.Tests.ClientService;

/// <summary>Exercises the real AddDiagnosticsCommands() wiring, the same way HeartbeatOptionsTests does.</summary>
public sealed class DiagnosticsOptionsTests
{
    [Fact]
    public void Disabled_RegistersNoClientOrWorker()
    {
        // Invalid values on purpose: a disabled feature must not fail startup.
        using var provider = BuildProvider(new() { ["Diagnostics:Enabled"] = "false", ["Diagnostics:PlatformBaseAddress"] = "http://insecure.lab.test/" });

        Assert.False(provider.GetRequiredService<IOptions<DiagnosticsOptions>>().Value.Enabled);
        Assert.Null(provider.GetService<PlatformCommandClient>());
        Assert.DoesNotContain(provider.GetServices<IHostedService>(), service => service is DiagnosticsWorker);
    }

    [Fact]
    public void Enabled_ValidConfig_RegistersClientAndWorker()
    {
        using var provider = BuildProvider(Enabled("https://platform.lab.test/", @"C:\LabData\diagnostics"));

        Assert.True(provider.GetRequiredService<IOptions<DiagnosticsOptions>>().Value.Enabled);
        Assert.NotNull(provider.GetService<PlatformCommandClient>());
        Assert.Contains(provider.GetServices<IHostedService>(), service => service is DiagnosticsWorker);
    }

    [Theory]
    [InlineData("http://platform.lab.test/", @"C:\LabData\diagnostics")] // plain http
    [InlineData("platform.lab.test", @"C:\LabData\diagnostics")] // not absolute
    [InlineData("https://platform.lab.test/", @"diagnostics")] // relative
    [InlineData("https://platform.lab.test/", @"C:diagnostics")] // drive-relative
    [InlineData("https://platform.lab.test/", @"\diagnostics")] // root of the current drive
    public void Enabled_InvalidConfig_FailsValidation(string baseAddress, string outputDirectory)
    {
        using var provider = BuildProvider(Enabled(baseAddress, outputDirectory));

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<DiagnosticsOptions>>().Value);
    }

    [Fact]
    public void Enabled_MissingBaseAddress_FailsValidation()
    {
        using var provider = BuildProvider(new() { ["Diagnostics:Enabled"] = "true" });

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<DiagnosticsOptions>>().Value);
    }

    [Fact]
    public void Enabled_NoBounds_UsesDefaults()
    {
        using var provider = BuildProvider(Enabled("https://platform.lab.test/", @"C:\LabData\diagnostics"));

        var options = provider.GetRequiredService<IOptions<DiagnosticsOptions>>().Value;
        Assert.Equal(TimeSpan.FromSeconds(10), options.MinPollInterval);
        Assert.Equal(TimeSpan.FromHours(1), options.MaxPollInterval);
    }

    [Theory]
    [InlineData("00:00:00", "01:00:00", "00:00:30")] // minimum not positive
    [InlineData("-00:00:01", "01:00:00", "00:00:30")] // negative minimum
    [InlineData("00:01:00", "00:00:30", "00:00:45")] // minimum above maximum
    [InlineData("00:00:10", "1.00:00:01", "00:00:30")] // maximum over 24 hours
    [InlineData("00:01:00", "01:00:00", "00:00:30")] // configured interval below the minimum
    [InlineData("00:00:10", "00:00:20", "00:00:30")] // configured interval above the maximum
    public void Enabled_InvalidBounds_FailsValidation(string min, string max, string pollInterval)
    {
        using var provider = BuildProvider(WithBounds(Enabled("https://platform.lab.test/", @"C:\LabData\diagnostics"), min, max, pollInterval));

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<DiagnosticsOptions>>().Value);
    }

    [Fact]
    public void Enabled_MaxAtDayLimit_Valid()
    {
        using var provider = BuildProvider(WithBounds(Enabled("https://platform.lab.test/", @"C:\LabData\diagnostics"), "00:00:10", "1.00:00:00", "00:00:30"));

        Assert.Equal(TimeSpan.FromDays(1), provider.GetRequiredService<IOptions<DiagnosticsOptions>>().Value.MaxPollInterval);
    }

    // Like the existing disabled case: bounds that are never used mustn't stop the service starting.
    [Fact]
    public void Disabled_InvalidBounds_StillStarts()
    {
        using var provider = BuildProvider(WithBounds(new() { ["Diagnostics:Enabled"] = "false" }, "00:01:00", "00:00:30", "00:00:45"));

        Assert.False(provider.GetRequiredService<IOptions<DiagnosticsOptions>>().Value.Enabled);
        Assert.Null(provider.GetService<PlatformCommandClient>());
    }

    private static Dictionary<string, string?> WithBounds(Dictionary<string, string?> values, string min, string max, string pollInterval)
    {
        values["Diagnostics:MinPollInterval"] = min;
        values["Diagnostics:MaxPollInterval"] = max;
        values["Diagnostics:PollInterval"] = pollInterval;
        return values;
    }

    private static Dictionary<string, string?> Enabled(string baseAddress, string outputDirectory) => new()
    {
        ["Diagnostics:Enabled"] = "true",
        ["Diagnostics:PlatformBaseAddress"] = baseAddress,
        ["Diagnostics:OutputDirectory"] = outputDirectory,
    };

    private static ServiceProvider BuildProvider(Dictionary<string, string?> values)
    {
        values["Heartbeat:DeviceId"] = "LAB-DEVICE-001";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddOptions<HeartbeatOptions>().BindConfiguration(HeartbeatOptions.SectionName);
        services.AddDiagnosticsCommands(configuration);
        return services.BuildServiceProvider();
    }
}
