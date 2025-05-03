// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Text.Json;
using BridgingIT.DevKit.Application.JobScheduling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

public class PostgresJobStoreProvider : IJobStoreProvider
{
    private readonly ILogger<PostgresJobStoreProvider> logger;
    private readonly string connectionString;
    private readonly string schema;
    private readonly string prefix;

    public PostgresJobStoreProvider(ILoggerFactory loggerFactory, string connectionString, string tablePrefix)
    {
        this.logger = loggerFactory?.CreateLogger<PostgresJobStoreProvider>() ?? NullLogger<PostgresJobStoreProvider>.Instance;
        this.connectionString = connectionString;

        // Parse tablePrefix into schema and prefix, handling brackets or dots
        var cleanPrefix = tablePrefix.Replace("[", string.Empty).Replace("]", string.Empty); // "[public].[qrtz_" -> "public.qrtz_"
        var parts = cleanPrefix.Split(['.'], StringSplitOptions.RemoveEmptyEntries);
        this.schema = parts.Length > 1 ? parts[0] : "public"; // Default to "public" if no schema
        this.prefix = parts.Length > 1 ? parts[1] : parts[0]; // Prefix is after schema or the whole string
    }

    public async Task<IEnumerable<JobRun>> GetJobRunsAsync(
        string jobName, string jobGroup,
        DateTimeOffset? startDate, DateTimeOffset? endDate,
        string status, int? priority, string instanceName,
        string resultContains, int? take,
        CancellationToken cancellationToken)
    {
        var runs = new List<JobRun>();
        var tableName = this.GetTableName("journal_triggers");
        var sql = $@"
            SELECT 
                entry_id, trigger_name, trigger_group, job_name, job_group, description,
                start_time, end_time, scheduled_time, duration_ms, status, error_message,
                job_data_json, instance_name, priority, result, retry_count, category
            FROM {tableName}
            WHERE job_name = @jobName AND job_group = @jobGroup
                {(startDate.HasValue ? "AND start_time >= @startDate" : "")}
                {(endDate.HasValue ? "AND start_time <= @endDate" : "")}
                {(status != null ? "AND status = @status" : "")}
                {(priority.HasValue ? "AND priority = @priority" : "")}
                {(instanceName != null ? "AND instance_name = @instanceName" : "")}
                {(resultContains != null ? "AND result ILIKE @resultContains" : "")}
            ORDER BY start_time DESC
            {(take.HasValue ? $"LIMIT {take.Value}" : "")}";

        await using var connection = new NpgsqlConnection(this.connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@jobName", jobName);
        command.Parameters.AddWithValue("@jobGroup", jobGroup);
        if (startDate.HasValue) command.Parameters.AddWithValue("@startDate", startDate.Value.UtcDateTime);
        if (endDate.HasValue) command.Parameters.AddWithValue("@endDate", endDate.Value.UtcDateTime);
        if (status != null) command.Parameters.AddWithValue("@status", status);
        if (priority.HasValue) command.Parameters.AddWithValue("@priority", priority.Value);
        if (instanceName != null) command.Parameters.AddWithValue("@instanceName", instanceName);
        if (resultContains != null) command.Parameters.AddWithValue("@resultContains", $"%{resultContains}%");

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            runs.Add(this.MapJobRun(reader));
        }

        return runs;
    }

    public async Task<JobRunStats> GetJobRunStatsAsync(
        string jobName, string jobGroup,
        DateTimeOffset? startDate, DateTimeOffset? endDate,
        CancellationToken cancellationToken)
    {
        var tableName = this.GetTableName("journal_triggers");
        var sql = $@"
            SELECT 
                COUNT(*) as total_runs,
                SUM(CASE WHEN status = 'Success' THEN 1 ELSE 0 END) as success_count,
                SUM(CASE WHEN status = 'Failed' THEN 1 ELSE 0 END) as failure_count,
                SUM(CASE WHEN status = 'Interrupted' THEN 1 ELSE 0 END) as interrupt_count,
                AVG(duration_ms) as avg_duration_ms,
                MAX(duration_ms) as max_duration_ms,
                MIN(duration_ms) as min_duration_ms
            FROM {tableName}
            WHERE job_name = @jobName AND job_group = @jobGroup
                {(startDate.HasValue ? "AND start_time >= @startDate" : "")}
                {(endDate.HasValue ? "AND start_time <= @endDate" : "")}";

        await using var connection = new NpgsqlConnection(this.connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@jobName", jobName);
        command.Parameters.AddWithValue("@jobGroup", jobGroup);
        if (startDate.HasValue) command.Parameters.AddWithValue("@startDate", startDate.Value.UtcDateTime);
        if (endDate.HasValue) command.Parameters.AddWithValue("@endDate", endDate.Value.UtcDateTime);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new JobRunStats
            {
                TotalRuns = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                SuccessCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                FailureCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                InterruptCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                AvgRunDurationMs = reader.IsDBNull(4) ? 0 : reader.GetDouble(4),
                MaxRunDurationMs = reader.IsDBNull(5) ? 0 : reader.GetInt64(5),
                MinRunDurationMs = reader.IsDBNull(6) ? 0 : reader.GetInt64(6)
            };
        }

        return new JobRunStats();
    }

