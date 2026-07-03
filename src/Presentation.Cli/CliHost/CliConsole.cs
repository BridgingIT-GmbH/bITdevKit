namespace BridgingIT.DevKit.Cli;

using System.Text.Json;
using Spectre.Console;

/// <summary>
/// Wraps Spectre.Console and JSON output behavior for CLI commands.
/// </summary>
/// <param name="settings">The output settings.</param>
public sealed class CliConsole(CliOutputSettings settings)
{
    private readonly IAnsiConsole console = Spectre.Console.AnsiConsole.Create(new AnsiConsoleSettings
    {
        Ansi = settings.NoColor ? AnsiSupport.No : AnsiSupport.Detect,
        ColorSystem = settings.NoColor ? ColorSystemSupport.NoColors : ColorSystemSupport.Detect
    });

    private readonly IAnsiConsole silentConsole = Spectre.Console.AnsiConsole.Create(new AnsiConsoleSettings
    {
        Ansi = AnsiSupport.No,
        ColorSystem = ColorSystemSupport.NoColors,
        Out = new SilentAnsiConsoleOutput()
    });

    /// <summary>
    /// Gets the underlying Spectre console.
    /// </summary>
    public IAnsiConsole Console => this.console;

    /// <summary>
    /// Gets the console that should be passed to the shared command executor.
    /// </summary>
    public IAnsiConsole ExecutorConsole => settings.IsJson || settings.Quiet ? this.silentConsole : this.console;

    /// <summary>
    /// Writes a plain text line when text output is enabled.
    /// </summary>
    /// <param name="text">The text to write.</param>
    public void WriteLine(string text = "")
    {
        if (!settings.Quiet && !settings.IsJson)
        {
            this.console.WriteLine(text);
        }
    }

    /// <summary>
    /// Writes a Spectre markup line when text output is enabled.
    /// </summary>
    /// <param name="markup">The markup to write.</param>
    public void MarkupLine(string markup)
    {
        if (!settings.Quiet && !settings.IsJson)
        {
            this.console.MarkupLine(markup);
        }
    }

    /// <summary>
    /// Writes a value as JSON to standard output.
    /// </summary>
    /// <param name="value">The value to serialize.</param>
    public void WriteJson(object value)
    {
        System.Console.Out.WriteLine(JsonSerializer.Serialize(value, CliJson.Options));
    }

    /// <summary>
    /// Writes an error in the configured output format.
    /// </summary>
    /// <param name="summary">The error summary.</param>
    /// <param name="code">The stable error code.</param>
    /// <param name="exitCode">The process exit code.</param>
    public void Error(string summary, string code, CliExitCode exitCode)
    {
        if (settings.IsJson)
        {
            this.WriteJson(new { available = false, code, summary, exitCode = (int)exitCode });
            return;
        }

        this.console.MarkupLine($"[red]{Escape(summary)}[/]");
    }

    /// <summary>
    /// Writes a table when text output is enabled.
    /// </summary>
    /// <param name="table">The table to write.</param>
    public void WriteTable(Table table)
    {
        if (!settings.Quiet && !settings.IsJson)
        {
            this.console.Write(table);
        }
    }

    /// <summary>
    /// Escapes text for Spectre markup output.
    /// </summary>
    /// <param name="value">The value to escape.</param>
    /// <returns>The escaped value.</returns>
    public static string Escape(string value)
        => Markup.Escape(value ?? string.Empty);
}
