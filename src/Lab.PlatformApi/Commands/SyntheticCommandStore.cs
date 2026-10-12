using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Lab.PlatformApi.Commands;

/// <summary>
/// In-memory, per-device command queues. A stand-in for the platform's command service; the seeded
/// command and its case label are fictional.
/// </summary>
public sealed class SyntheticCommandStore
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<DeviceCommand>> _queues =
        new(StringComparer.OrdinalIgnoreCase);

    public SyntheticCommandStore()
    {
        Enqueue("LAB-DEVICE-001", new DeviceCommand(
            Id: Guid.Parse("6f1c2d8e-0b7a-4c55-9e1f-3a2b4c5d6e7f"),
            Type: DeviceCommandType.CollectDiagnostics,
            OutputName: "case-1042",
            Sections: [DiagnosticsSection.Environment, DiagnosticsSection.Service]));

        // Fictional value, inside the client's default bounds of 10 s to 1 h.
        Enqueue("LAB-DEVICE-003", new DeviceCommand(
            Id: Guid.Parse("0d3b7e52-9a41-4f6c-8b2e-7c1d5a9f3e60"),
            Type: DeviceCommandType.SetPollInterval,
            OutputName: null,
            Sections: null,
            IntervalSeconds: 60));
    }

    public void Enqueue(string deviceId, DeviceCommand command) =>
        _queues.GetOrAdd(deviceId, _ => new ConcurrentQueue<DeviceCommand>()).Enqueue(command);

    // Dequeue, not peek: each command is handed out once, so two pollers can't both act on it.
    public bool TryClaim(string deviceId, [NotNullWhen(true)] out DeviceCommand? command)
    {
        command = null;
        return _queues.TryGetValue(deviceId, out var queue) && queue.TryDequeue(out command);
    }
}
