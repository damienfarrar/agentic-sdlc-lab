namespace Lab.ClientService.Commands;

/// <summary>A poll-interval command that passed <see cref="PollIntervalValidator"/>: the interval is within bounds.</summary>
public sealed record ValidPollIntervalCommand(Guid Id, TimeSpan Interval) : ValidCommand(Id);
