namespace Lab.Tests.ClientService;

/// <summary>
/// Minimal controllable clock. Subclassing the BCL's TimeProvider avoids pulling in
/// Microsoft.Extensions.TimeProvider.Testing for the one method we need.
/// </summary>
internal sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
