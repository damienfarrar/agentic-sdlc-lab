using System.Net;
using System.Net.Http.Json;
using Lab.PlatformApi;
using Lab.PlatformApi.Commands;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Lab.Tests.PlatformApi;

/// <summary>
/// Claiming dequeues, so each test builds its own factory (and so its own store) instead of sharing a class
/// fixture: shared state would make the result depend on which test ran first.
/// </summary>
public sealed class CommandEndpointTests
{
    private const string ClaimPath = "/api/devices/LAB-DEVICE-001/commands/next";

    [Fact]
    public async Task ClaimNext_SeededDevice_ReturnsCommandOnce()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var first = await client.PostAsync(ClaimPath, content: null, TestContext.Current.CancellationToken);
        using var second = await client.PostAsync(ClaimPath, content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var command = await first.Content.ReadFromJsonAsync<DeviceCommand>(TestContext.Current.CancellationToken);
        Assert.NotNull(command);
        Assert.Equal(DeviceCommandType.CollectDiagnostics, command.Type);
        Assert.Equal("case-1042", command.OutputName);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
    }

    [Fact]
    public async Task ClaimNext_SerializesEnumsAsNames()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(ClaimPath, content: null, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // The client parses names, not numbers; pin the wire format it depends on.
        Assert.Contains("\"type\":\"CollectDiagnostics\"", body, StringComparison.Ordinal);
        Assert.Contains("\"sections\":[\"Environment\",\"Service\"]", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClaimNext_DeviceWithNoCommands_Returns204()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync("/api/devices/LAB-DEVICE-002/commands/next", content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ClaimNext_UnknownDevice_Returns404()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync("/api/devices/NOPE-999/commands/next", content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // GET must not dequeue: a crawler, a prefetch or a browser on the Portal origin could otherwise claim commands.
    [Fact]
    public async Task ClaimNext_Get_Returns405()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(ClaimPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }
}
