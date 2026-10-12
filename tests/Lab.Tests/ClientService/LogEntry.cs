using Microsoft.Extensions.Logging;

namespace Lab.Tests.ClientService;

internal sealed record LogEntry(LogLevel Level, string Message, IReadOnlyList<KeyValuePair<string, object?>> State)
{
    public object? Value(string key) => State.FirstOrDefault(pair => pair.Key == key).Value;
}
