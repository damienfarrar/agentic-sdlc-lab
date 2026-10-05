namespace Lab.PlatformApi.Devices;

/// <summary>A synthetic device. Every value is invented for the sandbox.</summary>
public sealed record Device(string Id, string Model, string Site, DeviceStatus Status);
