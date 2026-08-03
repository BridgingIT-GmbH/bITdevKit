// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Lists available console commands and writes detailed help for a selected command or group.
/// </summary>
/// <example>
/// <code>
/// help
/// help storage
/// help storage blobs
/// help --filter storage
/// </code>
/// </example>
public class HelpConsoleCommand : ConsoleCommandBase
{
    /// <summary>
    /// Gets or sets the command or group name for detailed help.
    /// </summary>
    /// <example>
    /// <code>
    /// help storage
    /// </code>
    /// </example>
    [ConsoleCommandArgument(0, Description = "Command or group name for detailed help", Required = false)]
    public string Target { get; set; }

    /// <summary>
    /// Gets or sets the subcommand name when the first argument is a group.
    /// </summary>
    /// <example>
    /// <code>
    /// help storage blobs
    /// </code>
    /// </example>
    [ConsoleCommandArgument(1, Description = "Subcommand name when first arg is a group", Required = false)]
    public string Sub { get; set; }

    /// <summary>
    /// Gets or sets a text filter applied to command names, aliases, groups, and descriptions.
    /// </summary>
    /// <example>
    /// <code>
    /// help --filter storage
    /// </code>
    /// </example>
    [ConsoleCommandOption("filter", Alias = "f", Description = "Filter command names, groups, aliases, and descriptions")]
    public string Filter { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HelpConsoleCommand" /> class.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddTransient&lt;IConsoleCommand, HelpConsoleCommand&gt;();
    /// </code>
    /// </example>
    public HelpConsoleCommand()
        : base("help", "List available commands", "?")
    {
    }

    /// <inheritdoc />
    public override Task ExecuteAsync(
        IAnsiConsole console,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        var commands = services.GetServices<IConsoleCommand>()
            .OrderBy(command => command is IGroupedConsoleCommand grouped ? grouped.GroupName : string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(this.Target))
        {
            this.WriteTargetHelp(console, commands);
            return Task.CompletedTask;
        }

        this.WriteCommandList(console, this.ApplyFilter(commands));
        return Task.CompletedTask;
    }

    private void WriteTargetHelp(IAnsiConsole console, IReadOnlyCollection<IConsoleCommand> commands)
    {
        if (!string.IsNullOrWhiteSpace(this.Sub))
        {
            var specific = commands.OfType<IGroupedConsoleCommand>()
                .FirstOrDefault(command => GroupMatches(command, this.Target) && CommandMatches(command, this.Sub));

            if (specific is not null)
            {
                ConsoleCommandBinder.WriteHelp(console, specific, detailed: true);
                return;
            }

            console.MarkupLine($"[yellow]No such subcommand:[/] {Markup.Escape(this.Target)} {Markup.Escape(this.Sub)}");
            this.WriteGroupHelp(console, commands);
            return;
        }

        var direct = commands.FirstOrDefault(command => command is not IGroupedConsoleCommand && command.Matches(this.Target));
        if (direct is not null)
        {
            ConsoleCommandBinder.WriteHelp(console, direct, detailed: true);
            return;
        }

        if (this.WriteGroupHelp(console, commands))
        {
            return;
        }

        var filtered = this.ApplyFilter(commands, this.Target).ToList();
        if (filtered.Count != 0)
        {
            console.MarkupLine($"[grey]No exact command or group named '{Markup.Escape(this.Target)}'. Showing matching commands instead.[/]");
            this.WriteCommandList(console, filtered);
            return;
        }

        console.MarkupLine($"[yellow]No such command or group:[/] {Markup.Escape(this.Target)}");
    }

    private bool WriteGroupHelp(IAnsiConsole console, IReadOnlyCollection<IConsoleCommand> commands)
    {
        var grouped = commands.OfType<IGroupedConsoleCommand>()
            .Where(command => GroupMatches(command, this.Target))
            .Cast<IConsoleCommand>()
            .ToList();

        if (grouped.Count == 0)
        {
            return false;
        }

        var groupName = ((IGroupedConsoleCommand)grouped[0]).GroupName;
        this.WriteGroupedTable(console, groupName, grouped, showGroup: false);
        console.MarkupLine($"[grey]Use[/] help {Markup.Escape(groupName)} <command> [grey]for detailed help.[/]");
        return true;
    }

    private void WriteCommandList(IAnsiConsole console, IReadOnlyCollection<IConsoleCommand> commands)
    {
        var regularCommands = commands
            .Where(command => command is not IGroupedConsoleCommand)
            .ToList();

        var groupedCommands = commands
            .OfType<IGroupedConsoleCommand>()
            .GroupBy(command => command.GroupName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totalCount = regularCommands.Count + groupedCommands.Sum(group => group.Count());
        var filterText = string.IsNullOrWhiteSpace(this.Filter) ? string.Empty : $" matching '{Markup.Escape(this.Filter)}'";
        console.MarkupLine($"[bold]Available commands[/] [grey]({totalCount}{filterText})[/]");

        if (totalCount == 0)
        {
            console.MarkupLine("[yellow]No commands matched.[/]");
            return;
        }

        if (regularCommands.Count != 0)
        {
            this.WriteGroupedTable(console, "Commands", regularCommands, showGroup: false);
        }

        foreach (var group in groupedCommands)
        {
            this.WriteGroupedTable(console, group.Key, group.Cast<IConsoleCommand>().ToList(), showGroup: true);
        }

        console.MarkupLine("[grey]Use[/] help <command>, help <group>, help <group> <command>, [grey]or[/] help --filter <text>.");
    }

    private void WriteGroupedTable(IAnsiConsole console, string title, IReadOnlyCollection<IConsoleCommand> commands, bool showGroup)
    {
        var table = new Table()
            .Border(TableBorder.Minimal)
            .Title($"[bold cyan]{Markup.Escape(title)}[/]");

        table.AddColumn(showGroup ? "Command" : "Name");
        table.AddColumn("Aliases");
        table.AddColumn("Description");

        foreach (var command in commands.OrderBy(command => command.Name, StringComparer.OrdinalIgnoreCase))
        {
            table.AddRow(
                Markup.Escape(FormatCommandName(command, showGroup)),
                Markup.Escape(FormatAliases(command)),
                Markup.Escape(command.Description ?? string.Empty));
        }

        console.Write(table);
    }

    private IReadOnlyCollection<IConsoleCommand> ApplyFilter(IReadOnlyCollection<IConsoleCommand> commands, string filter = null)
    {
        var effectiveFilter = string.IsNullOrWhiteSpace(filter) ? this.Filter : filter;
        if (string.IsNullOrWhiteSpace(effectiveFilter))
        {
            return commands;
        }

        return commands
            .Where(command => MatchesFilter(command, effectiveFilter))
            .ToList();
    }

    private static bool MatchesFilter(IConsoleCommand command, string filter)
    {
        if (Contains(command.Name, filter)
            || Contains(command.Description, filter)
            || command.Aliases.Any(alias => Contains(alias, filter)))
        {
            return true;
        }

        return command is IGroupedConsoleCommand grouped
            && (Contains(grouped.GroupName, filter) || grouped.GroupAliases.Any(alias => Contains(alias, filter)));
    }

    private static bool CommandMatches(IConsoleCommand command, string value) =>
        command.Matches(value);

    private static bool GroupMatches(IGroupedConsoleCommand command, string value) =>
        string.Equals(command.GroupName, value, StringComparison.OrdinalIgnoreCase)
        || command.GroupAliases.Any(alias => string.Equals(alias, value, StringComparison.OrdinalIgnoreCase));

    private static bool Contains(string value, string filter) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains(filter, StringComparison.OrdinalIgnoreCase);

    private static string FormatCommandName(IConsoleCommand command, bool showGroup)
    {
        if (showGroup && command is IGroupedConsoleCommand grouped)
        {
            return $"{grouped.GroupName} {command.Name}";
        }

        return command.Name;
    }

    private static string FormatAliases(IConsoleCommand command)
    {
        var aliases = new List<string>();

        if (command is IGroupedConsoleCommand grouped)
        {
            aliases.AddRange(grouped.GroupAliases
                .Where(alias => !string.Equals(alias, grouped.GroupName, StringComparison.OrdinalIgnoreCase))
                .Select(alias => $"{alias} {command.Name}"));
        }

        aliases.AddRange(command.Aliases
            .Where(alias => !string.Equals(alias, command.Name, StringComparison.OrdinalIgnoreCase)));

        return string.Join(", ", aliases.Distinct(StringComparer.OrdinalIgnoreCase));
    }
}
