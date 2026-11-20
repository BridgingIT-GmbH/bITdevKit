// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

public static partial class StorageDimensionsQualification
{
    private static async Task<int> ProfileMixedAsync(IServiceScopeFactory scopes, string engine,
        IReadOnlyList<DimensionQualificationJob> jobs, MixedCommandProfile profile, string output, IEnumerable<QualificationQuery> queries)
    {
        var errors = new ConcurrentQueue<string>();
        var observed = jobs.Select(job => !job.Writer ? job : job with
        {
            Execute = async worker =>
            {
                using var operation = profile.Begin(job.Name, worker);
                return await job.Execute(worker);
            }
        }).ToArray();
        var variants = Enumerable.Range(1, 5).SelectMany(pair => new[]
        {
            (Label: $"pair-{pair}-parallel", MaxDop: 0, WritersOnly: false),
            (Label: $"pair-{pair}-serial", MaxDop: 1, WritersOnly: false)
        }).Append((Label: "writers-only", MaxDop: 0, WritersOnly: true));
        foreach (var variant in variants)
        {
            await ConfigureSqlMaxDopAsync(scopes, variant.MaxDop);
            if (variant.Label == "pair-1-serial")
            {
                var serialPlans = Path.Combine(output, "serial-query-plans");
                Directory.CreateDirectory(serialPlans);
                await CapturePlansAsync(scopes, engine, serialPlans, queries);
            }

            var selected = variant.WritersOnly ? observed.Where(job => job.Writer).ToArray() : observed;
            var label = variant.Label;
            var readers = variant.WritersOnly ? 0 : 16;
            await LoadAsync(selected, engine, TimeSpan.FromSeconds(15), errors, false, readerCount: readers);
            profile.Samples.Clear();
            var before = await CaptureSqlProfileAsync(scopes);
            using var sampling = new CancellationTokenSource();
            var samples = new List<object>();
            var sampler = SampleSqlRequestsAsync(scopes, samples, sampling.Token);
            var cpu = Process.GetCurrentProcess().TotalProcessorTime;
            var gcBefore = CaptureGCPauses();
            var timer = Stopwatch.StartNew();
            var cases = await LoadAsync(selected, engine, TimeSpan.FromSeconds(30), errors, true, readerCount: readers);
            timer.Stop();
            var cpuMilliseconds = (Process.GetCurrentProcess().TotalProcessorTime - cpu).TotalMilliseconds;
            var gcAfter = CaptureGCPauses();
            sampling.Cancel();
            await sampler;
            var after = await CaptureSqlProfileAsync(scopes);
            await WriteAsync(output, $"mixed-profile-{label}.json", new
            {
                QualificationComplete = false, DiagnosticOnly = true, OperationChecksPassed = errors.IsEmpty,
                Note = "Instrumentation and SQL request sampling perturb this diagnostic interval. It does not qualify latency or replace the full workload.",
                SqlMaxDop = variant.MaxDop, DurationSeconds = timer.Elapsed.TotalSeconds, ApplicationCpuMilliseconds = cpuMilliseconds,
                GCPausesBefore = gcBefore, GCPausesAfter = gcAfter,
                Cases = cases, WriterSamples = profile.Samples.ToArray(), WriterSamplesCapped = profile.Samples.Count == 20000, SqlSamples = samples,
                DatabaseBefore = before, DatabaseAfter = after, Errors = errors.ToArray()
            });
            Console.WriteLine($"Mixed profile {label} finished; failures={cases.Sum(item => item.Failures)}, writer samples={profile.Samples.Count}");
        }

        return errors.IsEmpty ? 0 : 1;
    }

