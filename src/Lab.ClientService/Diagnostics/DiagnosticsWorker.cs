using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Lab.ClientService.Diagnostics;

/// <summary>
/// Polls the platform for commands and runs them. Each tick's failures are logged and swallowed: an unhandled
/// exception in a BackgroundService stops the host, and one bad command mustn't take the service down.
/// </summary>
public sealed partial class DiagnosticsWorker(
    PlatformCommandClient client,
    DiagnosticsCollector collector,
    IOptions<DiagnosticsOptions> options,
    IOptions<HeartbeatOptions> heartbeatOptions,
    TimeProvider timeProvider,
    ILogger<DiagnosticsWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.PollInterval, timeProvider);

        do
        {
            await PollOnceAsync(stoppingToken).ConfigureAwait(false);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task PollOnceAsync(CancellationToken stoppingToken)
    {
        DeviceCommandDto? command;
        try
        {
            command = await client.ClaimNextAsync(heartbeatOptions.Value.DeviceId, stoppingToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            LogClaimFailed(logger, ex);
            return;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            LogClaimFailed(logger, ex);
            return;
        }
        catch (OperationCanceledException ex) when (!stoppingToken.IsCancellationRequested)
        {
            // HttpClient.Timeout surfaces as a cancellation; only the host's token means "stop".
            LogClaimFailed(logger, ex);
            return;
        }

        if (command is null)
        {
            return;
        }

        if (!CommandValidator.TryValidate(command, out var valid, out var rejection))
        {
            LogCommandRejected(logger, command.Id, rejection);
            return;
        }

        try
        {
            var path = collector.Write(valid);
            LogBundleWritten(logger, valid.Id, path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogBundleFailed(logger, valid.Id, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not claim a command from the platform")]
    private static partial void LogClaimFailed(ILogger logger, Exception exception);

    // Logs the id and a fixed reason, never the command's strings, so a hostile command can't forge log lines.
    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected command {CommandId}: {Reason}")]
    private static partial void LogCommandRejected(ILogger logger, Guid commandId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Command {CommandId}: diagnostics written to {Path}")]
    private static partial void LogBundleWritten(ILogger logger, Guid commandId, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Command {CommandId}: could not write diagnostics")]
    private static partial void LogBundleFailed(ILogger logger, Guid commandId, Exception exception);
}
