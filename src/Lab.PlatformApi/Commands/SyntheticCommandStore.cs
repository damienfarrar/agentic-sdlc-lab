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
            Guid.Parse("6f1c2d8e-0b7a-4c55-9e1f-3a2b4c5d6e7f"),
            DeviceCommandType.CollectDiagnostics,
            "case-1042",
            [DiagnosticsSection.Environment, DiagnosticsSection.Service]));
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
