namespace Lab.ClientService.Diagnostics;

/// <summary>
/// The command exactly as the platform sent it. The client keeps its own contract rather than sharing the API's
/// types, and everything here is untrusted: strings stay strings until <see cref="CommandValidator"/> accepts them.
/// </summary>
public sealed record DeviceCommandDto(
    Guid Id,
    string? Type,
    string? OutputName,
    IReadOnlyList<string?>? Sections);
