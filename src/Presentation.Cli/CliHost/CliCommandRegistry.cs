namespace BridgingIT.DevKit.Cli;

using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Records CLI command registrations supplied by command modules.
/// </summary>
public sealed class CliCommandRegistry : ICliCommandRegistry
{
    private readonly List<Type> commandTypes = [];
    private readonly List<CliCommandGroupInfo> groups = [];

    /// <summary>
    /// Gets the registered command implementation types.
    /// </summary>
    public IReadOnlyList<Type> CommandTypes => this.commandTypes;

    /// <summary>
    /// Gets the registered command group metadata.
    /// </summary>
    public IReadOnlyList<CliCommandGroupInfo> Groups => this.groups;

    /// <inheritdoc />
    public void WithCommand<TCommand>()
        where TCommand : class
        => this.WithCommand(typeof(TCommand));

    /// <inheritdoc />
    public void WithCommand(Type commandType)
    {
        if (!typeof(IConsoleCommand).IsAssignableFrom(commandType))
        {
            throw new ArgumentException($"Command type '{commandType.FullName}' must implement IConsoleCommand.", nameof(commandType));
        }

        if (!this.commandTypes.Contains(commandType))
        {
            this.commandTypes.Add(commandType);
        }
    }

    /// <inheritdoc />
    public void AddGroup(string name, string description, Action<ICliCommandRegistry> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        var nested = new CliCommandRegistry();
        configure(nested);
        foreach (var commandType in nested.CommandTypes)
        {
            this.WithCommand(commandType);
        }

        this.groups.Add(new CliCommandGroupInfo(name, description, nested.CommandTypes));
    }

    /// <summary>
    /// Registers all recorded commands with the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public void RegisterCommands(IServiceCollection services)
    {
        foreach (var commandType in this.commandTypes)
        {
            services.AddTransient(typeof(IConsoleCommand), commandType);
        }
    }
}

/// <summary>
/// Describes commands registered under a CLI command group.
/// </summary>
/// <param name="Name">The group name.</param>
/// <param name="Description">The group description.</param>
/// <param name="CommandTypes">The commands registered in the group.</param>
public sealed record CliCommandGroupInfo(string Name, string Description, IReadOnlyList<Type> CommandTypes);