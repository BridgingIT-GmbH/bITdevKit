namespace BridgingIT.DevKit.Cli;

using System.Reflection;
using BridgingIT.DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

/// <summary>
/// Shows CLI version information and registered command modules.
/// </summary>
public sealed class VersionCliCommand() : ConsoleCommandBase("version", "Shows CLI version information")
{
    /// <inheritdoc />
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var context = services.GetRequiredService<CliRuntimeContext>();
        var cliConsole = services.GetRequiredService<CliConsole>();
        var assembly = typeof(CliApplication).Assembly;
        var version = assembly.GetName().Version?.ToString() ?? "0.0.0";
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? version;
        var modules = services.GetRequiredService<IReadOnlyList<CliModuleInfo>>();

        if (context.Output.IsJson)
        {
            cliConsole.WriteJson(new { version, informationalVersion, modules, exitCode = 0 });
            return Task.CompletedTask;
        }

        console.MarkupLine($"[bold]bdk[/] {CliConsole.Escape(informationalVersion)}");
        var table = new Table().Border(TableBorder.Minimal).AddColumn("Module").AddColumn("Description");
        foreach (var module in modules)
        {
            table.AddRow(module.Name, module.Description);
        }

        console.Write(table);
        return Task.CompletedTask;
    }
}
