using System.Text.Json.Serialization;

namespace Lab.PlatformApi.Commands;

/// <summary>
/// A command queued for one device. Arguments that don't apply to the command's <see cref="Type"/> are null and
/// left out of the JSON. <see cref="OutputName"/> is a label (e.g. a case reference) that the client turns into a
/// file name, so the client must treat it as untrusted input. <see cref="IntervalSeconds"/> isn't checked here:
/// the bounds are set on the device, and the platform doesn't know them.
/// </summary>
public sealed record DeviceCommand(
    Guid Id,
    DeviceCommandType Type,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? OutputName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<DiagnosticsSection>? Sections,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? IntervalSeconds = null);
