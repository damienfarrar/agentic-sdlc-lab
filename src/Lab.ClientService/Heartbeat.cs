namespace Lab.ClientService;

/// <summary>One liveness signal from the (synthetic) device this client service manages.</summary>
public sealed record Heartbeat(string DeviceId, long Sequence, DateTimeOffset TimestampUtc);
