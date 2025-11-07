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
using System.Collections.Generic;
using System.Linq;
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

                await RunLoopAsync(app, console);
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
    private static async Task RunLoopAsync(WebApplication app, IAnsiConsole console)
    {
        PrintBanner(console);

        while (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
        {
            console.Markup("[grey]> [/]");

            var line = Console.ReadLine();
            if (line is null) { break; }
            if (string.IsNullOrWhiteSpace(line)) { continue; }

            ConsoleCommandHistory.Append(line);
            var tokens = SplitArgs(line);
            if (tokens.Length == 0) { continue; }

            var primary = tokens[0];
            using var scope = app.Services.CreateScope();
            var all = scope.ServiceProvider.GetServices<IConsoleCommand>().ToList();
            IConsoleCommand cmd = null;
            var groupedCandidates = all.OfType<IGroupedConsoleCommand>()
                .Where(g => string.Equals(g.GroupName, primary, StringComparison.OrdinalIgnoreCase)
                    || g.GroupAliases?.Any(a => string.Equals(a, primary, StringComparison.OrdinalIgnoreCase)) == true)
                .ToList();
            var consumed = 1;
            if (groupedCandidates.Count != 0)
            {
                if (tokens.Length == 1)
                {
                    // list subcommands for this group
                    var table = new Table().Border(TableBorder.Minimal).Title($"[bold cyan]{primary} (group) subcommands[/]");
                    table.AddColumn("Sub"); table.AddColumn("Description");
                    foreach (var sc in groupedCandidates.OrderBy(c => c.Name)) { table.AddRow(sc.Name, sc.Description); }
                    console.Write(table); continue;
                }
                var sub = tokens[1]; consumed = 2; cmd = groupedCandidates.FirstOrDefault(c => c.Matches(sub));
                if (cmd is null) { console.MarkupLine($"[yellow]Unknown subcommand '{sub}' for group '{primary}'.[/]"); continue; }
            }
            else { cmd = all.FirstOrDefault(c => c.Matches(primary)); }
            if (cmd is null) { console.MarkupLine("[yellow]Unknown command[/]. Type [bold]help[/] for list."); continue; }
            console.MarkupLine("[cyan]=== " + (cmd is IGroupedConsoleCommand gc ? gc.GroupName + " " + cmd.Name : cmd.Name) + " ===[/][grey] " + cmd.Description + "[/]");

            var bindTokens = tokens.Skip(consumed).ToArray();
            var (ok, errors) = ConsoleCommandBinder.TryBind(cmd, bindTokens);
            if (!ok)
            {
                foreach (var e in errors)
                {
                    console.MarkupLine("[red]" + e + "[/]");
                }

                ConsoleCommandBinder.WriteHelp(console, cmd, detailed: true); continue;
            }
            try { cmd.OnAfterBind(console, tokens); }
            catch (Exception hookEx) { console.MarkupLine("[red]Post-bind validation failed:[/] " + hookEx.Message); continue; }
            try { await cmd.ExecuteAsync(console, scope.ServiceProvider); }
            catch (Exception ex) { console.MarkupLine("[red]Command failed:[/] " + ex.Message); }
        }
    }

    /// <summary>
    /// Splits a raw command line into tokens honoring quoted segments.
    /// </summary>
    /// <param name="commandLine">The raw command line string.</param>
    /// <returns>Token array.</returns>
    private static string[] SplitArgs(string commandLine)
    {
        var list = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var ch in commandLine)
        {
            switch (ch)
            {
                case '"': inQuotes = !inQuotes; break;
                case ' ' when !inQuotes:
                    if (current.Length > 0) { list.Add(current.ToString()); current.Clear(); }
                    break;
                default: current.Append(ch); break;
            }
        }
        if (current.Length > 0)
        {
            list.Add(current.ToString());
        }

        return list.ToArray();
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
