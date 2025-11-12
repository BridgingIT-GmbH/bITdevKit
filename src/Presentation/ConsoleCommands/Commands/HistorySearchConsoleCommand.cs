// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;
using System.Collections.Generic;
using System.Linq;

public class HistorySearchConsoleCommand : ConsoleCommandBase, IGroupedConsoleCommand
{
    public string GroupName => "history";
    public IReadOnlyCollection<string> GroupAliases => ["hist"];
    [ConsoleCommandArgument(0, Description = "Search text", Required = true)] public string Text { get; set; }
    public HistorySearchConsoleCommand() : base("search", "Search command history by substring") { }
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var query = this.Text ?? string.Empty;
        var matches = ConsoleCommandHistory.GetAll().Select((l, i) => (i + 1, l))
            .Where(x => x.l.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            .Reverse().Take(100).Reverse();
        var table = new Table().Border(TableBorder.Minimal); table.AddColumn("#"); table.AddColumn("Match");
        foreach (var item in matches)
        {
            table.AddRow(item.Item1.ToString(), item.l);
        }

        if (!table.Rows.Any())
        {
            table.AddRow("-", "(none)");
        }

        console.Write(table);

        return Task.CompletedTask;
    }
}
// === end diagnostic additions ===