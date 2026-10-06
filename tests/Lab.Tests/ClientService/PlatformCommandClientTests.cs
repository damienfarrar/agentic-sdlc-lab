using System.Net;
using System.Text;
using Lab.ClientService.Diagnostics;

namespace Lab.Tests.ClientService;

public sealed class PlatformCommandClientTests
{
    private static readonly Uri BaseAddress = new("https://platform.lab.test/");

    [Fact]
    public async Task ClaimNextAsync_NoContent_ReturnsNull()
    {
        using var client = new PlatformCommandClient(new StubHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.NoContent)), BaseAddress);

        var command = await client.ClaimNextAsync("LAB-DEVICE-001", TestContext.Current.CancellationToken);

        Assert.Null(command);
    }

    [Fact]
    public async Task ClaimNextAsync_Ok_ParsesCommandAndPostsToDevicePath()
    {
        const string body = """{"id":"11111111-2222-3333-4444-555555555555","type":"CollectDiagnostics","outputName":"case-1042","sections":["Environment"]}""";
        var handler = new StubHttpMessageHandler(() => Json(HttpStatusCode.OK, body));
        using var client = new PlatformCommandClient(handler, BaseAddress);

        var command = await client.ClaimNextAsync("LAB-DEVICE-001", TestContext.Current.CancellationToken);

        Assert.NotNull(command);
        Assert.Equal("case-1042", command.OutputName);
        Assert.Equal(["Environment"], command.Sections);
        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal("/api/devices/LAB-DEVICE-001/commands/next", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    // A device id must stay one path segment: no "..", extra segments or query string.
    [Fact]
    public async Task ClaimNextAsync_HostileDeviceId_IsEscapedIntoOneSegment()
    {
        var handler = new StubHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.NoContent));
        using var client = new PlatformCommandClient(handler, BaseAddress);

        await client.ClaimNextAsync("../admin?x=1", TestContext.Current.CancellationToken);

        var uri = handler.LastRequest?.RequestUri;
        Assert.NotNull(uri);
        Assert.Equal("/api/devices/..%2Fadmin%3Fx%3D1/commands/next", uri.AbsolutePath);
        Assert.Empty(uri.Query);
    }

    [Fact]
    public async Task ClaimNextAsync_ServerError_Throws()
    {
        using var client = new PlatformCommandClient(new StubHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.InternalServerError)), BaseAddress);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.ClaimNextAsync("LAB-DEVICE-001", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ClaimNextAsync_OversizedBody_Throws()
    {
        var body = $$"""{"outputName":"{{new string('a', PlatformCommandClient.MaxResponseBytes)}}"}""";
        using var client = new PlatformCommandClient(new StubHttpMessageHandler(() => Json(HttpStatusCode.OK, body)), BaseAddress);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.ClaimNextAsync("LAB-DEVICE-001", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ClaimNextAsync_NullJsonBody_Throws()
    {
        using var client = new PlatformCommandClient(new StubHttpMessageHandler(() => Json(HttpStatusCode.OK, "null")), BaseAddress);

        await Assert.ThrowsAsync<InvalidDataException>(() => client.ClaimNextAsync("LAB-DEVICE-001", TestContext.Current.CancellationToken));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
