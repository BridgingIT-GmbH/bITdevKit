// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public static partial class StorageDimensionsQualification
{
    private static async Task CaptureColdDatabaseReadsAsync(IServiceScopeFactory scopes, string engine,
        string output, IReadOnlyList<DimensionQualificationJob> jobs)
    {
        // The container belongs to this invocation. Never flush caches on application databases or the host.
        if (engine == "sqlserver")
        {
            await using var scope = scopes.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
            await context.Database.ExecuteSqlRawAsync("CHECKPOINT; DBCC DROPCLEANBUFFERS WITH NO_INFOMSGS;");
        }

        var cases = new List<object>();
        foreach (var job in jobs.Where(job => !job.Writer))
        {
            var timer = Stopwatch.StartNew();
            var rows = await job.Execute(0);
            cases.Add(new { Case = job.Name, Rows = rows, ElapsedMilliseconds = timer.Elapsed.TotalMilliseconds });
        }

        await WriteAsync(output, "cold-database-reads.json", new
        {
            Engine = engine, Cases = cases,
            Reset = engine == "sqlserver" ? "CHECKPOINT and DBCC DROPCLEANBUFFERS in the owned container" : "Restart of the owned PostgreSQL container; refreshed port bindings and client pools",
            Note = "One sequential read pass after an engine-buffer reset, before profiling/warm-up. Later queries can benefit from earlier reads. Host OS caches are retained on the larger physical host. Informational, not a cold-storage latency guarantee."
        });
        Console.WriteLine("Cold database-buffer read pass captured; normal warm-up and measured intervals follow.");
    }
}
