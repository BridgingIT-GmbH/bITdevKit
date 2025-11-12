// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;
using System.Collections.Generic;

/// <summary>
/// Defines a console command with metadata and execution logic.
/// </summary>
public interface IConsoleCommand
{
    /// <summary>Gets the primary command name.</summary>
    string Name { get; }

    /// <summary>Gets the aliases that also trigger this command.</summary>
    IReadOnlyCollection<string> Aliases { get; }

    /// <summary>Gets a short description of the command purpose.</summary>
    string Description { get; }

    /// <summary>Determines whether user input matches the command name or an alias.</summary>
    bool Matches(string input);

    /// <summary>Hook invoked after successful binding for additional validation or adjustment.</summary>
    void OnAfterBind(IAnsiConsole console, string[] tokens);

    /// <summary>Executes the command logic asynchronously.</summary>
    Task ExecuteAsync(IAnsiConsole console, IServiceProvider services);
}
