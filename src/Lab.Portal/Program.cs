using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace Lab.Portal;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        // wwwroot/appsettings.json is downloaded by every browser: configuration only, never secrets.
        var platformApiBaseUrl = builder.Configuration["PlatformApi:BaseUrl"]
            ?? throw new InvalidOperationException("PlatformApi:BaseUrl is missing from wwwroot/appsettings.json.");

        builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(platformApiBaseUrl) });

        await builder.Build().RunAsync();
    }
}
