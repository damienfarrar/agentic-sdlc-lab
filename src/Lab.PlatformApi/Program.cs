using Lab.PlatformApi.Commands;
using Lab.PlatformApi.Devices;

namespace Lab.PlatformApi;

// Public and non-static on purpose: tests use it as the type argument of WebApplicationFactory<Program>.
public class Program
{
    private const string PortalCorsPolicy = "Portal";

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddHealthChecks();
        builder.Services.AddSingleton<SyntheticDeviceStore>();
        builder.Services.AddSingleton<SyntheticCommandStore>();

        // Browsers may call this API only from the Portal's origin, and only with GET.
        // Origins come from config; appsettings.json defaults to none (deny all).
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        builder.Services.AddCors(options => options.AddPolicy(PortalCorsPolicy, policy =>
            policy.WithOrigins(allowedOrigins).WithMethods(HttpMethods.Get)));

        var app = builder.Build();

        app.UseHttpsRedirection();
        app.UseCors(PortalCorsPolicy);

        app.MapHealthChecks("/health");
        app.MapDeviceEndpoints();
        app.MapCommandEndpoints();

        app.Run();
    }
}
