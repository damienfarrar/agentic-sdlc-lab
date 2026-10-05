using Lab.PlatformApi;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Lab.Tests.PlatformApi;

/// <summary>
/// Pins a security property: only the Portal's origin gets a CORS grant. If someone (or an agent)
/// loosens the policy to AllowAnyOrigin, these tests fail.
/// </summary>
public sealed class CorsPolicyTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("https://localhost:7201", true)]
    [InlineData("https://evil.example", false)]
    public async Task OnlyPortalOrigin_IsGrantedCors(string origin, bool expectGrant)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/devices");
        request.Headers.Add("Origin", origin);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        var granted = response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values)
            && values.Contains(origin);
        Assert.Equal(expectGrant, granted);
    }
}
