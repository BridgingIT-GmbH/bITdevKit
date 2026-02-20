// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;
using System.Threading.Tasks;

/// <summary>
/// Console command that displays the current Serilog log level.
/// </summary>
public class LogLevelGetConsoleCommand : LogLevelGroupConsoleCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LogLevelGetConsoleCommand"/> class.
    /// </summary>
    public LogLevelGetConsoleCommand() : base("get", "Show current log level") { }

    /// <summary>
    /// Executes the command: writes the current log level and its description to the console.
    /// </summary>
    /// <param name="console">The Spectre console used for output.</param>
    /// <param name="services">Service provider (unused here but available for extensibility).</param>
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        await this.ExecuteWithLogLevelManagerAsync(console, async manager =>
        {
            var currentLevel = manager.CurrentLevel;
            var description = manager.GetLevelDescription(currentLevel);

            console.MarkupLine($"[bold]Current Log Level:[/] {currentLevel}");
            console.MarkupLine($"[grey]{description}[/]");

            await Task.CompletedTask;
        });
    }
}
