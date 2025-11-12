// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;
using System.Collections.Generic;

public class HistoryClearConsoleCommand : ConsoleCommandBase, IGroupedConsoleCommand
{
    public string GroupName => "history";
    public IReadOnlyCollection<string> GroupAliases => ["hist"];
    [ConsoleCommandOption("keep-last", Alias = "k", Description = "Keep last N entries", Default = 10)] public int KeepLast { get; set; }
    public HistoryClearConsoleCommand() : base("clear", "Clear command history (optionally keep last N)") { }
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        ConsoleCommandHistory.ClearKeepLast(Math.Max(0, this.KeepLast));
        console.MarkupLine(this.KeepLast > 0 ? $"[green]History cleared; kept last {this.KeepLast} entries.[/]" : "[green]History fully cleared.[/]");

        return Task.CompletedTask;
    }
}
// === end diagnostic additions ===