// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using System.Diagnostics;
using System.Linq;

public class RestartConsoleCommand : ConsoleCommandBase
{
    /// <summary>
    /// Restarts the current application (development only) by spawning a new process and stopping the existing one.
    /// Usage: <c>restart</c>
    /// Aliases: <c>re</c>, <c>reload</c>
    /// </summary>
    public RestartConsoleCommand() : base("restart", "Restart the application (development only)", "re") { }
    /// <summary>Performs application restart procedure.</summary>
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var env = services.GetRequiredService<IWebHostEnvironment>();
        if (!env.IsDevelopment()) { console.MarkupLine("[red]Restart denied: not a development environment.[/]"); return Task.CompletedTask; }
        const string markerVar = "BITDEVKIT_RESTARTING";
        var alreadyRestarting = Environment.GetEnvironmentVariable(markerVar) == "1";
        if (alreadyRestarting) { console.MarkupLine("[yellow]Restart already in progress.[/]"); return Task.CompletedTask; }
        Environment.SetEnvironmentVariable(markerVar, "1");
        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe)) { console.MarkupLine("[red]Cannot determine executable path.[/]"); return Task.CompletedTask; }
        var argList = Environment.GetCommandLineArgs().Skip(1).Select(a => a.Contains(' ') ? $"\"{a}\"" : a);
        var argsLine = string.Join(' ', argList);
        try
        {
            console.MarkupLine("[yellow]Spawning new instance...[/]");
            var startInfo = new ProcessStartInfo(exe, argsLine) { UseShellExecute = false };
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            console.MarkupLine("[red]Failed to spawn new instance:[/] " + ex.Message);
            Environment.SetEnvironmentVariable(markerVar, null);
            return Task.CompletedTask;
        }
        console.MarkupLine("[grey]Stopping current instance...[/]");
        services.GetRequiredService<IHostApplicationLifetime>().StopApplication();

        return Task.CompletedTask;
    }
}
// === end diagnostic additions ===