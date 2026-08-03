// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

/// <summary>
/// Opens the persisted console command history file in a local editor and reloads it after saving.
/// </summary>
/// <example>
/// <code>
/// history edit
/// history edit --editor "code --wait"
/// </code>
/// </example>
public class HistoryEditConsoleCommand : ConsoleCommandBase, IGroupedConsoleCommand
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryEditConsoleCommand"/> class.
    /// </summary>
    /// <example>
    /// <code>
    /// var command = new HistoryEditConsoleCommand();
    /// </code>
    /// </example>
    public HistoryEditConsoleCommand() : base("edit", "Edit command history in a local editor")
    {
    }

    /// <inheritdoc />
    public string GroupName => "history";

    /// <inheritdoc />
    public IReadOnlyCollection<string> GroupAliases => ["hist"];

    /// <inheritdoc />
    public override bool IsWebConsoleEnabled => false;

    /// <summary>
    /// Gets or sets the editor command to use instead of the platform default.
    /// </summary>
    /// <example>
    /// <code>
    /// history edit --editor "code --wait"
    /// </code>
    /// </example>
    [ConsoleCommandOption("editor", Alias = "e", Description = "Editor command to use, for example \"code --wait\"")]
    public string Editor { get; set; }

    /// <inheritdoc />
    public override async Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var path = ConsoleCommandHistory.FilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        if (!File.Exists(path))
        {
            await File.WriteAllTextAsync(path, string.Empty, cancellationToken).ConfigureAwait(false);
        }

        var editor = ResolveEditor(this.Editor, path);
        console.MarkupLine($"[grey]Opening history file:[/] {Markup.Escape(path)}");
        console.MarkupLine($"[grey]Editor:[/] {Markup.Escape(editor.Display)}");

        try
        {
            using var process = Process.Start(editor.StartInfo);
            if (process is null)
            {
                console.MarkupLine("[red]Could not start the editor.[/]");
                return;
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            ConsoleCommandHistory.Reload();
            console.MarkupLine($"[green]History reloaded.[/] {ConsoleCommandHistory.GetAll().Count} entries");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            console.MarkupLine("[yellow]History edit cancelled.[/]");
        }
        catch (Exception ex)
        {
            console.MarkupLine("[red]Could not open the history editor:[/] " + Markup.Escape(ex.Message));
        }
    }

    private static EditorLaunch ResolveEditor(string configuredEditor, string path)
    {
        var command = FirstNonEmpty(configuredEditor, Environment.GetEnvironmentVariable("VISUAL"), Environment.GetEnvironmentVariable("EDITOR"));
        if (!string.IsNullOrWhiteSpace(command))
        {
            return FromCommand(command, path);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new EditorLaunch(
                new ProcessStartInfo("notepad", Quote(path)) { UseShellExecute = false },
                "notepad");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new EditorLaunch(
                new ProcessStartInfo("open", "-W " + Quote(path)) { UseShellExecute = false },
                "open -W");
        }

        var linuxEditor = CommandExists("sensible-editor")
            ? "sensible-editor"
            : CommandExists("editor")
                ? "editor"
                : "xdg-open";

        return new EditorLaunch(
            new ProcessStartInfo(linuxEditor, Quote(path)) { UseShellExecute = false },
            linuxEditor);
    }

    private static EditorLaunch FromCommand(string command, string path)
    {
        var tokens = ConsoleCommandExecutor.SplitArgs(command);
        if (tokens.Length == 0)
        {
            return ResolveEditor(null, path);
        }

        var arguments = string.Join(' ', tokens.Skip(1).Append(Quote(path)));
        return new EditorLaunch(
            new ProcessStartInfo(tokens[0], arguments) { UseShellExecute = false },
            command);
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static bool CommandExists(string command)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var extensions = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.BAT;.CMD")
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [string.Empty];

        return paths.Any(path => extensions.Any(extension => File.Exists(Path.Combine(path, command + extension))));
    }

    private static string Quote(string value) =>
        "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private sealed record EditorLaunch(ProcessStartInfo StartInfo, string Display);
}
