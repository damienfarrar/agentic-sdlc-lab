namespace Lab.PlatformApi.Commands;

/// <summary>
/// A command queued for one device. <see cref="OutputName"/> is a label (e.g. a case reference) that the client
/// turns into a file name, so the client must treat it as untrusted input.
/// </summary>
public sealed record DeviceCommand(
    Guid Id,
    DeviceCommandType Type,
    string OutputName,
    IReadOnlyList<DiagnosticsSection> Sections);
