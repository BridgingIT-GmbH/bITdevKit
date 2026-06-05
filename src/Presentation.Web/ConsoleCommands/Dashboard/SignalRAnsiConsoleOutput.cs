// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.ConsoleCommands.Dashboard;

using Spectre.Console;
using System.IO;
using System.Text;

/// <summary>
/// Spectre.Console output adapter that writes ANSI text to SignalR.
/// </summary>
/// <example>
/// <code>
/// var output = new SignalRAnsiConsoleOutput(sendAsync, 120, 32, token);
/// </code>
/// </example>
public sealed class SignalRAnsiConsoleOutput : IAnsiConsoleOutput
{
    private readonly SignalRTextWriter writer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRAnsiConsoleOutput" /> class.
    /// </summary>
    public SignalRAnsiConsoleOutput(
        Func<string, CancellationToken, Task> sendAsync,
        int width,
        int height,
        CancellationToken cancellationToken)
    {
        this.writer = new SignalRTextWriter(sendAsync, cancellationToken);
        this.Width = width;
        this.Height = height;
    }

    /// <inheritdoc />
    public TextWriter Writer => this.writer;

    /// <inheritdoc />
    public bool IsTerminal => true;

    /// <inheritdoc />
    public int Width { get; }

    /// <inheritdoc />
    public int Height { get; }

    /// <inheritdoc />
    public void SetEncoding(Encoding encoding)
    {
    }
}
