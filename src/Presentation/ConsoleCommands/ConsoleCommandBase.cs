// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Base implementation of <see cref="IConsoleCommand"/> with common logic.
/// </summary>
public abstract class ConsoleCommandBase(string name, string description, params string[] aliases) : IConsoleCommand
{
    /// <summary>Gets the command name.</summary>
    public string Name { get; } = name;

    /// <summary>Gets the collection of aliases.</summary>
    public IReadOnlyCollection<string> Aliases { get; } = aliases.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Gets the description text.</summary>
    public string Description { get; } = description;

    /// <inheritdoc />
    public bool Matches(string input) => string.Equals(this.Name, input, StringComparison.OrdinalIgnoreCase) || this.Aliases.Any(a => string.Equals(a, input, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public virtual void OnAfterBind(IAnsiConsole console, string[] tokens) { }

    /// <inheritdoc />
    public abstract Task ExecuteAsync(IAnsiConsole console, IServiceProvider services);
}