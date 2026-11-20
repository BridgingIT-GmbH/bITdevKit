// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public static partial class StorageDimensionsQualification
{
    private static async Task<int> ProfileOfferedAsync(BlobDimensionsQualification blobs, DocumentDimensionsQualification documents,
        IServiceScopeFactory scopes, string output, string source, string feature, int owners, bool skipCalibration, int pairs)
    {
        using var metadata = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(source)), "preflight.json")));
        if (metadata.RootElement.GetProperty("Engine").GetString() != "sqlserver"
            || metadata.RootElement.GetProperty("Feature").GetString() != feature
            || metadata.RootElement.GetProperty("OwnersPerFeature").GetInt32() != owners)
            throw new ArgumentException("The frozen offered load must describe the same engine, feature and owner count.");
        using var frozen = JsonDocument.Parse(await File.ReadAllTextAsync(source));
        var offered = frozen.RootElement.GetProperty("OfferedLoad").Deserialize<OrdinaryOfferedLoad>();
        if (offered.ReadsPerSecond <= 0 || offered.WritesPerSecond <= 0 || offered.QueueDeadlineMilliseconds != 250)
            throw new ArgumentException("Use a previously recorded reference offered load without changing its deadline.");
        var errors = new ConcurrentQueue<string>();
        // Reproduce the saturation history that precedes the full comparison; keep replay rates frozen.
        var calibration = skipCalibration ? null : await CalibrateOrdinaryLoadAsync(blobs, documents, "sqlserver", output, false, errors);
        await WriteAsync(output, "replay-load.json", new { Frozen = offered, DiagnosticCalibration = calibration, Source = Path.GetFullPath(source), DiagnosticOnly = true, SaturationHistorySkipped = skipCalibration });
        await CaptureSqlSchedulingEventsAsync(scopes, Path.Combine(output, "post-calibration-sql-events.xml"));
        var results = new List<object>();
        for (var pair = 0; pair < pairs; pair++)
        {
            foreach (var parentOnly in pair % 2 == 0 ? new[] { true, false } : new[] { false, true })
            {
                var jobs = new List<DimensionQualificationJob>();
                if (blobs is not null) jobs.AddRange(blobs.BaselineJobs(parentOnly));
                if (documents is not null) jobs.AddRange(documents.BaselineJobs(parentOnly));
                await LoadOfferedAsync(jobs, "sqlserver", TimeSpan.FromSeconds(15), errors, offered, false);
                var before = await CaptureSqlSchedulingStateAsync(scopes, parentOnly);
                var sqlBefore = await CaptureSqlProfileAsync(scopes);
                var schedulerSamples = new List<object>();
                using var sampling = new CancellationTokenSource();
                var schedulerSampling = SampleSqlRequestsAsync(scopes, schedulerSamples, sampling.Token);
                var misses = new ConcurrentQueue<object>();
                var startedAtUtc = DateTimeOffset.UtcNow;
                var timer = Stopwatch.StartNew();
                IReadOnlyList<DimensionQualificationCase> cases;
                try
                {
                    cases = await LoadOfferedAsync(jobs, "sqlserver", TimeSpan.FromSeconds(120), errors, offered, true, misses);
                }
                finally
                {
                    timer.Stop();
                    await sampling.CancelAsync();
                    await schedulerSampling;
                }

                var sqlAfter = await CaptureSqlProfileAsync(scopes);
                var endedAtUtc = DateTimeOffset.UtcNow;
                var after = await CaptureSqlSchedulingStateAsync(scopes, parentOnly);
                var label = $"offered-profile-{pair + 1}-{(parentOnly ? "parent" : "dimensions")}";
                results.Add(new
                {
                    DiagnosticOnly = true, QualificationComplete = false, ParentOnly = parentOnly, Pair = pair + 1,
                    Source = Path.GetFullPath(source), OfferedLoad = offered, StartedAtUtc = startedAtUtc, EndedAtUtc = endedAtUtc,
                    DurationSeconds = timer.Elapsed.TotalSeconds, Cases = cases, MissedRequestDetails = misses.ToArray(),
                    DatabaseBefore = before, DatabaseAfter = after, SqlProfileBefore = sqlBefore, SqlProfileAfter = sqlAfter, SchedulerSamples = schedulerSamples, Errors = errors.ToArray(),
                    Note = "Frozen-load replay with SQL event tracing and optional external runtime tracing. This does not qualify any performance gate."
                });
                await WriteAsync(output, label + ".json", results[^1]);
                await CaptureSqlSchedulingEventsAsync(scopes, Path.Combine(output, label + "-sql-events.xml"));
                Console.WriteLine($"Offered profile {pair + 1}/{(parentOnly ? "parent" : "dimensions")}: operations={cases.Sum(item => item.Operations)}, misses={cases.Sum(item => item.MissedRequests)}");
                if (!errors.IsEmpty) return 1;
            }
        }

        return 0;
    }

    private static async Task StartSqlSchedulingEventsAsync(IServiceScopeFactory scopes, string output)
    {
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
        await context.Database.OpenConnectionAsync();
        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT p.name AS package,o.name AS event_name FROM sys.dm_xe_objects o JOIN sys.dm_xe_packages p ON p.guid=o.package_guid "
            + "WHERE o.object_type='event' AND ((p.name='sqlserver' AND o.name IN ('auto_stats','database_file_size_change')) OR (p.name='sqlos' AND o.name IN ('wait_info','wait_info_external')))";
        var available = new HashSet<string>(StringComparer.Ordinal);
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync()) available.Add(reader.GetString(0) + "." + reader.GetString(1));
        }

        string[] requested = ["sqlserver.auto_stats", "sqlserver.database_file_size_change", "sqlos.wait_info", "sqlos.wait_info_external"];
        if (requested.Any(name => !available.Contains(name))) throw new InvalidOperationException("The owned SQL Server lacks a required scheduling diagnostic event.");
        var events = requested.Select(name => $"ADD EVENT {name}(ACTION(sqlserver.database_name,sqlserver.session_id)"
            + (name.StartsWith("sqlos.", StringComparison.Ordinal) ? " WHERE ([duration]>(100) AND [opcode]=(1)))" : ")"));
        command.CommandText = "CREATE EVENT SESSION dimensions_scheduling_probe ON SERVER " + string.Join(",", events)
            + " ADD TARGET package0.event_file(SET filename=N'/var/opt/mssql/log/dimensions_scheduling_probe.xel',max_file_size=(64),max_rollover_files=(4)) WITH(MAX_MEMORY=4096 KB,MAX_DISPATCH_LATENCY=1 SECONDS); ALTER EVENT SESSION dimensions_scheduling_probe ON SERVER STATE=START;";
        await command.ExecuteNonQueryAsync();
        await WriteAsync(output, "sql-scheduling-probe.json", new { Events = requested, Target = "event_file", MaximumRetainedMiB = 256, LongWaitThresholdMilliseconds = 100, DiagnosticOnly = true });
    }

    private static async Task<object> CaptureSqlSchedulingStateAsync(IServiceScopeFactory scopes, bool parentOnly)
    {
        await using var scope = scopes.CreateAsyncScope();
        DbContext context = parentOnly ? scope.ServiceProvider.GetRequiredService<DimensionsParentOnlyContext>() : scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
        await context.Database.OpenConnectionAsync();
        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT DB_NAME() AS database_name,name,type_desc,size,growth,is_percent_growth FROM sys.database_files; "
            + "SELECT total_log_size_in_bytes,used_log_space_in_bytes,used_log_space_in_percent FROM sys.dm_db_log_space_usage; "
            + "SELECT OBJECT_NAME(s.object_id) AS table_name,s.name,p.last_updated,p.rows,p.rows_sampled,p.modification_counter FROM sys.stats s CROSS APPLY sys.dm_db_stats_properties(s.object_id,s.stats_id) p WHERE OBJECT_NAME(s.object_id) LIKE '__Storage_%'";
        return await ReadProfileResultsAsync(command);
    }

    private static async Task CaptureSqlSchedulingEventsAsync(IServiceScopeFactory scopes, string output)
    {
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
        await context.Database.OpenConnectionAsync();
        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT CONVERT(nvarchar(max),event_data) FROM sys.fn_xe_file_target_read_file('/var/opt/mssql/log/dimensions_scheduling_probe*.xel',NULL,NULL,NULL) ORDER BY file_name,file_offset";
        var events = new System.Text.StringBuilder("<Events source=\"event_file\">");
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync()) events.Append(reader.GetString(0));
        }

        events.Append("</Events>");
        await File.WriteAllTextAsync(output, events.ToString());
    }
}
