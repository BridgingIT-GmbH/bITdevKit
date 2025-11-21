// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;
using System.Threading.Tasks;

public class LogLevelSetConsoleCommand : LogLevelGroupConsoleCommandBase
{
    [ConsoleCommandArgument(0, Description = "Log level (Verbose/Debug/Information/Warning/Error/Fatal)", Required = true)]
    public string Level { get; set; }

    public LogLevelSetConsoleCommand() : base("set", "Change log level") { }

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