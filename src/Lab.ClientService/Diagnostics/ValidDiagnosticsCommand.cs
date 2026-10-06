namespace Lab.ClientService.Diagnostics;

/// <summary>A command that passed <see cref="CommandValidator"/>. Only this type reaches the file system.</summary>
public sealed record ValidDiagnosticsCommand(Guid Id, string FileName, IReadOnlyList<DiagnosticsSection> Sections);
