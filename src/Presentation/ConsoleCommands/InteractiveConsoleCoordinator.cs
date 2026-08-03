// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;

/// <summary>
/// Coordinates native terminal writes with the interactive console command prompt.
/// </summary>
/// <example>
/// <code>
/// var coordinator = InteractiveConsoleCoordinator.Instance;
/// coordinator.WriteLine("log line");
/// </code>
/// </example>
public sealed class InteractiveConsoleCoordinator
{
    private readonly object sync = new();
    private bool inputActive;
    private string prompt = string.Empty;
    private Action promptWriter;
    private string buffer = string.Empty;
    private int cursor;
    private int startLeft;
    private int startTop;

    private InteractiveConsoleCoordinator()
    {
    }

    /// <summary>
    /// Gets the shared process-wide coordinator used by the terminal prompt and console log sink.
    /// </summary>
    /// <example>
    /// <code>
    /// InteractiveConsoleCoordinator.Instance.WriteLine("ready");
    /// </code>
    /// </example>
    public static InteractiveConsoleCoordinator Instance { get; } = new();

    /// <summary>
    /// Gets a value indicating whether an interactive command input session is currently active.
    /// </summary>
    /// <example>
    /// <code>
    /// var active = InteractiveConsoleCoordinator.Instance.IsInputActive;
    /// </code>
    /// </example>
    public bool IsInputActive
    {
        get
        {
            lock (this.sync)
            {
                return this.inputActive;
            }
        }
    }

    /// <summary>
    /// Writes coordinated terminal output while preserving the active input prompt when possible.
    /// </summary>
    /// <param name="write">The output action to execute.</param>
    /// <example>
    /// <code>
    /// coordinator.Write(() => Console.WriteLine("message"));
    /// </code>
    /// </example>
    public void Write(Action write)
    {
        ArgumentNullException.ThrowIfNull(write);

        lock (this.sync)
        {
            this.WriteCore(write);
        }
    }

    /// <summary>
    /// Writes coordinated Spectre console output while preserving the active input prompt when possible.
    /// </summary>
    /// <param name="write">The Spectre console output action to execute.</param>
    /// <example>
    /// <code>
    /// coordinator.Write(console => console.MarkupLine("[green]ready[/]"));
    /// </code>
    /// </example>
    public void Write(Action<IAnsiConsole> write)
    {
        ArgumentNullException.ThrowIfNull(write);

        this.Write(() => write(AnsiConsole.Console));
    }

    /// <summary>
    /// Writes text while preserving the active input prompt when possible.
    /// </summary>
    /// <param name="text">The text to write.</param>
    /// <example>
    /// <code>
    /// coordinator.Write("message");
    /// </code>
    /// </example>
    public void Write(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        this.Write(() => Console.Write(text));
    }

    /// <summary>
    /// Writes a line while preserving the active input prompt when possible.
    /// </summary>
    /// <param name="text">The line text to write.</param>
    /// <example>
    /// <code>
    /// coordinator.WriteLine("message");
    /// </code>
    /// </example>
    public void WriteLine(string text)
    {
        this.Write(() => Console.WriteLine(text));
    }

    /// <summary>
    /// Starts an input session and records prompt positioning.
    /// </summary>
    /// <param name="promptText">The prompt text rendered before the command line.</param>
    /// <example>
    /// <code>
    /// var session = coordinator.BeginInput("&gt; ");
    /// </code>
    /// </example>
    public InteractiveConsoleInputSession BeginInput(string promptText)
    {
        return this.BeginInput(promptText, null);
    }

    /// <summary>
    /// Starts an input session and renders the prompt with Spectre console output.
    /// </summary>
    /// <param name="promptText">The plain-text prompt used for fallback and state.</param>
    /// <param name="writePrompt">The optional Spectre prompt writer.</param>
    /// <example>
    /// <code>
    /// var session = coordinator.BeginInput("&gt; ", console => console.Markup("[grey]&gt; [/]"));
    /// </code>
    /// </example>
    public InteractiveConsoleInputSession BeginInput(string promptText, Action<IAnsiConsole> writePrompt)
    {
        lock (this.sync)
        {
            this.prompt = promptText ?? string.Empty;
            this.promptWriter = writePrompt is null ? null : () => writePrompt(AnsiConsole.Console);
            this.WritePromptCore();
            this.buffer = string.Empty;
            this.cursor = 0;
            this.startLeft = Console.CursorLeft;
            this.startTop = Console.CursorTop;
            this.inputActive = true;

            return new InteractiveConsoleInputSession(this);
        }
    }

