using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Lab.ClientService.Diagnostics;

public static class DiagnosticsServiceCollectionExtensions
{
    /// <summary>
    /// Registers remote diagnostics commands. When <c>Diagnostics:Enabled</c> is false, only the options are
    /// registered: the client and worker aren't in the container at all, so nothing polls the platform.
    /// </summary>
    public static IServiceCollection AddDiagnosticsCommands(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DiagnosticsOptions>()
            .BindConfiguration(DiagnosticsOptions.SectionName)
            .Validate(
                DiagnosticsOptions.IsValid,
                "When Diagnostics:Enabled is true, PlatformBaseAddress must be an absolute https URI, PollInterval must be positive and OutputDirectory must be a fully qualified path.")
            .ValidateOnStart();

        if (!configuration.GetValue<bool>($"{DiagnosticsOptions.SectionName}:{nameof(DiagnosticsOptions.Enabled)}"))
        {
            return services;
        }

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<DiagnosticsCollector>();
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<DiagnosticsOptions>>().Value;

            // One long-lived handler: reuses connections, and PooledConnectionLifetime picks up DNS changes,
            // which is what IHttpClientFactory would otherwise give us (without adding a package).
            // No redirects: the command endpoint has no reason to send one, and following it would hand the
            // request to wherever the response points.
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                AllowAutoRedirect = false,
            };

            return new PlatformCommandClient(handler, options.PlatformBaseAddress!);
        });
        services.AddHostedService<DiagnosticsWorker>();

        return services;
    }
}
