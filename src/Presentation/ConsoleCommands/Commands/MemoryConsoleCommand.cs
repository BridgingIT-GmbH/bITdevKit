// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;
using System.Diagnostics;

public class MemoryConsoleCommand : ConsoleCommandBase
{
    /// <summary>
    /// Shows managed memory, process working set and selected diagnostic counters.
    /// Usage: <c>mem</c>
    /// Examples:
    /// <list type="bullet">
    /// <item><description><c>mem</c></description></item>
    /// </list>
    /// </summary>
    public MemoryConsoleCommand() : base("memory", "Show memory usage", ["mem"]) { }

    /// <summary>Executes the memory command.</summary>
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        var proc = Process.GetCurrentProcess();
        var managed = GC.GetTotalMemory(false);
        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Metric"); table.AddColumn("Value");
        table.AddRow("Managed", $"{managed / (1024 * 1024.0):F2} MB");
        table.AddRow("WorkingSet", $"{proc.WorkingSet64 / (1024 * 1024.0):F2} MB");
        table.AddRow("PrivateMem", $"{proc.PrivateMemorySize64 / (1024 * 1024.0):F2} MB");
        table.AddRow("Threads", proc.Threads.Count.ToString());
        table.AddRow("Handles", proc.HandleCount.ToString());
        console.Write(table);

        return Task.CompletedTask;
    }
}
// === end diagnostic additions ===