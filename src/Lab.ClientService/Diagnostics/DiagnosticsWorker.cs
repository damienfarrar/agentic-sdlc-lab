using System.Text.Json;
using Lab.ClientService.Commands;
using Microsoft.Extensions.Options;

namespace Lab.ClientService.Diagnostics;

/// <summary>
/// Polls the platform for commands and runs every allowlisted type (diagnostics, poll interval). Each tick's
/// failures are logged and swallowed: an unhandled exception in a BackgroundService stops the host, and one bad
/// command mustn't take the service down. An accepted poll interval lives only as long as this worker, so a
/// restart goes back to the configured one.
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
            await PollOnceAsync(timer, stoppingToken).ConfigureAwait(false);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task PollOnceAsync(PeriodicTimer timer, CancellationToken stoppingToken)
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

        if (!CommandParser.TryParse(command, PollIntervalBounds.From(options.Value), out var valid, out var rejection))
        {
            LogCommandRejected(logger, command.Id, rejection);
            return;
        }

        switch (valid)
        {
            case ValidDiagnosticsCommand diagnostics:
                WriteBundle(diagnostics);
                break;
            case ValidPollIntervalCommand pollInterval:
                // Setting Period reschedules the timer, so the next wait is the first at the new interval.
                var oldInterval = timer.Period;
                timer.Period = pollInterval.Interval;
                LogPollIntervalChanged(logger, pollInterval.Id, oldInterval, pollInterval.Interval);
                break;
        }
    }

    private void WriteBundle(ValidDiagnosticsCommand command)
    {
        try
        {
            var path = collector.Write(command);
            LogBundleWritten(logger, command.Id, path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogBundleFailed(logger, command.Id, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not claim a command from the platform")]
    private static partial void LogClaimFailed(ILogger logger, Exception exception);

    // Logs the id and a fixed reason, never the command's strings, so a hostile command can't forge log lines.
    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected command {CommandId}: {Reason}")]
    private static partial void LogCommandRejected(ILogger logger, Guid commandId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Command {CommandId}: diagnostics written to {Path}")]
    private static partial void LogBundleWritten(ILogger logger, Guid commandId, string path);

    // Logs only validated values: the interval passed the bounds check before it gets here.
    [LoggerMessage(Level = LogLevel.Information, Message = "Command {CommandId}: poll interval changed from {OldInterval} to {NewInterval}")]
    private static partial void LogPollIntervalChanged(ILogger logger, Guid commandId, TimeSpan oldInterval, TimeSpan newInterval);

    [LoggerMessage(Level = LogLevel.Error, Message = "Command {CommandId}: could not write diagnostics")]
    private static partial void LogBundleFailed(ILogger logger, Guid commandId, Exception exception);
}
