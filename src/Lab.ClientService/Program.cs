using Lab.ClientService.Diagnostics;

namespace Lab.ClientService;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Only takes effect when the Windows Service Control Manager starts the process, so
        // `dotnet run` still works as a plain console app. Adds SCM lifetime and Event Log logging.
        builder.Services.AddWindowsService(options => options.ServiceName = "LabClientService");

        builder.Services.AddHeartbeat();
        builder.Services.AddDiagnosticsCommands(builder.Configuration);

        builder.Build().Run();
    }
}
