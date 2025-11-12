// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using System.Collections.Generic;
using System.Linq;

public class HelpConsoleCommand : ConsoleCommandBase
{
    [ConsoleCommandArgument(0, Description = "Command or group name for detailed help", Required = false)] public string Target { get; set; }
    [ConsoleCommandArgument(1, Description = "Subcommand name (when first arg is a group)", Required = false)] public string Sub { get; set; }

    /// <summary>
    /// Lists all available commands or detailed help for a specific command.
    /// Usage: <c>help</c> | <c>help &lt;command&gt;</c>
    /// Alias: <c>?</c>
    /// Example: <c>help mem</c>
    /// </summary>
    public HelpConsoleCommand() : base("help", "List available commands", "?") { }

    /// <summary>Executes help listing.</summary>
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var commands = services.GetServices<IConsoleCommand>().ToList();
        if (!string.IsNullOrWhiteSpace(this.Target))
        {
            // When both group and sub are provided attempt specific subcommand help first.
            if (!string.IsNullOrWhiteSpace(this.Sub))
            {
                var specific = commands.OfType<IGroupedConsoleCommand>()
                    .Where(g => string.Equals(g.GroupName, this.Target, StringComparison.OrdinalIgnoreCase)
                        || g.GroupAliases?.Any(a => string.Equals(a, this.Target, StringComparison.OrdinalIgnoreCase)) == true)
                    .FirstOrDefault(c => string.Equals(c.Name, this.Sub, StringComparison.OrdinalIgnoreCase) || c.Aliases.Any(a => string.Equals(a, this.Sub, StringComparison.OrdinalIgnoreCase)));
                if (specific is not null) { ConsoleCommandBinder.WriteHelp(console, specific, detailed: true); return Task.CompletedTask; }
            }
            // Try regular (non-group) command match
            var direct = commands.FirstOrDefault(c => c.Matches(this.Target));
            if (direct is not null && string.IsNullOrWhiteSpace(this.Sub)) { ConsoleCommandBinder.WriteHelp(console, direct, detailed: true); return Task.CompletedTask; }
            // Group listing (if Target matches a group)
            var grouped = commands.OfType<IGroupedConsoleCommand>()
                .Where(g => string.Equals(g.GroupName, this.Target, StringComparison.OrdinalIgnoreCase)
                    || g.GroupAliases?.Any(a => string.Equals(a, this.Target, StringComparison.OrdinalIgnoreCase)) == true)
                .Cast<IConsoleCommand>().ToList();
            if (grouped.Count != 0)
            {
                if (!string.IsNullOrWhiteSpace(this.Sub))
                {
                    console.MarkupLine($"[yellow]No such subcommand:[/] {this.Target} {this.Sub}");
                }

                var tbl = new Table().Border(TableBorder.Minimal).Title($"[bold cyan]{this.Target} (group) subcommands[/]");
                tbl.AddColumn("Sub"); tbl.AddColumn("Description");
                foreach (var sc in grouped.OrderBy(c => c.Name))
                {
                    tbl.AddRow(sc.Name, sc.Description);
                }

                console.Write(tbl); return Task.CompletedTask;
            }
            // Fallback unknown
            console.MarkupLine("[yellow]No such command or group:[/] " + this.Target);
        }
        // List all commands sorted by group then name
        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Command/Group"); table.AddColumn("Aliases"); table.AddColumn("Description");
        var ordered = commands
            .OrderBy(c => c is IGroupedConsoleCommand gc ? gc.GroupName : "")
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var c in ordered)
        {
            if (c is IGroupedConsoleCommand gc)
            {
                var aliasList = gc.GroupAliases?.Where(a => !string.Equals(a, gc.GroupName, StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<string>();
                var cmdAliases = c.Aliases.Where(a => !string.Equals(a, c.Name, StringComparison.OrdinalIgnoreCase));
                var allAliases = aliasList.Concat(cmdAliases).Distinct(StringComparer.OrdinalIgnoreCase);
                table.AddRow(gc.GroupName + " " + c.Name, string.Join(", ", allAliases), c.Description);
            }
            else
            {
                var aliases = c.Aliases.Count != 0 ? string.Join(", ", c.Aliases.Where(a => !string.Equals(a, c.Name, StringComparison.OrdinalIgnoreCase))) : string.Empty;
                table.AddRow(c.Name, aliases, c.Description);
            }
        }
        console.Write(table); return Task.CompletedTask;
    }
}
// === end diagnostic additions ===