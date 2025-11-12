// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

public class QuitConsoleCommand : ConsoleCommandBase
{
    /// <summary>
    /// Signals the application lifetime to stop (graceful shutdown).
    /// Usage: <c>quit</c>
    /// Aliases: <c>exit</c>, <c>q</c>
    /// </summary>
    public QuitConsoleCommand() : base("quit", "Stop the server", "exit", "q") { }
    /// <summary>Stops application execution.</summary>
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        console.MarkupLine("[red]Stopping...[/]");
        services.GetRequiredService<IHostApplicationLifetime>().StopApplication();

        return Task.CompletedTask;
    }
}
// === end diagnostic additions ===