namespace BridgingIT.DevKit.Cli;

using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

/// <summary>
/// Deletes stale, invalid, or unreachable host descriptors.
/// </summary>
public sealed class HostsCleanCliCommand() : CliGroupedConsoleCommandBase("hosts", "clean", "Removes stale host descriptors")
{
    /// <summary>
    /// Gets or sets a value indicating whether stale descriptors should be removed without prompting.
    /// </summary>
    [ConsoleCommandOption("yes", Alias = "y", Description = "Remove stale descriptors without interactive confirmation.")]
    public bool Yes { get; set; }

    /// <inheritdoc />
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var context = services.GetRequiredService<CliRuntimeContext>();
        var cliConsole = services.GetRequiredService<CliConsole>();
        var discovery = services.GetRequiredService<HostRuntimeDiscovery>();
        var hosts = discovery.Discover(context.Workspace, includeAll: true);
        var removable = hosts.Where(host => host.Status is HostRuntimeStatus.Invalid or HostRuntimeStatus.Stale or HostRuntimeStatus.Unreachable).ToArray();

        var removed = new List<object>();
        var skipped = new List<object>();

        if (this.Yes)
        {
            foreach (var host in removable)
            {
                try
                {
                    File.Delete(host.DescriptorPath);
                    removed.Add(new { path = host.DescriptorPath, reason = host.Status.ToString() });
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    skipped.Add(new { path = host.DescriptorPath, reason = exception.Message });
                }
            }
        }
        else
        {
            skipped.AddRange(removable.Select(host => new { path = host.DescriptorPath, reason = "ConfirmationRequired" }));
        }

        if (context.Output.IsJson)
        {
            cliConsole.WriteJson(new
            {
                workspacePath = context.Workspace.Path,
                confirmed = this.Yes,
                removed,
                skipped,
                candidates = removable.Select(HostRuntimeJson.Map),
                exitCode = 0
            });
            return Task.CompletedTask;
        }

        console.WriteLine(this.Yes
            ? $"Removed {removed.Count} descriptor(s). Skipped {skipped.Count}."
            : $"Use 'bdk hosts clean --yes' to remove {removable.Length} stale, invalid, or unreachable descriptor(s).");
        return Task.CompletedTask;
    }
}
