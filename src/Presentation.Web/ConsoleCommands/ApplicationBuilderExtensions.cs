// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Presentation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using System.Reflection;

/// <summary>
/// Provides an interactive command-based console that runs inside a locally hosted Kestrel <see cref="WebApplication"/>.
/// </summary>
public static partial class ApplicationBuilderExtensions
{
    /// <summary>
    /// Enables the interactive console loop in a web application when running locally.
    /// </summary>
    /// <param name="app">The web application instance.</param>
    /// <param name="startupDelay">Optional delay before starting the loop.</param>
    public static void UseConsoleCommandsInteractive(this WebApplication app, TimeSpan? startupDelay = null)
    {
        if (!IsLocalAndKestrel(app))
        {
            return;
        }

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            _ = Task.Run(async () =>
            {
                const string restartMarkerVar = "BITDEVKIT_RESTARTING";
                // If this instance was spawned by a restart, clear the marker so future restarts are allowed.
                if (Environment.GetEnvironmentVariable(restartMarkerVar) == "1")
                {
                    Environment.SetEnvironmentVariable(restartMarkerVar, null);
                }

                EnsureHistoryLoaded();
                if (startupDelay.HasValue && startupDelay.Value.TotalMilliseconds > 0)
                {
                    await Task.Delay(startupDelay.Value);
                }
                var console = app.Services.GetRequiredService<IAnsiConsole>();
                var executor = app.Services.GetRequiredService<ConsoleCommandExecutor>();

                await RunLoopAsync(app, console, executor);
            });
        });
    }

    /// <summary>
    /// Determines whether the application is running locally on Kestrel (no IIS proxy) so the interactive console can be enabled.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns><c>true</c> if local &amp; Kestrel; otherwise <c>false</c>.</returns>
    private static bool IsLocalAndKestrel(WebApplication app)
    {
        if (!app.Environment.IsLocalDevelopment() && !app.Environment.IsDevelopment())
        {
            return false;
        }

        try
        {
            var server = app.Services.GetService<IServer>();
            var hasFeature = server?.Features.Get<IServerAddressesFeature>() is not null;
            var hasUrls = app.Urls?.Count > 0;
            return hasFeature || hasUrls;
        }
        catch { return false; }
    }

    /// <summary>
    /// Main input loop that reads user commands and executes them until shutdown or stdin closes.
    /// </summary>
    /// <param name="app">The running web application.</param>
    /// <param name="console">The Spectre console abstraction.</param>
    private static async Task RunLoopAsync(WebApplication app, IAnsiConsole console, ConsoleCommandExecutor executor)
    {
        PrintBanner(console);

        while (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
        {
            console.Markup("[grey]> [/]");

            var line = Console.ReadLine();
            if (line is null) { break; }
            if (string.IsNullOrWhiteSpace(line)) { continue; }

            await executor.ExecuteAsync(
                line,
                console,
                app.Services,
                ConsoleCommandExecutionSource.Terminal,
                app.Lifetime.ApplicationStopping);
        }
    }

    /// <summary>
    /// Writes the startup banner and quick usage hint.
    /// </summary>
    /// <param name="console">The console abstraction.</param>
    private static void PrintBanner(IAnsiConsole console)
    {
        console.MarkupLine("[grey]Type [bold]help[/]/[bold]?[/] for commands. Examples: [bold]status[/], [bold]mem[/], [bold]env[/], [bold]ports[/], [bold]metrics[/], [bold]restart[/].[/]");
        console.Write(new Rule().Centered().RuleStyle("grey"));
    }

    /// <summary>
    /// Ensures history file has been loaded for the current assembly context.
    /// </summary>
    private static void EnsureHistoryLoaded()
    {
        ConsoleCommandHistory.Initialize(
            Assembly.GetEntryAssembly()?.GetName().Name);
    }
}
