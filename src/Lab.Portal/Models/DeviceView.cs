namespace Lab.Portal.Models;

/// <summary>
/// The Portal's view of a device from Lab.PlatformApi's GET /api/devices.
/// Deliberately duplicated rather than shared; see ADR-0001 follow-ups (Lab.Contracts).
/// </summary>
public sealed record DeviceView(string Id, string Model, string Site, string Status);
