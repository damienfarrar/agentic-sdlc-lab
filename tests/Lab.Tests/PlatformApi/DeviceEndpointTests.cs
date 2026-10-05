using System.Net;
using System.Net.Http.Json;
using Lab.PlatformApi;
using Lab.PlatformApi.Devices;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Lab.Tests.PlatformApi;

public sealed class DeviceEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetAll_ReturnsOnlySyntheticDevices()
    {
        using var client = factory.CreateClient();

        var devices = await client.GetFromJsonAsync<List<Device>>("/api/devices", TestContext.Current.CancellationToken);

        Assert.NotNull(devices);
        Assert.NotEmpty(devices);
        // Guard the sandbox rule in code: every device id must use the synthetic LAB- prefix.
        Assert.All(devices, device => Assert.StartsWith("LAB-", device.Id, StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetById_KnownId_ReturnsDevice()
    {
        using var client = factory.CreateClient();

        var device = await client.GetFromJsonAsync<Device>("/api/devices/LAB-DEVICE-001", TestContext.Current.CancellationToken);

        Assert.NotNull(device);
        Assert.Equal("LAB-DEVICE-001", device.Id);
        Assert.Equal(DeviceStatus.Online, device.Status);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/devices/NOPE-999", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
