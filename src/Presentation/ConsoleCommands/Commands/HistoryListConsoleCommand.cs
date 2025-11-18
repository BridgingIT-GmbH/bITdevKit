// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;
using System.Collections.Generic;
using System.Linq;

public class HistoryListConsoleCommand : ConsoleCommandBase, IGroupedConsoleCommand
{
    public string GroupName => "history";

    public IReadOnlyCollection<string> GroupAliases => ["hist"];

    [ConsoleCommandOption("max", Alias = "m", Description = "Max items to show", Default = 50)] public int Max { get; set; }
    public HistoryListConsoleCommand() : base("list", "List command history") { }

    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var all = ConsoleCommandHistory.GetAll(); var take = this.Max <= 0 ? 50 : this.Max;
        var seq = all.Select((l, i) => (i + 1, l)).Reverse().Take(take).Reverse();
        var table = new Table().Border(TableBorder.Minimal); table.AddColumn("#"); table.AddColumn("Command");
        foreach (var item in seq)
        {
            table.AddRow(item.Item1.ToString(), item.l);
        }

        if (!table.Rows.Any())
        {
            table.AddRow("-", "(empty)");
        }

        console.Write(table);
        var lastError = ConsoleCommandHistory.LastError; if (!string.IsNullOrWhiteSpace(lastError))
        {
            console.MarkupLine("[yellow]History IO issues detected:[/] " + lastError);
        }

        return Task.CompletedTask;
    }
}
// === end diagnostic additions ===