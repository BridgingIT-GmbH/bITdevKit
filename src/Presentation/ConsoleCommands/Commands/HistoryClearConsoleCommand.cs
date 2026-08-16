// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;
using System.Collections.Generic;

/// <summary>
/// Represents history clear console command.
/// </summary>
public class HistoryClearConsoleCommand : ConsoleCommandBase, IGroupedConsoleCommand
{
    /// <summary>
    /// Gets the group name.
    /// </summary>
    public string GroupName => "history";

    /// <summary>
    /// Gets the group aliases.
    /// </summary>
    public IReadOnlyCollection<string> GroupAliases => ["hist"];

    /// <summary>
    /// Gets or sets the keep last.
    /// </summary>
    [ConsoleCommandOption("keep-last", Alias = "k", Description = "Keep last N entries", Default = 10)] public int KeepLast { get; set; }
    /// <summary>
    /// Initializes a new instance of the <c>HistoryClearConsoleCommand</c> class.
    /// </summary>
    public HistoryClearConsoleCommand() : base("clear", "Clear command history (optionally keep last N)") { }

    /// <inheritdoc/>
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ConsoleCommandHistory.ClearKeepLast(Math.Max(0, this.KeepLast));
        console.MarkupLine(this.KeepLast > 0 ? $"[green]History cleared; kept last {this.KeepLast} entries.[/]" : "[green]History fully cleared.[/]");

        return Task.CompletedTask;
    }
}
