// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.ConsoleCommands.Dashboard;

using Spectre.Console;

/// <summary>
/// Creates per-command ANSI consoles for the dashboard web console.
/// </summary>
/// <example>
/// <code>
/// var console = factory.Create(sendAsync, 120, 32, cancellationToken);
/// </code>
/// </example>
public sealed class WebAnsiConsoleFactory
{
    /// <summary>
    /// Creates a Spectre console that streams ANSI output through the supplied callback.
    /// </summary>
    public IAnsiConsole Create(
        Func<string, CancellationToken, Task> sendAsync,
        int columns,
        int rows,
        CancellationToken cancellationToken)
    {
        return AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            ColorSystem = ColorSystemSupport.TrueColor,
            Out = new SignalRAnsiConsoleOutput(sendAsync, columns, rows, cancellationToken)
        });
    }
}
