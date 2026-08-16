// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using BridgingIT.DevKit.Common;
using Spectre.Console;
using System.Diagnostics;

/// <summary>
/// Represents memory console command.
/// </summary>
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
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var proc = Process.GetCurrentProcess();
        var managed = GC.GetTotalMemory(false);
        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Metric"); table.AddColumn("Value");
        table.AddRow("Managed", $"{ByteSize.ToMegabytes(managed):F2} MB");
        table.AddRow("WorkingSet", $"{ByteSize.ToMegabytes(proc.WorkingSet64):F2} MB");
        table.AddRow("PrivateMem", $"{ByteSize.ToMegabytes(proc.PrivateMemorySize64):F2} MB");
        table.AddRow("Threads", proc.Threads.Count.ToString());
        table.AddRow("Handles", proc.HandleCount.ToString());
        console.Write(table);

        return Task.CompletedTask;
    }
}
// === end diagnostic additions ===
