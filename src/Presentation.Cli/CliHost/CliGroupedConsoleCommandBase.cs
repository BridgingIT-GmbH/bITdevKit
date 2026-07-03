namespace BridgingIT.DevKit.Cli;

using BridgingIT.DevKit.Presentation;

/// <summary>
/// Base class for grouped CLI commands that use the existing Console Commands model.
/// </summary>
public abstract class CliGroupedConsoleCommandBase(string groupName, string name, string description, params string[] aliases)
    : ConsoleCommandBase(name, description, aliases), IGroupedConsoleCommand
{
    /// <inheritdoc />
    public string GroupName { get; } = groupName;

    /// <inheritdoc />
    public IReadOnlyCollection<string> GroupAliases { get; } = [];
}