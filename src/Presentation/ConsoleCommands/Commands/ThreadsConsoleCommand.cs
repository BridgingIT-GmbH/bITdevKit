// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation;

using Spectre.Console;
using System.Threading; // added for ThreadPool

public class ThreadsConsoleCommand : ConsoleCommandBase
{
    /// <summary>
    /// Displays thread pool configuration, usage and pending work item count.
    /// Usage: <c>threads</c>
    /// </summary>
    public ThreadsConsoleCommand() : base("threads", "Thread pool statistics", ["tr"]) { }

    /// <summary>Outputs thread pool statistics.</summary>
    public override Task ExecuteAsync(IAnsiConsole console, IServiceProvider services)
    {
        ThreadPool.GetAvailableThreads(out var workerAvail, out var ioAvail);
        ThreadPool.GetMaxThreads(out var workerMax, out var ioMax);
        ThreadPool.GetMinThreads(out var workerMin, out var ioMin);
        var workerUsed = workerMax - workerAvail;
        var ioUsed = ioMax - ioAvail;
        var pending = GetPendingWorkItemCountSafe();
        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Metric"); table.AddColumn("Value");
        table.AddRow("Worker.Max", workerMax.ToString());
        table.AddRow("Worker.Min", workerMin.ToString());
        table.AddRow("Worker.Available", workerAvail.ToString());
        table.AddRow("Worker.Used", workerUsed.ToString());
        table.AddRow("IO.Max", ioMax.ToString());
        table.AddRow("IO.Min", ioMin.ToString());
        table.AddRow("IO.Available", ioAvail.ToString());
        table.AddRow("IO.Used", ioUsed.ToString());
        table.AddRow("PendingWorkItems", pending.ToString());
        console.Write(table);

        return Task.CompletedTask;

        static long GetPendingWorkItemCountSafe()
        {
            try { return ThreadPool.PendingWorkItemCount; } catch { return -1; }
        }
    }
}
// === end diagnostic additions ===