    private static async Task ConfigureSqlMaxDopAsync(IServiceScopeFactory scopes, int maxDop)
    {
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
        await context.Database.OpenConnectionAsync();
        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "EXEC sp_configure 'show advanced options', 1; RECONFIGURE; EXEC sp_configure 'max degree of parallelism', @maxDop; RECONFIGURE;";
        var parameter = command.CreateParameter(); parameter.ParameterName = "@maxDop"; parameter.Value = maxDop; command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync();
        command.Parameters.Clear();
        command.CommandText = "SELECT CONVERT(int,value_in_use) FROM sys.configurations WHERE name='max degree of parallelism'";
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != maxDop)
            throw new InvalidOperationException("The owned server did not apply the requested query parallelism cap.");
    }

    private static async Task<object> CaptureSqlProfileAsync(IServiceScopeFactory scopes)
    {
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
        await context.Database.OpenConnectionAsync();
        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT wait_type,waiting_tasks_count,wait_time_ms,signal_wait_time_ms FROM sys.dm_os_wait_stats WHERE waiting_tasks_count>0; "
            + "SELECT CONVERT(varchar(64),s.query_hash,2) AS query_hash,s.execution_count,s.total_worker_time,s.total_elapsed_time,s.total_logical_reads,s.total_logical_writes, "
            + "SUBSTRING(t.text,(s.statement_start_offset/2)+1,((CASE s.statement_end_offset WHEN -1 THEN DATALENGTH(t.text) ELSE s.statement_end_offset END-s.statement_start_offset)/2)+1) AS statement_text "
            + "FROM sys.dm_exec_query_stats s CROSS APPLY sys.dm_exec_sql_text(s.sql_handle) t WHERE t.text LIKE '%__Storage_%'";
        return await ReadProfileResultsAsync(command);
    }

    private static async Task SampleSqlRequestsAsync(IServiceScopeFactory scopes, List<object> samples, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await using var scope = scopes.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<DimensionsQualificationContext>();
                await context.Database.OpenConnectionAsync(token);
                using var command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT r.session_id,r.status,r.command,r.wait_type,r.wait_time,r.blocking_session_id,r.cpu_time,r.total_elapsed_time,r.reads,r.logical_reads,r.writes "
                    + "FROM sys.dm_exec_requests r JOIN sys.dm_exec_sessions s ON s.session_id=r.session_id WHERE r.session_id<>@@SPID AND s.is_user_process=1; "
                    + "SELECT scheduler_id,runnable_tasks_count,current_tasks_count,work_queue_count FROM sys.dm_os_schedulers WHERE status='VISIBLE ONLINE'";
                samples.Add(new { AtUtc = DateTimeOffset.UtcNow, Results = await ReadProfileResultsAsync(command, token) });
                await Task.Delay(TimeSpan.FromSeconds(1), token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private static async Task<object> ReadProfileResultsAsync(DbCommand command, CancellationToken token = default)
    {
        var results = new List<object>();
        await using var reader = await command.ExecuteReaderAsync(token);
        do
        {
            var rows = new List<Dictionary<string, object>>();
            while (await reader.ReadAsync(token))
                rows.Add(Enumerable.Range(0, reader.FieldCount).ToDictionary(reader.GetName, i => reader.IsDBNull(i) ? null : reader.GetValue(i)));
            results.Add(rows);
        } while (await reader.NextResultAsync(token));
        return results;
    }

    private sealed class MixedCommandProfile
    {
        private static readonly AsyncLocal<Operation> Current = new();

        public ConcurrentQueue<object> Samples { get; } = new();

        public DbCommandInterceptor Commands { get; } = new CommandTimings();

        public DbTransactionInterceptor Transactions { get; } = new TransactionTimings();

        public IDisposable Begin(string name, int worker)
        {
            var operation = new Operation(this, name, worker);
            Current.Value = operation;
            return operation;
        }

        private sealed class Operation(MixedCommandProfile owner, string name, int worker) : IDisposable
        {
            private readonly long started = Stopwatch.GetTimestamp();
            private readonly List<object> steps = [];

            public void Record(string kind, double duration) => this.steps.Add(new
            {
                Kind = kind, DurationMilliseconds = duration, FinishedMilliseconds = Stopwatch.GetElapsedTime(this.started).TotalMilliseconds
            });

            public void Dispose()
            {
                if (owner.Samples.Count < 20000) owner.Samples.Enqueue(new
                {
                    Name = name, Worker = worker, ElapsedMilliseconds = Stopwatch.GetElapsedTime(this.started).TotalMilliseconds, Steps = this.steps
                });
                Current.Value = null;
            }
        }

        private sealed class CommandTimings : DbCommandInterceptor
        {
            public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
            {
                Current.Value?.Record(command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ? "read" : "save-batch", eventData.Duration.TotalMilliseconds);
                return ValueTask.FromResult(result);
            }

            public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
            {
                Current.Value?.Record("nonquery", eventData.Duration.TotalMilliseconds);
                return ValueTask.FromResult(result);
            }
        }

        private sealed class TransactionTimings : DbTransactionInterceptor
        {
            public override ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection, TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
            {
                Current.Value?.Record("begin", eventData.Duration.TotalMilliseconds);
                return ValueTask.FromResult(result);
            }

            public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
            {
                Current.Value?.Record("commit", eventData.Duration.TotalMilliseconds);
                return Task.CompletedTask;
            }
        }
    }
}
