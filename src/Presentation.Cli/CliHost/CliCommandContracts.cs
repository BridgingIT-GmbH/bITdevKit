namespace BridgingIT.DevKit.Cli;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Defines a command module that can register CLI services and commands.
/// </summary>
public interface ICliCommandModule
{
    /// <summary>
    /// Gets the module name shown in CLI help and version output.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the module description shown in CLI help and version output.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Registers services required by the command module.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="context">The module registration context.</param>
    void RegisterServices(IServiceCollection services, CliCommandModuleContext context);

    /// <summary>
    /// Registers commands exposed by the module.
    /// </summary>
    /// <param name="registry">The command registry.</param>
    void RegisterCommands(ICliCommandRegistry registry);
}

/// <summary>
/// Records commands and groups registered by CLI command modules.
/// </summary>
public interface ICliCommandRegistry
{
    /// <summary>
    /// Registers a command type.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    void WithCommand<TCommand>()
        where TCommand : class;

    /// <summary>
    /// Registers a command type.
    /// </summary>
    /// <param name="commandType">The command type.</param>
    void WithCommand(Type commandType);

    /// <summary>
    /// Registers a named command group.
    /// </summary>
    /// <param name="name">The group name.</param>
    /// <param name="description">The group description.</param>
    /// <param name="configure">The group command registration callback.</param>
    void AddGroup(string name, string description, Action<ICliCommandRegistry> configure);
}

/// <summary>
/// Provides shared context to CLI command modules during registration.
/// </summary>
public sealed class CliCommandModuleContext
{
    /// <summary>
    /// Gets the resolved workspace context.
    /// </summary>
    public CliWorkspaceContext Workspace { get; init; }

    /// <summary>
    /// Gets the output settings for the current invocation.
    /// </summary>
    public CliOutputSettings Output { get; init; }

    /// <summary>
    /// Gets the host registry options.
    /// </summary>
    public HostRegistryOptions HostRegistry { get; init; }

    /// <summary>
    /// Gets environment variables visible to the CLI.
    /// </summary>
    public IDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