    public async Task PurgeJobRunsAsync(string jobName, string jobGroup, DateTimeOffset olderThan, CancellationToken cancellationToken)
    {
        var tableName = this.GetTableName("journal_triggers");
        var sql = $@"
            DELETE FROM {tableName}
            WHERE job_name = @jobName AND job_group = @jobGroup AND start_time < @olderThan";

        await using var connection = new NpgsqlConnection(this.connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@jobName", jobName);
        command.Parameters.AddWithValue("@jobGroup", jobGroup);
        command.Parameters.AddWithValue("@olderThan", olderThan.UtcDateTime);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveJobRunAsync(JobRun jobRun, CancellationToken cancellationToken)
    {
        var tableName = this.GetTableName("journal_triggers");
        var sql = $@"
            INSERT INTO {tableName} (
                sched_name, entry_id, trigger_name, trigger_group, job_name, job_group, description,
                start_time, end_time, scheduled_time, duration_ms, status, error_message, job_data_json,
                instance_name, priority, result, retry_count, category
            ) VALUES (
                @schedName, @entryId, @triggerName, @triggerGroup, @jobName, @jobGroup, @description,
                @startTime, @endTime, @scheduledTime, @runTimeMs, @status, @errorMessage, @jobDataJson,
                @instanceName, @priority, @result, @retryCount, @category
            )
            ON CONFLICT (sched_name, entry_id) DO UPDATE SET
                trigger_name = EXCLUDED.trigger_name,
                trigger_group = EXCLUDED.trigger_group,
                job_name = EXCLUDED.job_name,
                job_group = EXCLUDED.job_group,
                description = EXCLUDED.description,
                start_time = EXCLUDED.start_time,
                end_time = EXCLUDED.end_time,
                scheduled_time = EXCLUDED.scheduled_time,
                duration_ms = EXCLUDED.duration_ms,
                status = EXCLUDED.status,
                error_message = EXCLUDED.error_message,
                job_data_json = EXCLUDED.job_data_json,
                instance_name = EXCLUDED.instance_name,
                priority = EXCLUDED.priority,
                result = EXCLUDED.result,
                retry_count = EXCLUDED.retry_count,
                category = EXCLUDED.category;";

        await using var connection = new NpgsqlConnection(this.connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@schedName", "Scheduler"); // Default scheduler name
        command.Parameters.AddWithValue("@entryId", jobRun.Id);
        command.Parameters.AddWithValue("@triggerName", jobRun.TriggerName);
        command.Parameters.AddWithValue("@triggerGroup", jobRun.TriggerGroup);
        command.Parameters.AddWithValue("@jobName", jobRun.JobName);
        command.Parameters.AddWithValue("@jobGroup", jobRun.JobGroup);
        command.Parameters.AddWithValue("@description", (object)jobRun.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("@startTime", jobRun.StartTime.UtcDateTime);
        command.Parameters.AddWithValue("@endTime", (object)jobRun.EndTime?.UtcDateTime ?? DBNull.Value);
        command.Parameters.AddWithValue("@scheduledTime", jobRun.ScheduledTime.UtcDateTime);
        command.Parameters.AddWithValue("@runTimeMs", (object)jobRun.DurationMs ?? DBNull.Value);
        command.Parameters.AddWithValue("@status", jobRun.Status);
        command.Parameters.AddWithValue("@errorMessage", (object)jobRun.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("@jobDataJson", JsonSerializer.Serialize(jobRun.Data));
        command.Parameters.AddWithValue("@instanceName", (object)jobRun.InstanceName ?? DBNull.Value);
        command.Parameters.AddWithValue("@priority", (object)jobRun.Priority ?? DBNull.Value);
        command.Parameters.AddWithValue("@result", (object)jobRun.Result ?? DBNull.Value);
        command.Parameters.AddWithValue("@retryCount", jobRun.RetryCount);
        command.Parameters.AddWithValue("@category", (object)jobRun.Category ?? DBNull.Value);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (NpgsqlException ex) when (ex.SqlState == "42P01") // Relation (table) does not exist, Silently ignore the error when the table doesn't exist
        {
            return;
        }
        catch (Exception)
        {
            throw;
        }
    }

    private JobRun MapJobRun(NpgsqlDataReader reader)
    {
        return new JobRun
        {
            Id = reader.GetString(0),
            TriggerName = reader.GetString(1),
            TriggerGroup = reader.GetString(2),
            JobName = reader.GetString(3),
            JobGroup = reader.GetString(4),
            Description = reader.IsDBNull(5) ? null : reader.GetString(5),
            StartTime = reader.GetDateTime(6),
            EndTime = reader.IsDBNull(7) ? null : (DateTimeOffset?)reader.GetDateTime(7),
            ScheduledTime = reader.GetDateTime(8),
            DurationMs = reader.IsDBNull(9) ? null : reader.GetInt64(9),
            Status = reader.GetString(10),
            ErrorMessage = reader.IsDBNull(11) ? null : reader.GetString(11),
            Data = reader.IsDBNull(12) ? new Dictionary<string, object>() : JsonSerializer.Deserialize<Dictionary<string, object>>(reader.GetString(12)),
            InstanceName = reader.IsDBNull(13) ? null : reader.GetString(13),
            Priority = reader.IsDBNull(14) ? null : reader.GetInt32(14),
            Result = reader.IsDBNull(15) ? null : reader.GetString(15),
            RetryCount = reader.GetInt32(16),
            Category = reader.IsDBNull(17) ? null : reader.GetString(17)
        };
    }

    private string GetTableName(string table)
    {
        // Construct a properly quoted PostgreSQL table name
        return $"\"{this.schema}\".\"{this.prefix}{table}\"";
    }
}