    internal void UpdateInput(string value, int cursorPosition)
    {
        lock (this.sync)
        {
            this.buffer = value ?? string.Empty;
            this.cursor = Math.Clamp(cursorPosition, 0, this.buffer.Length);
        }
    }

    internal void RedrawInput()
    {
        lock (this.sync)
        {
            this.RedrawInputCore();
        }
    }

    internal void SetInputCursor(int cursorPosition)
    {
        lock (this.sync)
        {
            this.cursor = Math.Clamp(cursorPosition, 0, this.buffer.Length);
            this.SetInputCursorCore();
        }
    }

    internal void EndInput()
    {
        lock (this.sync)
        {
            this.inputActive = false;
            this.prompt = string.Empty;
            this.promptWriter = null;
            this.buffer = string.Empty;
            this.cursor = 0;
        }
    }

    private void WriteCore(Action write)
    {
        var shouldRedraw = this.inputActive && !Console.IsOutputRedirected;
        if (shouldRedraw)
        {
            this.ClearInputLineCore();
        }

        write();

        if (shouldRedraw)
        {
            this.WritePromptCore();
            this.startLeft = Console.CursorLeft;
            this.startTop = Console.CursorTop;
            Console.Write(this.buffer);
            this.SetInputCursorCore();
        }
    }

    private void RedrawInputCore()
    {
        if (!this.inputActive || Console.IsOutputRedirected)
        {
            return;
        }

        this.ClearInputLineCore();
        this.WritePromptCore();
        this.startLeft = Console.CursorLeft;
        this.startTop = Console.CursorTop;
        Console.Write(this.buffer);
        this.SetInputCursorCore();
    }

    private void ClearInputLineCore()
    {
        Console.SetCursorPosition(0, this.startTop);
        Console.Write(new string(' ', Math.Max(0, Console.BufferWidth - 1)));
        Console.SetCursorPosition(0, this.startTop);
    }

    private void SetInputCursorCore()
    {
        Console.SetCursorPosition(Math.Min(Console.BufferWidth - 1, this.startLeft + this.cursor), this.startTop);
    }

    private void WritePromptCore()
    {
        if (this.promptWriter is not null)
        {
            this.promptWriter();
            return;
        }

        Console.Write(this.prompt);
    }
}

/// <summary>
/// Represents an active interactive console input session.
/// </summary>
/// <example>
/// <code>
/// using var session = InteractiveConsoleCoordinator.Instance.BeginInput("&gt; ");
/// </code>
/// </example>
public sealed class InteractiveConsoleInputSession : IDisposable
{
    private readonly InteractiveConsoleCoordinator coordinator;
    private bool disposed;

    internal InteractiveConsoleInputSession(InteractiveConsoleCoordinator coordinator)
    {
        this.coordinator = coordinator;
    }

    /// <summary>
    /// Updates the active input buffer and cursor.
    /// </summary>
    /// <param name="value">The command-line buffer.</param>
    /// <param name="cursorPosition">The cursor position within the buffer.</param>
    /// <example>
    /// <code>
    /// session.Update("help", 4);
    /// </code>
    /// </example>
    public void Update(string value, int cursorPosition) => this.coordinator.UpdateInput(value, cursorPosition);

    /// <summary>
    /// Redraws the active input line.
    /// </summary>
    /// <example>
    /// <code>
    /// session.Redraw();
    /// </code>
    /// </example>
    public void Redraw() => this.coordinator.RedrawInput();

    /// <summary>
    /// Moves the terminal cursor inside the active input line.
    /// </summary>
    /// <param name="cursorPosition">The cursor position within the input buffer.</param>
    /// <example>
    /// <code>
    /// session.SetCursor(0);
    /// </code>
    /// </example>
    public void SetCursor(int cursorPosition) => this.coordinator.SetInputCursor(cursorPosition);

    /// <inheritdoc />
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.coordinator.EndInput();
        this.disposed = true;
    }
}
