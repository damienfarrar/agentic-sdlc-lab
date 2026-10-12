using System.Text.Json;

namespace Lab.ClientService.Diagnostics;

/// <summary>
/// The command exactly as the platform sent it. The client keeps its own contract rather than sharing the API's
/// types, and everything here is untrusted: strings stay strings until <see cref="CommandValidator"/> accepts them.
/// <see cref="IntervalSeconds"/> stays a raw JSON value on purpose: a typed number would make a malformed value
/// (a fraction, a string, an overflow) fail the whole response, so the rejection would be logged without the
/// command id. As a raw value it reaches the validator, which rejects it with the id and a fixed reason.
/// </summary>
public sealed record DeviceCommandDto(
    Guid Id,
    string? Type,
    string? OutputName,
    IReadOnlyList<string?>? Sections,
    JsonElement? IntervalSeconds = null);
