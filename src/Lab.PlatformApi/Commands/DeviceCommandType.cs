using System.Text.Json.Serialization;

namespace Lab.PlatformApi.Commands;

// A closed set of command types. The client maps each one to fixed code; nothing on the wire is executed.
[JsonConverter(typeof(JsonStringEnumConverter<DeviceCommandType>))]
public enum DeviceCommandType
{
    CollectDiagnostics,
}
