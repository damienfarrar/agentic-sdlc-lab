using Microsoft.AspNetCore.Http.HttpResults;

namespace Lab.PlatformApi.Devices;

public static class DeviceEndpoints
{
    public static IEndpointRouteBuilder MapDeviceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var devices = endpoints.MapGroup("/api/devices");

        devices.MapGet("/", GetDevices);

        // Typed union return so the 200/404 contract is visible in the signature (and to OpenAPI later).
        devices.MapGet("/{id}", Results<Ok<Device>, NotFound> (string id, SyntheticDeviceStore store) =>
            store.Find(id) is { } device ? TypedResults.Ok(device) : TypedResults.NotFound());

        return endpoints;
    }

    // `status` binds as a string, not DeviceStatus?, so we own the parsing and the 400 body.
    // Absent, empty or whitespace-only means "no filter". Padded values (" Online") still get a 400.
    private static Results<Ok<IReadOnlyList<Device>>, ValidationProblem> GetDevices(
        string? status, SyntheticDeviceStore store)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return TypedResults.Ok(store.GetAll());
        }

        if (!TryParseStatus(status, out var parsed))
        {
            // Lists the allowed values but doesn't echo the caller's input back.
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = [$"Unknown status. Allowed values: {string.Join(", ", Enum.GetNames<DeviceStatus>())}."],
            });
        }

        return TypedResults.Ok(store.GetByStatus(parsed));
    }

    // Matches enum names only, ignoring case. Enum.TryParse would also accept "1", "99" (undefined)
    // and "Online,Offline" (flags syntax), none of which are valid on the wire.
    private static bool TryParseStatus(string value, out DeviceStatus status)
    {
        foreach (var candidate in Enum.GetValues<DeviceStatus>())
        {
            if (string.Equals(candidate.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                status = candidate;
                return true;
            }
        }

        status = default;
        return false;
    }
}
