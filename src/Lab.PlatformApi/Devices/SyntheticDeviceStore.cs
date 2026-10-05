namespace Lab.PlatformApi.Devices;

/// <summary>
/// Read-only, in-memory device data. A stand-in for the platform's device registry;
/// all identifiers, models and sites are fictional.
/// </summary>
public sealed class SyntheticDeviceStore
{
    private static readonly Device[] Devices =
    [
        new("LAB-DEVICE-001", "SimDevice D100", "Synthetic Site A", DeviceStatus.Online),
        new("LAB-DEVICE-002", "SimDevice D100", "Synthetic Site A", DeviceStatus.Offline),
        new("LAB-DEVICE-003", "SimDevice D200", "Synthetic Site B", DeviceStatus.Online),
    ];

    public IReadOnlyList<Device> GetAll() => Devices;

    // Filtering lives here, not in the endpoint: a real registry would push it down into its query.
    public IReadOnlyList<Device> GetByStatus(DeviceStatus status) =>
        Array.FindAll(Devices, device => device.Status == status);

    public Device? Find(string id) =>
        Array.Find(Devices, device => string.Equals(device.Id, id, StringComparison.OrdinalIgnoreCase));
}
