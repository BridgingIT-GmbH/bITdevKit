// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System.Data.Common;
using System.Data.SQLite;
using System.Text.Json;
using BridgingIT.DevKit.Application.JobScheduling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class SqliteJobStoreProvider : IJobStoreProvider
{
    private readonly ILogger<SqliteJobStoreProvider> logger;
    private readonly string connectionString;
    private readonly string tablePrefix;

    public SqliteJobStoreProvider(ILoggerFactory loggerFactory, string connectionString, string tablePrefix)
    {
        this.logger = loggerFactory?.CreateLogger<SqliteJobStoreProvider>() ?? NullLogger<SqliteJobStoreProvider>.Instance;
        this.connectionString = connectionString;
        this.tablePrefix = tablePrefix;
    }

    /// <summary>
    /// Retrieves job run history from the SQLite database with specified filters.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="startDate">Filter runs starting on or after this date.</param>
    /// <param name="endDate">Filter runs ending on or before this date.</param>
    /// <param name="status">Filter by execution status.</param>
    /// <param name="priority">Filter by trigger priority.</param>
    /// <param name="instanceName">Filter by scheduler instance name.</param>
    /// <param name="resultContains">Filter runs where the result contains this string.</param>
    /// <param name="take">Limit the number of runs returned.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A collection of job run records from the SQLite database.</returns>
    public async Task<IEnumerable<JobRun>> GetJobRunsAsync(
        string jobName, string jobGroup,
        DateTimeOffset? startDate, DateTimeOffset? endDate,
        string status, int? priority, string instanceName,
        string resultContains, int? take,
        CancellationToken cancellationToken)
    {
        var runs = new List<JobRun>();
        var sql = $@"
            SELECT 
                ENTRY_ID, TRIGGER_NAME, TRIGGER_GROUP, JOB_NAME, JOB_GROUP, DESCRIPTION,
                START_TIME, END_TIME, SCHEDULED_TIME, RUN_TIME_MS, STATUS, ERROR_MESSAGE,
                JOB_DATA_JSON, INSTANCE_NAME, PRIORITY, RESULT, RETRY_COUNT, CATEGORY
            FROM {this.tablePrefix}JOURNAL_TRIGGERS
            WHERE JOB_NAME = @jobName AND JOB_GROUP = @jobGroup
                {(startDate.HasValue ? "AND START_TIME >= @startDate" : "")}
                {(endDate.HasValue ? "AND START_TIME <= @endDate" : "")}
                {(status != null ? "AND STATUS = @status" : "")}
                {(priority.HasValue ? "AND PRIORITY = @priority" : "")}
                {(instanceName != null ? "AND INSTANCE_NAME = @instanceName" : "")}
                {(resultContains != null ? "AND RESULT LIKE @resultContains" : "")}
            ORDER BY START_TIME DESC
            {(take.HasValue ? $"LIMIT {take.Value}" : "")}";

        await using var connection = new SQLiteConnection(this.connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SQLiteCommand(sql, connection);
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

    /// <summary>
    /// Retrieves aggregated statistics for a job's execution history from the SQLite database.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="startDate">Start of the date range for stats.</param>
    /// <param name="endDate">End of the date range for stats.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Statistics including total runs, success/failure counts, and runtime metrics.</returns>
    public async Task<JobRunStats> GetJobRunStatsAsync(
        string jobName, string jobGroup,
        DateTimeOffset? startDate, DateTimeOffset? endDate,
        CancellationToken cancellationToken)
    {
        var sql = $@"
            SELECT 
                COUNT(*) as TotalRuns,
                SUM(CASE WHEN STATUS = 'Success' THEN 1 ELSE 0 END) as SuccessCount,
                SUM(CASE WHEN STATUS = 'Failed' THEN 1 ELSE 0 END) as FailureCount,
                AVG(RUN_TIME_MS) as AvgRunTimeMs,
                MAX(RUN_TIME_MS) as MaxRunTimeMs,
                MIN(RUN_TIME_MS) as MinRunTimeMs
            FROM {this.tablePrefix}JOURNAL_TRIGGERS
            WHERE JOB_NAME = @jobName AND JOB_GROUP = @jobGroup
                {(startDate.HasValue ? "AND START_TIME >= @startDate" : "")}
                {(endDate.HasValue ? "AND START_TIME <= @endDate" : "")}";

        await using var connection = new SQLiteConnection(this.connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SQLiteCommand(sql, connection);
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
                AvgRunTimeMs = reader.IsDBNull(3) ? 0 : reader.GetDouble(3),
                MaxRunTimeMs = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                MinRunTimeMs = reader.IsDBNull(5) ? 0 : reader.GetInt64(5)
            };
        }

        return new JobRunStats();
    }

    /// <summary>
    /// Deletes job run history older than a specified date from the SQLite database.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="olderThan">Delete runs older than this date.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task PurgeJobRunsAsync(string jobName, string jobGroup, DateTimeOffset olderThan, CancellationToken cancellationToken)
    {
        var sql = $@"
            DELETE FROM {this.tablePrefix}JOURNAL_TRIGGERS
            WHERE JOB_NAME = @jobName AND JOB_GROUP = @jobGroup AND START_TIME < @olderThan";

        await using var connection = new SQLiteConnection(this.connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SQLiteCommand(sql, connection);
        command.Parameters.AddWithValue("@jobName", jobName);
        command.Parameters.AddWithValue("@jobGroup", jobGroup);
        command.Parameters.AddWithValue("@olderThan", olderThan.UtcDateTime);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Saves or updates a job run record in the SQLite database.
    /// </summary>
    /// <param name="jobRun">The job run record to save.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task SaveJobRunAsync(JobRun jobRun, CancellationToken cancellationToken)
    {
        var sql = $@"
            INSERT OR REPLACE INTO {this.tablePrefix}JOURNAL_TRIGGERS (
                SCHED_NAME, ENTRY_ID, TRIGGER_NAME, TRIGGER_GROUP, JOB_NAME, JOB_GROUP, DESCRIPTION,
                START_TIME, END_TIME, SCHEDULED_TIME, RUN_TIME_MS, STATUS, ERROR_MESSAGE, JOB_DATA_JSON,
                INSTANCE_NAME, PRIORITY, RESULT, RETRY_COUNT, CATEGORY
            ) VALUES (
                @schedName, @entryId, @triggerName, @triggerGroup, @jobName, @jobGroup, @description,
                @startTime, @endTime, @scheduledTime, @runTimeMs, @status, @errorMessage, @jobDataJson,
                @instanceName, @priority, @result, @retryCount, @category
            );";

        await using var connection = new SQLiteConnection(this.connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SQLiteCommand(sql, connection);
        command.Parameters.AddWithValue("@schedName", "Scheduler"); // Assuming a default scheduler name
        command.Parameters.AddWithValue("@entryId", jobRun.Id);
        command.Parameters.AddWithValue("@triggerName", jobRun.TriggerName);
        command.Parameters.AddWithValue("@triggerGroup", jobRun.TriggerGroup);
        command.Parameters.AddWithValue("@jobName", jobRun.JobName);
        command.Parameters.AddWithValue("@jobGroup", jobRun.JobGroup);
        command.Parameters.AddWithValue("@description", (object)jobRun.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("@startTime", jobRun.StartTime.UtcDateTime);
        command.Parameters.AddWithValue("@endTime", (object)jobRun.EndTime?.UtcDateTime ?? DBNull.Value);
        command.Parameters.AddWithValue("@scheduledTime", jobRun.ScheduledTime.UtcDateTime);
        command.Parameters.AddWithValue("@runTimeMs", (object)jobRun.RunTimeMs ?? DBNull.Value);
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

    private JobRun MapJobRun(DbDataReader reader)
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
            RunTimeMs = reader.IsDBNull(9) ? null : reader.GetInt64(9),
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
}