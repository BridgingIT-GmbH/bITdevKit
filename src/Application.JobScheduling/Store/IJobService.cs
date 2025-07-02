// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

public interface IJobService
{
    /// <summary>
    /// Retrieves all jobs currently registered with the scheduler.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A collection of job information objects.</returns>
    Task<IEnumerable<JobInfo>> GetJobsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves details for a specific job identified by its name and group.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The job information, or null if not found.</returns>
    Task<JobInfo> GetJobAsync(string jobName, string jobGroup = null, CancellationToken cancellationToken = default);

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
        string jobName, string jobGroup = null,
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
        string jobName, string jobGroup = null,
        DateTimeOffset? startDate = null, DateTimeOffset? endDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all triggers associated with a specific job.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A collection of trigger information objects.</returns>
    Task<IEnumerable<TriggerInfo>> GetTriggersAsync(string jobName, string jobGroup = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or updates a job run record in the store.
    /// </summary>
    /// <param name="jobRun">The job run record to save.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task SaveJobRunAsync(JobRun jobRun, CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers a job to run immediately with optional data.
    /// </summary>
    /// <param name="jobName">The name of the job to trigger.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="data">Optional data to pass to the job execution.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task TriggerJobAsync(string jobName, string jobGroup = null, IDictionary<string, object> data = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers multiple jobs to run immediately with optional data for each job.
    /// </summary>
    /// <param name="jobNames">The collection of job names to trigger.</param>
    /// <param name="jobGroup">The common group all jobs belong to.</param>
    /// <param name="jobDatas">Optional dictionary mapping each job name to its specific data. Jobs with no entry will receive null data.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task TriggerJobsAsync(IEnumerable<string> jobNames, string jobGroup = null, IDictionary<string, object> data = null, IDictionary<string, IDictionary<string, object>> jobDatas = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers a job to run immediately with optional data and waits for its completion.
    /// </summary>
    /// <param name="jobName">The name of the job to trigger.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="data">Optional data to pass to the job execution.</param>
    /// <param name="checkInterval">The time interval in milliseconds between status checks. Default is 1000ms.</param>
    /// <param name="timeout">The maximum time to wait for the job to complete. Default is 10 minutes.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The final job information with execution results.</returns>
    /// <exception cref="TimeoutException">Thrown when the job does not complete within the specified timeout period.</exception>
    Task<JobInfo> TriggerJobAndWaitAsync(string jobName, string jobGroup = null, IDictionary<string, object> data = null, int checkInterval = 1000, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers multiple jobs to run immediately with optional data for each job and waits for their completion.
    /// </summary>
    /// <param name="jobNames">The collection of job names to trigger.</param>
    /// <param name="jobGroup">The common group all jobs belong to.</param>
    /// <param name="jobData">Optional dictionary mapping each job name to its specific data. Jobs with no entry will receive null data.</param>
    /// <param name="sequentially">Whether to run jobs sequentially (true) or concurrently (false). Default is false (concurrent).</param>
    /// <param name="checkInterval">The time interval in milliseconds between status checks. Default is 1000ms.</param>
    /// <param name="timeout">The maximum time to wait for all jobs to complete. Default is 10 minutes.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A dictionary mapping job names to their final job information with execution results.</returns>
    /// <exception cref="ArgumentException">Thrown when jobNames is null or empty.</exception>
    /// <exception cref="TimeoutException">Thrown when one or more jobs do not complete within the specified timeout period.</exception>
    Task<IDictionary<string, JobInfo>> TriggerJobsAndWaitAsync(IEnumerable<string> jobNames, string jobGroup = null, IDictionary<string, object> data = null, IDictionary<string, IDictionary<string, object>> jobData = null, bool sequentially = true, bool continueOnFailed = false, int checkInterval = 1000, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Interrupts a scheduled job identified by its name and group. The operation can be canceled using a provided
    /// token.
    /// </summary>
    /// <param name="jobName">Specifies the name of the job to be interrupted.</param>
    /// <param name="jobGroup">Indicates the group to which the job belongs.</param>
    /// <param name="cancellationToken">Allows the operation to be canceled if needed.</param>
    Task InterruptJobAsync(string jobName, string jobGroup = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses the execution of a specific job.
    /// </summary>
    /// <param name="jobName">The name of the job to pause.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task PauseJobAsync(string jobName, string jobGroup = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes the execution of a paused job.
    /// </summary>
    /// <param name="jobName">The name of the job to resume.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task ResumeJobAsync(string jobName, string jobGroup = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Purges job run history older than a specified date.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="olderThan">Delete runs older than this date.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task PurgeJobRunsAsync(string jobName, string jobGroup, DateTimeOffset? olderThan = null, CancellationToken cancellationToken = default);
}
