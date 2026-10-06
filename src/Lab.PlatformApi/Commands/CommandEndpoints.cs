using Lab.PlatformApi.Devices;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lab.PlatformApi.Commands;

public static class CommandEndpoints
{
    public static IEndpointRouteBuilder MapCommandEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // POST, not GET: claiming removes the command from the queue, so the call is neither safe nor idempotent.
        // It also keeps browsers on the Portal origin out, since the CORS policy allows GET only.
        // No authentication yet: any caller can claim any device's commands (tracked in the STRIDE model).
        endpoints.MapPost("/api/devices/{id}/commands/next", ClaimNext);

        return endpoints;
    }

    private static Results<Ok<DeviceCommand>, NoContent, NotFound> ClaimNext(
        string id, SyntheticDeviceStore devices, SyntheticCommandStore commands)
    {
        if (devices.Find(id) is null)
        {
            return TypedResults.NotFound();
        }

        return commands.TryClaim(id, out var command)
            ? TypedResults.Ok(command)
            : TypedResults.NoContent();
    }
}
