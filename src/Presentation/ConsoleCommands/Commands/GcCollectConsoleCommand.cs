// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;

public class GcCollectConsoleCommand : ConsoleCommandBase
{
    [ConsoleCommandOption("no-collect", Description = "Skip forcing GC; only show current memory stats")] public bool NoCollect { get; set; }
    /// <summary>
    /// Forces a full garbage collection and reports memory freed and collection counts (unless --no-collect specified).
    /// Usage: <c>gc</c>
    /// Alias: <c>collect</c>
    /// </summary>
    public GcCollectConsoleCommand() : base("gc", "Force GC collect and show freed memory", ["col"]) { }

    /// <summary>Executes (optional) GC collection and displays memory stats.</summary>
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var before = GC.GetTotalMemory(false);
        if (!this.NoCollect)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        var after = GC.GetTotalMemory(false); // do not force finalizers if skipped
        var freedMb = this.NoCollect ? 0 : (before - after) / (1024 * 1024.0);
        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Metric"); table.AddColumn("Value");
        table.AddRow("Before.Managed MB", (before / (1024 * 1024.0)).ToString("F2"));
        table.AddRow("After.Managed MB", (after / (1024 * 1024.0)).ToString("F2"));
        table.AddRow("Freed MB", freedMb.ToString("F2"));
        table.AddRow("Mode", this.NoCollect ? "skipped" : "forced");
        table.AddRow("Gen0", GC.CollectionCount(0).ToString());
        table.AddRow("Gen1", GC.CollectionCount(1).ToString());
        table.AddRow("Gen2", GC.CollectionCount(2).ToString());
        console.Write(table);

        return Task.CompletedTask;
    }
}
// === end diagnostic additions ===