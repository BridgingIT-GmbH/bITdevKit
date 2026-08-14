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
using System.Text;

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

                EnsureHistoryLoaded(app);
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
            var line = ReadTerminalLine(console);
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

    private static string ReadTerminalLine(IAnsiConsole console)
    {
        var inputMode = SelectTerminalInputMode(
            Console.IsInputRedirected,
            Console.IsOutputRedirected,
            OperatingSystem.IsLinux());

        if (inputMode == TerminalInputMode.Redirected)
        {
            return Console.ReadLine();
        }

        if (inputMode == TerminalInputMode.Basic)
        {
            return ReadBasicTerminalLine(console);
        }

        try
        {
            return ReadInteractiveTerminalLine();
        }
        catch (IOException)
        {
            return Console.ReadLine();
        }
        catch (InvalidOperationException)
        {
            return Console.ReadLine();
        }
        catch (ArgumentOutOfRangeException)
        {
            return Console.ReadLine();
        }
    }

    internal static TerminalInputMode SelectTerminalInputMode(
        bool isInputRedirected,
        bool isOutputRedirected,
        bool isLinux) =>
        (isInputRedirected, isOutputRedirected, isLinux) switch
        {
            (true, _, _) or (_, true, _) => TerminalInputMode.Redirected,
            (_, _, true) => TerminalInputMode.Basic,
            _ => TerminalInputMode.Enhanced
        };

    private static string ReadBasicTerminalLine(IAnsiConsole console)
    {
        var theme = ConsoleTheme.Current;
        console.Markup($"[{theme.PromptStyle}]> [/]");

        return Console.ReadLine();
    }

    private static string ReadInteractiveTerminalLine()
    {
        var history = ConsoleCommandHistory.GetAll()
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
        var historyIndex = history.Count;
        var draft = string.Empty;
        var buffer = new StringBuilder();
        var cursor = 0;
        using var session = InteractiveConsoleCoordinator.Instance.BeginInput("> ", console =>
        {
            var theme = ConsoleTheme.Current;
            console.Markup($"[{theme.PromptStyle}]> [/]");
        });

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    Console.WriteLine();
                    return buffer.ToString();
                case ConsoleKey.Backspace when cursor > 0:
                    buffer.Remove(cursor - 1, 1);
                    cursor--;
                    RedrawInput(session, buffer, cursor);
                    break;
                case ConsoleKey.Delete when cursor < buffer.Length:
                    buffer.Remove(cursor, 1);
                    RedrawInput(session, buffer, cursor);
                    break;
                case ConsoleKey.LeftArrow when HasControlModifier(key) && cursor > 0:
                    cursor = MoveToPreviousWord(buffer, cursor);
                    SetInputCursor(session, cursor);
                    break;
                case ConsoleKey.LeftArrow when cursor > 0:
                    cursor--;
                    SetInputCursor(session, cursor);
                    break;
                case ConsoleKey.RightArrow when HasControlModifier(key) && cursor < buffer.Length:
                    cursor = MoveToNextWord(buffer, cursor);
                    SetInputCursor(session, cursor);
                    break;
                case ConsoleKey.RightArrow when cursor < buffer.Length:
                    cursor++;
                    SetInputCursor(session, cursor);
                    break;
                case ConsoleKey.L when HasControlModifier(key):
                    ReplaceInput(session, buffer, string.Empty, ref cursor);
                    historyIndex = history.Count;
                    draft = string.Empty;
                    break;
                case ConsoleKey.Home:
                    cursor = 0;
                    SetInputCursor(session, cursor);
                    break;
                case ConsoleKey.End:
                    cursor = buffer.Length;
                    SetInputCursor(session, cursor);
                    break;
                case ConsoleKey.UpArrow:
                    if (history.Count == 0 || historyIndex <= 0)
                    {
                        break;
                    }

                    if (historyIndex == history.Count)
                    {
                        draft = buffer.ToString();
                    }

                    historyIndex--;
                    ReplaceInput(session, buffer, history[historyIndex], ref cursor);
                    break;
                case ConsoleKey.DownArrow:
                    if (historyIndex >= history.Count)
                    {
                        break;
                    }

                    historyIndex++;
                    ReplaceInput(session, buffer, historyIndex == history.Count ? draft : history[historyIndex], ref cursor);
                    break;
                case ConsoleKey.Escape:
                    ReplaceInput(session, buffer, string.Empty, ref cursor);
                    historyIndex = history.Count;
                    draft = string.Empty;
                    break;
                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        buffer.Insert(cursor, key.KeyChar);
                        cursor++;
                        historyIndex = history.Count;
                        RedrawInput(session, buffer, cursor);
                    }

                    break;
            }
        }
    }

    private static bool HasControlModifier(ConsoleKeyInfo key)
    {
        return (key.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control;
    }

    private static int MoveToPreviousWord(StringBuilder buffer, int cursor)
    {
        var index = Math.Clamp(cursor, 0, buffer.Length);
        while (index > 0 && char.IsWhiteSpace(buffer[index - 1]))
        {
            index--;
        }

        while (index > 0 && !char.IsWhiteSpace(buffer[index - 1]))
        {
            index--;
        }

        return index;
    }

    private static int MoveToNextWord(StringBuilder buffer, int cursor)
    {
        var index = Math.Clamp(cursor, 0, buffer.Length);
        while (index < buffer.Length && !char.IsWhiteSpace(buffer[index]))
        {
            index++;
        }

        while (index < buffer.Length && char.IsWhiteSpace(buffer[index]))
        {
            index++;
        }

        return index;
    }

    private static void ReplaceInput(InteractiveConsoleInputSession session, StringBuilder buffer, string value, ref int cursor)
    {
        buffer.Clear();
        buffer.Append(value);
        cursor = buffer.Length;
        RedrawInput(session, buffer, cursor);
    }

    private static void RedrawInput(InteractiveConsoleInputSession session, StringBuilder buffer, int cursor)
    {
        session.Update(buffer.ToString(), cursor);
        session.Redraw();
    }

    private static void SetInputCursor(InteractiveConsoleInputSession session, int cursor)
    {
        session.SetCursor(cursor);
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
    private static void EnsureHistoryLoaded(WebApplication app)
    {
        ConsoleCommandHistory.Initialize(app.Environment.ApplicationName);
    }

    internal enum TerminalInputMode
    {
        Redirected,
        Basic,
        Enhanced
    }
}
