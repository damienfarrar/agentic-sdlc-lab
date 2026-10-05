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

    [Theory]
    [InlineData("Online", DeviceStatus.Online)]
    [InlineData("Offline", DeviceStatus.Offline)]
    [InlineData("offline", DeviceStatus.Offline)]
    public async Task GetAll_StatusFilter_ReturnsExactlyMatchingDevices(string status, DeviceStatus expected)
    {
        using var client = factory.CreateClient();

        var all = await client.GetFromJsonAsync<List<Device>>("/api/devices", TestContext.Current.CancellationToken);
        var filtered = await client.GetFromJsonAsync<List<Device>>($"/api/devices?status={status}", TestContext.Current.CancellationToken);

        Assert.NotNull(all);
        var expectedDevices = all.Where(device => device.Status == expected).ToList();
        // If the data had no device with this status, an empty result would pass and prove nothing.
        Assert.NotEmpty(expectedDevices);
        // Equal, not "all match": a filter that dropped some matching devices would pass an Assert.All.
        Assert.Equal(expectedDevices, filtered);
    }

    [Fact]
    public async Task GetAll_EmptyStatus_ReturnsAllDevices()
    {
        using var client = factory.CreateClient();

        var all = await client.GetFromJsonAsync<List<Device>>("/api/devices", TestContext.Current.CancellationToken);
        var filtered = await client.GetFromJsonAsync<List<Device>>("/api/devices?status=", TestContext.Current.CancellationToken);

        Assert.Equal(all, filtered);
    }

    [Fact]
    public async Task GetAll_WhitespaceStatus_ReturnsAllDevices()
    {
        using var client = factory.CreateClient();

        var all = await client.GetFromJsonAsync<List<Device>>("/api/devices", TestContext.Current.CancellationToken);
        var filtered = await client.GetFromJsonAsync<List<Device>>("/api/devices?status=%20", TestContext.Current.CancellationToken);

        Assert.Equal(all, filtered);
    }

    // "1", "99", "Online,Offline" and " Online" are all cases Enum.TryParse would accept.
    [Theory]
    [InlineData("Bogus")]
    [InlineData("1")]
    [InlineData("99")]
    [InlineData("Online,Offline")]
    [InlineData(" Online")]
    public async Task GetAll_InvalidStatus_Returns400ValidationProblem(string status)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/devices?status={Uri.EscapeDataString(status)}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Online, Offline", body, StringComparison.Ordinal);
    }

    // Only one status per request. The framework joins repeated values into "Online,Offline", which
    // the name-only parse rejects; this pins that, so a change in binding can't silently turn it into a filter.
    [Fact]
    public async Task GetAll_RepeatedStatus_Returns400()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/devices?status=Online&status=Offline", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_InvalidStatus_DoesNotEchoInput()
    {
        // A distinctive marker: short values like "1" appear in any problem body ("status":400).
        const string marker = "LabEchoProbe";
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/devices?status={marker}", TestContext.Current.CancellationToken);

        // Without this, a 200 [] (marker accepted as a filter) would also pass the echo check below.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(marker, body, StringComparison.OrdinalIgnoreCase);
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
