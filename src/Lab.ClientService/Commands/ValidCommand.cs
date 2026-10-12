namespace Lab.ClientService.Commands;

/// <summary>
/// A command that passed validation. Only a subtype of this reaches code that acts on a command.
/// </summary>
public abstract record ValidCommand(Guid Id);
