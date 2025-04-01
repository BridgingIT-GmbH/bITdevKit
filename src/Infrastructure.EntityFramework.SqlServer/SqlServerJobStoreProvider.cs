// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Text.Json;
using BridgingIT.DevKit.Application.JobScheduling;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class SqlServerJobStoreProvider : IJobStoreProvider
{
    private readonly ILogger<SqlServerJobStoreProvider> logger;
    private readonly string connectionString;
    private readonly string schema;
    private readonly string prefix;

    public SqlServerJobStoreProvider(ILoggerFactory loggerFactory, string connectionString, string tablePrefix)
    {
        this.logger = loggerFactory?.CreateLogger<SqlServerJobStoreProvider>() ?? NullLogger<SqlServerJobStoreProvider>.Instance;
        this.connectionString = connectionString;

        // Parse tablePrefix into schema and prefix, handling brackets correctly
        var cleanPrefix = tablePrefix.Replace("[", string.Empty).Replace("]", string.Empty); // "[dbo].[QRTZ_" -> "dbo.QRTZ_"
        var parts = cleanPrefix.Split(['.'], StringSplitOptions.RemoveEmptyEntries);
        this.schema = parts.Length > 1 ? parts[0] : "dbo"; // "dbo" (default if no schema)
        this.prefix = parts.Length > 1 ? parts[1] : parts[0]; // "QRTZ_"
    }

    public async Task<IEnumerable<JobRun>> GetJobRunsAsync(
        string jobName, string jobGroup,
        DateTimeOffset? startDate, DateTimeOffset? endDate,
        string status, int? priority, string instanceName,
        string resultContains, int? take,
        CancellationToken cancellationToken)
    {
        var runs = new List<JobRun>();
        var tableName = this.GetTableName("JOURNAL_TRIGGERS");
        var sql = $@"
            SELECT {(take.HasValue ? $"TOP {take.Value}" : "")}
                ENTRY_ID, TRIGGER_NAME, TRIGGER_GROUP, JOB_NAME, JOB_GROUP, DESCRIPTION,
                START_TIME, END_TIME, SCHEDULED_TIME, DURATION_MS, STATUS, ERROR_MESSAGE,
                JOB_DATA_JSON, INSTANCE_NAME, PRIORITY, RESULT, RETRY_COUNT, CATEGORY
            FROM {tableName}
            WHERE JOB_NAME = @jobName AND JOB_GROUP = @jobGroup
                {(startDate.HasValue ? "AND START_TIME >= @startDate" : "")}
                {(endDate.HasValue ? "AND START_TIME <= @endDate" : "")}
                {(status != null ? "AND STATUS = @status" : "")}
                {(priority.HasValue ? "AND PRIORITY = @priority" : "")}
                {(instanceName != null ? "AND INSTANCE_NAME = @instanceName" : "")}
                {(resultContains != null ? "AND RESULT LIKE @resultContains" : "")}
            ORDER BY START_TIME DESC";

        await using var connection = new SqlConnection(this.connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
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
        var tableName = this.GetTableName("JOURNAL_TRIGGERS");
        var sql = $@"
            SELECT 
                COUNT(*) as TotalRuns,
                SUM(CASE WHEN STATUS = 'Success' THEN 1 ELSE 0 END) as SuccessCount,
                SUM(CASE WHEN STATUS = 'Failed' THEN 1 ELSE 0 END) as FailureCount,
                SUM(CASE WHEN STATUS = 'Interrupted' THEN 1 ELSE 0 END) as InterruptCount,
                AVG(CAST(DURATION_MS AS FLOAT)) as AvgRunDurationMs,
                MAX(DURATION_MS) as MaxRunDurationMs,
                MIN(DURATION_MS) as MinRunDurationMs
            FROM {tableName}
            WHERE JOB_NAME = @jobName AND JOB_GROUP = @jobGroup
                {(startDate.HasValue ? "AND START_TIME >= @startDate" : "")}
                {(endDate.HasValue ? "AND START_TIME <= @endDate" : "")}";

        await using var connection = new SqlConnection(this.connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
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

    public async Task SaveJobRunAsync(JobRun jobRun, CancellationToken cancellationToken)
    {
        var tableName = this.GetTableName("JOURNAL_TRIGGERS");
        var sql = $@"
            IF EXISTS (SELECT 1 FROM {tableName} WHERE SCHED_NAME = @schedName AND ENTRY_ID = @entryId)
                UPDATE {tableName} 
                SET TRIGGER_NAME = @triggerName,
                    TRIGGER_GROUP = @triggerGroup,
                    JOB_NAME = @jobName,
                    JOB_GROUP = @jobGroup,
                    DESCRIPTION = @description,
                    START_TIME = @startTime,
                    END_TIME = @endTime,
                    SCHEDULED_TIME = @scheduledTime,
                    DURATION_MS = @durationMs,
                    STATUS = @status,
                    ERROR_MESSAGE = @errorMessage,
                    JOB_DATA_JSON = @jobDataJson,
                    INSTANCE_NAME = @instanceName,
                    PRIORITY = @priority,
                    RESULT = @result,
                    RETRY_COUNT = @retryCount,
                    CATEGORY = @category
                WHERE SCHED_NAME = @schedName AND ENTRY_ID = @entryId
            ELSE
                INSERT INTO {tableName} 
                VALUES (@schedName, @entryId, @triggerName, @triggerGroup, @jobName, @jobGroup, @description, 
                        @startTime, @endTime, @scheduledTime, @durationMs, @status, @errorMessage, @jobDataJson, 
                        @instanceName, @priority, @result, @retryCount, @category);";

        await using var connection = new SqlConnection(this.connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@schedName", "Scheduler");
        command.Parameters.AddWithValue("@entryId", jobRun.Id);
        command.Parameters.AddWithValue("@triggerName", jobRun.TriggerName);
        command.Parameters.AddWithValue("@triggerGroup", jobRun.TriggerGroup);
        command.Parameters.AddWithValue("@jobName", jobRun.JobName);
        command.Parameters.AddWithValue("@jobGroup", jobRun.JobGroup);
        command.Parameters.AddWithValue("@description", (object)jobRun.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("@startTime", jobRun.StartTime.UtcDateTime);
        command.Parameters.AddWithValue("@endTime", (object)jobRun.EndTime?.UtcDateTime ?? DBNull.Value);
        command.Parameters.AddWithValue("@scheduledTime", jobRun.ScheduledTime.UtcDateTime);
        command.Parameters.AddWithValue("@durationMs", (object)jobRun.DurationMs ?? DBNull.Value);
        command.Parameters.AddWithValue("@status", jobRun.Status);
        command.Parameters.AddWithValue("@errorMessage", (object)jobRun.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("@jobDataJson", JsonSerializer.Serialize(jobRun.Data));
        command.Parameters.AddWithValue("@instanceName", (object)jobRun.InstanceName ?? DBNull.Value);
        command.Parameters.AddWithValue("@priority", (object)jobRun.Priority ?? DBNull.Value);
        command.Parameters.AddWithValue("@result", (object)jobRun.Result ?? DBNull.Value);
        command.Parameters.AddWithValue("@retryCount", jobRun.RetryCount);
        command.Parameters.AddWithValue("@category", (object)jobRun.Category ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task PurgeJobRunsAsync(string jobName, string jobGroup, DateTimeOffset olderThan, CancellationToken cancellationToken)
    {
        var tableName = this.GetTableName("JOURNAL_TRIGGERS");
        var sql = $@"
            DELETE FROM {tableName}
            WHERE JOB_NAME = @jobName AND JOB_GROUP = @jobGroup AND START_TIME < @olderThan";

        await using var connection = new SqlConnection(this.connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@jobName", jobName);
        command.Parameters.AddWithValue("@jobGroup", jobGroup);
        command.Parameters.AddWithValue("@olderThan", olderThan.UtcDateTime);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private JobRun MapJobRun(SqlDataReader reader)
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
            Data = reader.IsDBNull(12) ? [] : JsonSerializer.Deserialize<Dictionary<string, object>>(reader.GetString(12)),
            InstanceName = reader.IsDBNull(13) ? null : reader.GetString(13),
            Priority = reader.IsDBNull(14) ? null : reader.GetInt32(14),
            Result = reader.IsDBNull(15) ? null : reader.GetString(15),
            RetryCount = reader.GetInt32(16),
            Category = reader.IsDBNull(17) ? null : reader.GetString(17)
        };
    }

    private string GetTableName(string table)
    {
        // Handle both bracketed (e.g., "[dbo].[QRTZ_") and unbracketed (e.g., "dbo.QRTZ_") prefixes
        return $"[{this.schema}].[{this.prefix}{table}]";
    }
}