using Microsoft.AspNetCore.Http.HttpResults;

namespace Lab.PlatformApi.Devices;

public static class DeviceEndpoints
{
    public static IEndpointRouteBuilder MapDeviceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var devices = endpoints.MapGroup("/api/devices");

        devices.MapGet("/", (SyntheticDeviceStore store) => TypedResults.Ok(store.GetAll()));

        // Typed union return so the 200/404 contract is visible in the signature (and to OpenAPI later).
        devices.MapGet("/{id}", Results<Ok<Device>, NotFound> (string id, SyntheticDeviceStore store) =>
            store.Find(id) is { } device ? TypedResults.Ok(device) : TypedResults.NotFound());

        return endpoints;
    }
}
