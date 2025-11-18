// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;

public class ClearConsoleCommand : ConsoleCommandBase
{
    /// <summary>
    /// Clears the console using Spectre facilities.
    /// Usage: <c>clear</c>
    /// Aliases: <c>cls</c>, <c>c</c>
    /// </summary>
    public ClearConsoleCommand() : base("clear", "Clear the screen", "cls", "c") { }

    /// <summary>Executes screen clear.</summary>
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        AnsiConsole.Clear();

        return Task.CompletedTask;
    }
}