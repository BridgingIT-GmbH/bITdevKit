// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;
using System.Threading.Tasks;

/// <summary>
/// Console command that lists all available Serilog log levels and highlights the current one.
/// </summary>
public class LogLevelListConsoleCommand : LogLevelGroupConsoleCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LogLevelListConsoleCommand"/> class.
    /// </summary>
    public LogLevelListConsoleCommand() : base("list", "List available log levels") { }

    /// <summary>
    /// Executes the command: builds and renders a table of log levels with descriptions.
    /// </summary>
    /// <param name="console">The Spectre console used for output.</param>
    /// <param name="services">Service provider (unused here but available for extensibility).</param>
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        await this.ExecuteWithLogLevelManagerAsync(console, async manager =>
        {
            var table = new Table()
                .Border(TableBorder.Minimal)
                //.Title("[bold cyan]Available Log Levels[/]")
                .AddColumn("Level")
                .AddColumn("Description")
                .AddColumn("Current");

            foreach (var level in manager.AvailableLevels)
            {
                var isCurrent = level == manager.CurrentLevel ? "[green]*[/]" : "";
                table.AddRow(
                    level.ToString(),
                    manager.GetLevelDescription(level),
                    isCurrent);
            }

            console.Write(table);
            await Task.CompletedTask;
        });
    }
}
