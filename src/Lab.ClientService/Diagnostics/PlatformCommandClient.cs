using System.Net;
using System.Net.Http.Json;

namespace Lab.ClientService.Diagnostics;

/// <summary>
/// Claims commands from the platform. Owns its <see cref="HttpClient"/> so the response-size limit is set here,
/// where it can be tested, rather than relying on whoever registers the client.
/// </summary>
public sealed class PlatformCommandClient : IDisposable
{
    /// <summary>A command is a few hundred bytes; anything near this is hostile or broken.</summary>
    public const int MaxResponseBytes = 64 * 1024;

    private readonly HttpClient _httpClient;

    public PlatformCommandClient(HttpMessageHandler handler, Uri baseAddress)
    {
        _httpClient = new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(30),
            MaxResponseContentBufferSize = MaxResponseBytes,
        };
    }

    /// <returns>The next command, or <see langword="null"/> when the platform has none (204).</returns>
    public async Task<DeviceCommandDto?> ClaimNextAsync(string deviceId, CancellationToken cancellationToken)
    {
        // Escaped so a device id can't add path segments or a query to the request.
        var path = new Uri($"api/devices/{Uri.EscapeDataString(deviceId)}/commands/next", UriKind.Relative);
        using var response = await _httpClient.PostAsync(path, content: null, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        // A 200 with a JSON null body is a broken platform, not "no command": only 204 means that.
        return await response.Content.ReadFromJsonAsync<DeviceCommandDto>(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The platform returned an empty command.");
    }

    public void Dispose() => _httpClient.Dispose();
}
