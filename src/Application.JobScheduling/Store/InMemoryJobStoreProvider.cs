// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

public class InMemoryJobStoreProvider(ILoggerFactory loggerFactory, TimeSpan? retentionPeriod = null) : IJobStoreProvider
{
    private readonly ILogger<InMemoryJobStoreProvider> logger = loggerFactory?.CreateLogger<InMemoryJobStoreProvider>() ?? NullLogger<InMemoryJobStoreProvider>.Instance;
    private readonly ConcurrentDictionary<string, JobRun> jobRuns = [];
    private readonly TimeSpan retentionPeriod = retentionPeriod ?? TimeSpan.FromHours(1);

    /// <summary>
    /// Retrieves job run history from in-memory storage with specified filters, cleaning up old records based on retention.
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
    /// <returns>A collection of job run records from in-memory storage.</returns>
    public Task<IEnumerable<JobRun>> GetJobRunsAsync(
        string jobName, string jobGroup,
        DateTimeOffset? startDate, DateTimeOffset? endDate,
        string status, int? priority, string instanceName,
        string resultContains, int? take,
        CancellationToken cancellationToken)
    {
        this.CleanupOldRuns();

        var runs = this.jobRuns.Values
            .Where(r => r.JobName == jobName && r.JobGroup == jobGroup)
            .Where(r => !startDate.HasValue || r.StartTime >= startDate.Value)
            .Where(r => !endDate.HasValue || r.StartTime <= endDate.Value)
            .Where(r => string.IsNullOrEmpty(status) || r.Status == status)
            .Where(r => !priority.HasValue || r.Priority == priority.Value)
            .Where(r => string.IsNullOrEmpty(instanceName) || r.InstanceName == instanceName)
            .Where(r => string.IsNullOrEmpty(resultContains) || (r.Result?.Contains(resultContains, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderByDescending(r => r.StartTime);

        if (take.HasValue)
        {
            runs = runs.Take(take.Value).OrderByDescending(r => r.StartTime);
        }

        return Task.FromResult(runs.AsEnumerable());
    }

    /// <summary>
    /// Retrieves aggregated statistics for a job's execution history from in-memory storage, cleaning up old records.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="startDate">Start of the date range for stats.</param>
    /// <param name="endDate">End of the date range for stats.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Statistics including total runs, success/failure counts, and runtime metrics.</returns>
    public Task<JobRunStats> GetJobRunStatsAsync(
        string jobName, string jobGroup,
        DateTimeOffset? startDate, DateTimeOffset? endDate,
        CancellationToken cancellationToken)
    {
        this.CleanupOldRuns();

        var runs = this.jobRuns.Values
            .Where(r => r.JobName == jobName && r.JobGroup == jobGroup)
            .Where(r => !startDate.HasValue || r.StartTime >= startDate.Value)
            .Where(r => !endDate.HasValue || r.StartTime <= endDate.Value);

        if (!runs.Any())
        {
            return Task.FromResult(new JobRunStats());
        }

        var stats = new JobRunStats
        {
            TotalRuns = runs.Count(),
            SuccessCount = runs.Count(r => r.Status == "Success"),
            FailureCount = runs.Count(r => r.Status == "Failed"),
            AvgRunDurationMs = runs.Where(r => r.DurationMs.HasValue).Average(r => r.DurationMs.Value),
            MaxRunDurationMs = runs.Where(r => r.DurationMs.HasValue).Max(r => r.DurationMs.Value),
            MinRunDurationMs = runs.Where(r => r.DurationMs.HasValue).Min(r => r.DurationMs.Value)
        };

        return Task.FromResult(stats);
    }

    /// <summary>
    /// Deletes job run history older than the retention period from in-memory storage.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="olderThan">Delete runs older than this date.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public Task PurgeJobRunsAsync(string jobName, string jobGroup, DateTimeOffset olderThan, CancellationToken cancellationToken)
    {
        var toRemove = this.jobRuns.Values
            .Where(r => r.JobName == jobName && r.JobGroup == jobGroup && r.StartTime < olderThan)
            .Select(r => r.Id);

        foreach (var id in toRemove)
        {
            this.jobRuns.TryRemove(id, out _);
        }

        this.logger.LogInformation("Purged {Count} job runs for {JobName} ({JobGroup}) older than {OlderThan}", toRemove.Count(), jobName, jobGroup, olderThan);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Saves or updates a job run record in in-memory storage, cleaning up old records based on retention.
    /// </summary>
    /// <param name="jobRun">The job run record to save.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public Task SaveJobRunAsync(JobRun jobRun, CancellationToken cancellationToken)
    {
        this.CleanupOldRuns();
        this.jobRuns[jobRun.Id] = jobRun;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes job runs that are older than a specified retention period. Logs the number of removed job runs if any
    /// are cleaned up.
    /// </summary>
    private void CleanupOldRuns()
    {
        var cutoff = DateTimeOffset.UtcNow - this.retentionPeriod;
        var toRemove = this.jobRuns.Values
            .Where(r => r.StartTime < cutoff)
            .Select(r => r.Id).ToList();

        foreach (var id in toRemove)
        {
            this.jobRuns.TryRemove(id, out _);
        }

        if (toRemove.Count != 0)
        {
            this.logger.LogDebug("Cleaned up {Count} job runs older than {RetentionPeriod}", toRemove.Count, this.retentionPeriod);
        }
    }
}
