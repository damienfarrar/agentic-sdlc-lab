using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Lab.ClientService;
using Lab.ClientService.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lab.Tests.ClientService;

/// <summary>
/// Runs the real worker on a manual clock. Timing is asserted through <see cref="ManualTimeProvider.Advance"/>'s
/// count of timer callbacks, which is deterministic; claims and log entries are awaited with bounded waits, never
/// with sleeps.
/// </summary>
public sealed class DiagnosticsWorkerTests : IDisposable
{
    private static readonly Uri BaseAddress = new("https://platform.lab.test/");
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ConfiguredInterval = TimeSpan.FromSeconds(30);
    private const string PollIntervalCommandId = "0d3b7e52-9a41-4f6c-8b2e-7c1d5a9f3e60";

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("lab-worker-");
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero));
    private readonly RecordingLogger<DiagnosticsWorker> _logger = new();
    private readonly ConcurrentQueue<string> _responses = new();
    private readonly SemaphoreSlim _claims = new(0);
    private readonly List<PlatformCommandClient> _clients = [];

    [Fact]
    public async Task PollOnce_AcceptedInterval_AppliesFromNextWait()
    {
        _responses.Enqueue(PollIntervalJson(60));
        using var worker = CreateWorker();

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitForClaimAsync();
        Assert.NotNull(WaitForIntervalChange());

        Assert.Equal(0, _time.Advance(TimeSpan.FromSeconds(30)));
        Assert.Equal(1, _time.Advance(TimeSpan.FromSeconds(30)));
        await WaitForClaimAsync();
        Assert.Equal(1, _time.Advance(TimeSpan.FromSeconds(60)));
        await WaitForClaimAsync();

        await worker.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_AfterRestart_UsesConfiguredInterval()
    {
        _responses.Enqueue(PollIntervalJson(60));
        using (var first = CreateWorker())
        {
            await first.StartAsync(TestContext.Current.CancellationToken);
            await WaitForClaimAsync();
            Assert.NotNull(WaitForIntervalChange());
            await first.StopAsync(TestContext.Current.CancellationToken);
        }

        using var second = CreateWorker();
        await second.StartAsync(TestContext.Current.CancellationToken);
        await WaitForClaimAsync();

        Assert.Equal(1, _time.Advance(ConfiguredInterval));
        await WaitForClaimAsync();

        await second.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PollOnce_AcceptedInterval_LogsOldAndNew()
    {
        _responses.Enqueue(PollIntervalJson(60));
        using var worker = CreateWorker();

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitForClaimAsync();
        var entry = WaitForIntervalChange();

        Assert.NotNull(entry);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(Guid.Parse(PollIntervalCommandId), entry.Value("CommandId"));
        Assert.Equal(ConfiguredInterval, entry.Value("OldInterval"));
        Assert.Equal(TimeSpan.FromMinutes(1), entry.Value("NewInterval"));

        await worker.StopAsync(TestContext.Current.CancellationToken);
    }

    // The marker sits in a field the log could be tempted to echo; a CR/LF in it would forge a second log line.
    [Theory]
    [InlineData("""{"id":"0d3b7e52-9a41-4f6c-8b2e-7c1d5a9f3e60","type":"SetPollInterval","intervalSeconds":60,"outputName":"MARKER-7f3a\r\nforged entry"}""")]
    [InlineData("""{"id":"0d3b7e52-9a41-4f6c-8b2e-7c1d5a9f3e60","type":"MARKER-7f3a\r\nforged entry","intervalSeconds":60}""")]
    public async Task PollOnce_HostileCommand_LogsNoCommandText(string body)
    {
        _responses.Enqueue(body);
        using var worker = CreateWorker();

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitForClaimAsync();

        Assert.NotNull(WaitForRejection());
        Assert.All(_logger.Entries, entry =>
        {
            Assert.DoesNotContain("MARKER-7f3a", entry.Message, StringComparison.Ordinal);
            Assert.All(entry.State, pair => Assert.DoesNotContain("MARKER-7f3a", pair.Value?.ToString() ?? string.Empty, StringComparison.Ordinal));
        });

        await worker.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PollOnce_RejectedInterval_KeepsCurrentInterval()
    {
        _responses.Enqueue(PollIntervalJson(5));
        using var worker = CreateWorker();

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitForClaimAsync();
        Assert.NotNull(WaitForRejection());

        Assert.Equal(1, _time.Advance(ConfiguredInterval));
        await WaitForClaimAsync();

        await worker.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PollOnce_AfterRejection_StillRunsLaterCommands()
    {
        _responses.Enqueue(PollIntervalJson(5));
        _responses.Enqueue(PollIntervalJson(60));
        using var worker = CreateWorker();

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitForClaimAsync();
        Assert.NotNull(WaitForRejection());
        Assert.Equal(1, _time.Advance(ConfiguredInterval));
        await WaitForClaimAsync();

        var change = WaitForIntervalChange();
        Assert.NotNull(change);
        Assert.Equal(TimeSpan.FromMinutes(1), change.Value("NewInterval"));

        await worker.StopAsync(TestContext.Current.CancellationToken);
    }

    public void Dispose()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        _claims.Dispose();
        _root.Delete(recursive: true);
    }

    private static string PollIntervalJson(int seconds) =>
        $$"""{"id":"{{PollIntervalCommandId}}","type":"SetPollInterval","intervalSeconds":{{seconds}}}""";

    private DiagnosticsWorker CreateWorker()
    {
        var options = Options.Create(new DiagnosticsOptions
        {
            Enabled = true,
            PlatformBaseAddress = BaseAddress,
            PollInterval = ConfiguredInterval,
            OutputDirectory = Path.Combine(_root.FullName, "out"),
        });
        var heartbeat = Options.Create(new HeartbeatOptions { DeviceId = "LAB-DEVICE-003" });
        var client = new PlatformCommandClient(new StubHttpMessageHandler(Respond), BaseAddress);
        _clients.Add(client);

        return new DiagnosticsWorker(client, new DiagnosticsCollector(options, heartbeat, _time), options, heartbeat, _time, _logger);
    }

    // Hands out queued bodies in order, then 204s. Counts each claim so tests can wait for it.
    private HttpResponseMessage Respond()
    {
        var response = _responses.TryDequeue(out var body)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") }
            : new HttpResponseMessage(HttpStatusCode.NoContent);
        _claims.Release();
        return response;
    }

    private async Task WaitForClaimAsync() =>
        Assert.True(await _claims.WaitAsync(WaitLimit, TestContext.Current.CancellationToken), "The worker did not claim a command in time.");

    private LogEntry? WaitForIntervalChange() =>
        _logger.WaitFor(entry => entry.Value("NewInterval") is not null, WaitLimit);

    private LogEntry? WaitForRejection() =>
        _logger.WaitFor(entry => entry.Value("Reason") is not null, WaitLimit);
}
