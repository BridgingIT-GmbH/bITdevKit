// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;
using System.Collections.Generic;

/// <summary>
/// Lists or changes the native console theme used by the prompt and console log sink.
/// </summary>
/// <example>
/// <code>
/// console theme
/// console theme matrix
/// console theme --name carbon
/// </code>
/// </example>
public class ConsoleThemeConsoleCommand : ConsoleCommandBase, IGroupedConsoleCommand
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleThemeConsoleCommand"/> class.
    /// </summary>
    /// <example>
    /// <code>
    /// var command = new ConsoleThemeConsoleCommand();
    /// </code>
    /// </example>
    public ConsoleThemeConsoleCommand() : base("theme", "List or change the native console theme")
    {
    }

    /// <inheritdoc />
    public string GroupName => "console";

    /// <inheritdoc />
    public IReadOnlyCollection<string> GroupAliases => ["term"];

    /// <inheritdoc />
    public override bool IsWebConsoleEnabled => false;

    /// <summary>
    /// Gets or sets the theme name to apply.
    /// </summary>
    /// <example>
    /// <code>
    /// console theme matrix
    /// </code>
    /// </example>
    [ConsoleCommandArgument(0, Description = "Theme name to apply")]
    public string Theme { get; set; }

    /// <summary>
    /// Gets or sets the theme name to apply.
    /// </summary>
    /// <example>
    /// <code>
    /// console theme --name carbon
    /// </code>
    /// </example>
    [ConsoleCommandOption("name", Alias = "n", Description = "Theme name to apply")]
    public string ThemeName { get; set; }

    /// <inheritdoc />
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var requested = FirstNonEmpty(this.ThemeName, this.Theme);
        if (!string.IsNullOrWhiteSpace(requested))
        {
            if (!ConsoleTheme.Set(requested))
            {
                console.MarkupLine($"[yellow]Unknown console theme:[/] {Markup.Escape(requested)}");
                WriteThemeTable(console);
                return Task.CompletedTask;
            }

            var current = ConsoleTheme.Current;
            console.MarkupLine($"[{current.AccentStyle}]Console theme set to {Markup.Escape(current.DisplayName)}.[/]");
        }

        WriteThemeTable(console);
        // console.MarkupLine($"[grey]Theme file:[/] {Markup.Escape(ConsoleTheme.FilePath)}");

        return Task.CompletedTask;
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static void WriteThemeTable(IAnsiConsole console)
    {
        var current = ConsoleTheme.Current;
        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Theme");
        table.AddColumn("Prompt");
        table.AddColumn("Logs");

        foreach (var theme in ConsoleThemeRegistry.All)
        {
            var marker = string.Equals(theme.Name, current.Name, StringComparison.OrdinalIgnoreCase) ? "*" : " ";
            table.AddRow(
                $"{marker} {Markup.Escape(theme.Name)}",
                $"[{theme.PromptStyle}]> [/]",
                $"[{theme.InformationStyle}]info[/] [{theme.WarningStyle}]warn[/] [{theme.ErrorStyle}]error[/]");
        }

        console.Write(table);
    }
}
