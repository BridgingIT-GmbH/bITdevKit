// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;
using System.Threading.Tasks;

public class LogLevelGetConsoleCommand : LogLevelGroupConsoleCommandBase
{
    public LogLevelGetConsoleCommand() : base("get", "Show current log level") { }

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
