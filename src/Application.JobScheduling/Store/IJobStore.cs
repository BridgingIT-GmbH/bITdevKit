// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

public interface IJobStore
{
    /// <summary>
    /// Retrieves all jobs currently registered with the scheduler.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A collection of job information objects.</returns>
    Task<IEnumerable<JobInfo>> GetJobsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves details for a specific job identified by its name and group.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The job information, or null if not found.</returns>
    Task<JobInfo> GetJobAsync(string jobName, string jobGroup, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves execution history for a specific job with optional filters.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="startDate">Filter runs starting on or after this date.</param>
    /// <param name="endDate">Filter runs ending on or before this date.</param>
    /// <param name="status">Filter by execution status (e.g., "Success", "Failed").</param>
    /// <param name="priority">Filter by trigger priority.</param>
    /// <param name="instanceName">Filter by scheduler instance name.</param>
    /// <param name="resultContains">Filter runs where the result contains this string.</param>
    /// <param name="take">Limit the number of runs returned.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A collection of job run records.</returns>
    Task<IEnumerable<JobRun>> GetJobRunsAsync(
        string jobName, string jobGroup,
        DateTimeOffset? startDate = null, DateTimeOffset? endDate = null,
        string status = null, int? priority = null, string instanceName = null,
        string resultContains = null, int? take = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves aggregated statistics for a job's execution history.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="startDate">Start of the date range for stats.</param>
    /// <param name="endDate">End of the date range for stats.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Statistics including total runs, success/failure counts, and runtime metrics.</returns>
    Task<JobRunStats> GetJobRunStatsAsync(
        string jobName, string jobGroup,
        DateTimeOffset? startDate, DateTimeOffset? endDate,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves all triggers associated with a specific job.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A collection of trigger information objects.</returns>
    Task<IEnumerable<TriggerInfo>> GetTriggersAsync(string jobName, string jobGroup, CancellationToken cancellationToken);

    /// <summary>
    /// Saves or updates a job run record in the store.
    /// </summary>
    /// <param name="jobRun">The job run record to save.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task SaveJobRunAsync(JobRun jobRun, CancellationToken cancellationToken);

    /// <summary>
    /// Triggers a job to run immediately with optional data.
    /// </summary>
    /// <param name="jobName">The name of the job to trigger.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="data">Optional data to pass to the job execution.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task TriggerJobAsync(string jobName, string jobGroup, IDictionary<string, object> data, CancellationToken cancellationToken);

    /// <summary>
    /// Pauses the execution of a specific job.
    /// </summary>
    /// <param name="jobName">The name of the job to pause.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task PauseJobAsync(string jobName, string jobGroup, CancellationToken cancellationToken);

    /// <summary>
    /// Resumes the execution of a paused job.
    /// </summary>
    /// <param name="jobName">The name of the job to resume.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task ResumeJobAsync(string jobName, string jobGroup, CancellationToken cancellationToken);

    /// <summary>
    /// Purges job run history older than a specified date.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="olderThan">Delete runs older than this date.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task PurgeJobRunsAsync(string jobName, string jobGroup, DateTimeOffset olderThan, CancellationToken cancellationToken);
}

public class JobInfo
{
    public string Name { get; set; }
    public string Group { get; set; }
    public string Description { get; set; }
    public string Type { get; set; }
    public string Status { get; set; }
    public int TriggerCount { get; set; }
    public JobRun LastRun { get; set; }
    public JobRunStats LastRunStats { get; set; }
    public string Category { get; set; }
    public IEnumerable<TriggerInfo> Triggers { get; set; }
}

public class JobRun
{
    public string Id { get; set; }
    public string JobName { get; set; }
    public string JobGroup { get; set; }

    public string Description { get; set; }
    public string TriggerName { get; set; }
    public string TriggerGroup { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public DateTimeOffset ScheduledTime { get; set; }
    public long? RunTimeMs { get; set; }
    public string Status { get; set; }
    public string ErrorMessage { get; set; }
    public IDictionary<string, object> Data { get; set; }
    public string InstanceName { get; set; }
    public int? Priority { get; set; }
    public string Result { get; set; }
    public int RetryCount { get; set; }
    public string Category { get; set; }
}

public class TriggerInfo
{
    public string Name { get; set; }
    public string Group { get; set; }
    public string Description { get; set; }
    public string CronExpression { get; set; }
    public DateTimeOffset? NextFireTime { get; set; }
    public DateTimeOffset? PreviousFireTime { get; set; }
    public string State { get; set; }
}

public class JobRunStats
{
    public int TotalRuns { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public double AvgRunTimeMs { get; set; }
    public long MaxRunTimeMs { get; set; }
    public long MinRunTimeMs { get; set; }
}