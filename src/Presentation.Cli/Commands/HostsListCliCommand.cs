namespace BridgingIT.DevKit.Cli;

using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

/// <summary>
/// Lists discovered DevKit host descriptors.
/// </summary>
public class HostsListCliCommand : CliGroupedConsoleCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HostsListCliCommand" /> class.
    /// </summary>
    public HostsListCliCommand()
        : this("list", "Lists discovered DevKit hosts")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HostsListCliCommand" /> class.
    /// </summary>
    /// <param name="name">The command name.</param>
    /// <param name="description">The command description.</param>
    protected HostsListCliCommand(string name, string description)
        : base("hosts", name, description)
    {
    }

    /// <summary>
    /// Gets or sets a value indicating whether all descriptors should be included.
    /// </summary>
    [ConsoleCommandOption("all", Description = "Include stale descriptors and descriptors outside the current workspace.")]
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the required feature endpoint name.
    /// </summary>
    [ConsoleCommandOption("feature", Description = "Filter by advertised feature endpoint.")]
    public string Feature { get; set; }

    /// <inheritdoc />
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var context = services.GetRequiredService<CliRuntimeContext>();
        var cliConsole = services.GetRequiredService<CliConsole>();
        var discovery = services.GetRequiredService<HostRuntimeDiscovery>();
        var hosts = discovery.Discover(context.Workspace, this.IncludeAll, this.Feature)
            .Where(host => this.IncludeAll || host.Status == HostRuntimeStatus.Ready)
            .ToArray();

        if (context.Output.IsJson)
        {
            cliConsole.WriteJson(new { workspacePath = context.Workspace.Path, hosts = hosts.Select(HostRuntimeJson.Map), exitCode = 0 });
            return Task.CompletedTask;
        }

        HostRuntimeTable.Write(console, hosts);
        if (hosts.Length == 0)
        {
            console.WriteLine(this.IncludeAll
                ? "No DevKit host descriptors were found."
                : "No running DevKit hosts were found for this workspace. Use '--all' to include stale descriptors.");
        }

        return Task.CompletedTask;
    }
}
