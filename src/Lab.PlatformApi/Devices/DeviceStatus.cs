using System.Text.Json.Serialization;

namespace Lab.PlatformApi.Devices;

// Serialized as "Online"/"Offline" rather than 0/1 so the wire format is readable and stable.
[JsonConverter(typeof(JsonStringEnumConverter<DeviceStatus>))]
public enum DeviceStatus
{
    Online,
    Offline,
}
