// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.ConsoleCommands.Dashboard;

using System.Collections.Concurrent;

/// <summary>
/// Tracks one dashboard web console browser session.
/// </summary>
/// <example>
/// <code>
/// var session = new WebConsoleSession("session-id");
/// </code>
/// </example>
public sealed class WebConsoleSession(string sessionId)
{
    private const int MaxOutputLines = 1000;
    private readonly ConcurrentQueue<string> outputBuffer = new();
    private readonly object commandSync = new();
    private CancellationTokenSource commandCancellation;

    /// <summary>
    /// Gets the stable browser session id.
    /// </summary>
    public string SessionId { get; } = sessionId;

    /// <summary>
    /// Gets or sets the current SignalR connection id.
    /// </summary>
    public string ConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the terminal column count.
    /// </summary>
    public int Columns { get; private set; } = 120;

    /// <summary>
    /// Gets or sets the terminal row count.
    /// </summary>
    public int Rows { get; private set; } = 32;

    /// <summary>
    /// Gets a value indicating whether a command is currently running.
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Updates terminal dimensions.
    /// </summary>
    public void Resize(int columns, int rows)
    {
        this.Columns = Math.Clamp(columns, 40, 300);
        this.Rows = Math.Clamp(rows, 10, 100);
    }

    /// <summary>
    /// Attempts to start a command.
    /// </summary>
    public bool TryStartCommand(out CancellationTokenSource cancellation)
    {
        lock (this.commandSync)
        {
            if (this.IsRunning)
            {
                cancellation = null;
                return false;
            }

            this.commandCancellation = new CancellationTokenSource();
            this.IsRunning = true;
            cancellation = this.commandCancellation;
            return true;
        }
    }

    /// <summary>
    /// Cancels the active command if one is running.
    /// </summary>
    public void CancelCurrentCommand()
    {
        lock (this.commandSync)
        {
            this.commandCancellation?.Cancel();
        }
    }

    /// <summary>
    /// Marks the active command as complete.
    /// </summary>
    public void CompleteCommand()
    {
        lock (this.commandSync)
        {
            this.commandCancellation?.Dispose();
            this.commandCancellation = null;
            this.IsRunning = false;
        }
    }

    /// <summary>
    /// Appends text to the bounded output replay buffer.
    /// </summary>
    public void AppendOutput(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        this.outputBuffer.Enqueue(text);
        while (this.outputBuffer.Count > MaxOutputLines && this.outputBuffer.TryDequeue(out _))
        {
        }
    }

    /// <summary>
    /// Gets buffered output text for reconnect replay.
    /// </summary>
    public string[] GetBufferedOutput()
    {
        return this.outputBuffer.ToArray();
    }
}
