// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;
using System.Threading.Tasks;

/// <summary>
/// Console command that changes the active Serilog log level at runtime.
/// </summary>
public class LogLevelSetConsoleCommand : LogLevelGroupConsoleCommandBase
{
    /// <summary>
    /// Gets or sets the target log level name (Verbose, Debug, Information, Warning, Error, Fatal).
    /// </summary>
    [ConsoleCommandArgument(0, Description = "Log level (Verbose/Debug/Information/Warning/Error/Fatal)", Required = true)]
    public string Level { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LogLevelSetConsoleCommand"/> class.
    /// </summary>
    public LogLevelSetConsoleCommand() : base("set", "Change log level") { }

    /// <summary>
    /// Executes the command: attempts to change the log level and reports success or validation errors.
    /// </summary>
    /// <param name="console">The Spectre console used for output.</param>
    /// <param name="services">Service provider (unused here but available for extensibility).
    /// </param>
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        await this.ExecuteWithLogLevelManagerAsync(console, async manager =>
        {
            try
            {
                var oldLevel = manager.CurrentLevel;
                manager.SetLevel(this.Level);
                var newLevel = manager.CurrentLevel;

                console.MarkupLine($"Log level changed from [bold]{oldLevel}[/] to [bold]{newLevel}[/]");
                console.MarkupLine($"[grey]{manager.GetLevelDescription(newLevel)}[/]");
            }
            catch (ArgumentException ex)
            {
                console.MarkupLine($"[red]Error:[/] {ex.Message}");
            }

            await Task.CompletedTask;
        });
    }
}