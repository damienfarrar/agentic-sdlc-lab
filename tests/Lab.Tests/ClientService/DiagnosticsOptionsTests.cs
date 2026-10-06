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
