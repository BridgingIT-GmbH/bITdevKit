// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using System.Linq;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;

public partial class JobStore(
    ILoggerFactory loggerFactory,
    ISchedulerFactory schedulerFactory,
    IJobStoreProvider provider) : IJobStore
{
    private readonly ILogger<JobStore> logger = loggerFactory?.CreateLogger<JobStore>() ?? NullLogger<JobStore>.Instance;

    /// <summary>
    /// Retrieves all jobs currently registered with the scheduler, enriched with persistent run data from the provider.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A collection of job information objects with latest run and stats.</returns>
    public async Task<IEnumerable<JobInfo>> GetJobsAsync(CancellationToken cancellationToken)
    {
        TypedLogger.LogGetJobs(this.logger, Constants.LogKey);

        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), cancellationToken);
        var jobs = new List<JobInfo>();

        foreach (var jobKey in jobKeys)
        {
            var detail = await scheduler.GetJobDetail(jobKey, cancellationToken);
            var triggers = await scheduler.GetTriggersOfJob(jobKey, cancellationToken);
            var runs = await this.GetJobRunsAsync(jobKey.Name, jobKey.Group, take: 1, cancellationToken: cancellationToken);
            var stats = await this.GetJobRunStatsAsync(jobKey.Name, jobKey.Group, null, null, cancellationToken);
            detail.JobDataMap.TryGetString("Category", out var category);

            jobs.Add(new JobInfo
            {
                Name = jobKey.Name,
                Group = jobKey.Group,
                Description = detail.Description,
                Type = detail.JobType.FullName,
                Status = await this.GetJobStatusAsync(jobKey, scheduler, cancellationToken),
                TriggerCount = triggers.Count,
                LastRun = runs.FirstOrDefault(),
                LastRunStats = stats,
                Category = category,
                Triggers = await Task.WhenAll(triggers.Select(async t => new TriggerInfo
                {
                    Name = t.Key.Name,
                    Group = t.Key.Group,
                    Description = t.Description,
                    CronExpression = t is ICronTrigger cron ? cron.CronExpressionString : null,
                    NextFireTime = t.GetNextFireTimeUtc(),
                    PreviousFireTime = t.GetPreviousFireTimeUtc(),
                    State = (await scheduler.GetTriggerState(t.Key, cancellationToken)).ToString()
                }))
            });
        }

        return jobs;
    }

    /// <summary>
    /// Retrieves details for a specific job identified by its name and group, enriched with persistent run data.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The job information with latest run and stats, or null if not found.</returns>
    public async Task<JobInfo> GetJobAsync(string jobName, string jobGroup, CancellationToken cancellationToken)
    {
        TypedLogger.LogGetJob(this.logger, Constants.LogKey, jobName, jobGroup);

        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var jobKey = new JobKey(jobName, jobGroup);
        var detail = await scheduler.GetJobDetail(jobKey, cancellationToken);
        if (detail == null)
        {
            this.logger.LogDebug("{LogKey} store: job not found (name={JobName}, group={JobGroup})", Constants.LogKey, jobName, jobGroup);
            return null;
        }

        var triggers = await scheduler.GetTriggersOfJob(jobKey, cancellationToken);
        var runs = await this.GetJobRunsAsync(jobName, jobGroup, take: 1, cancellationToken: cancellationToken);
        var stats = await this.GetJobRunStatsAsync(jobName, jobGroup, null, null, cancellationToken);
        detail.JobDataMap.TryGetString("Category", out var category);

        return new JobInfo
        {
            Name = jobKey.Name,
            Group = jobKey.Group,
            Description = detail.Description,
            Type = detail.JobType.FullName,
            Status = await this.GetJobStatusAsync(jobKey, scheduler, cancellationToken),
            TriggerCount = triggers.Count,
            LastRun = runs.FirstOrDefault(),
            LastRunStats = stats,
            Category = category,
            Triggers = await Task.WhenAll(triggers.Select(async t => new TriggerInfo
            {
                Name = t.Key.Name,
                Group = t.Key.Group,
                Description = t.Description,
                CronExpression = t is ICronTrigger cron ? cron.CronExpressionString : null,
                NextFireTime = t.GetNextFireTimeUtc(),
                PreviousFireTime = t.GetPreviousFireTimeUtc(),
                State = (await scheduler.GetTriggerState(t.Key, cancellationToken)).ToString()
            }))
        };
    }

    /// <summary>
    /// Retrieves execution history for a specific job from the database via the provider with optional filters.
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
    /// <returns>A collection of job run records from the database.</returns>
    public Task<IEnumerable<JobRun>> GetJobRunsAsync(
        string jobName, string jobGroup,
        DateTimeOffset? startDate = null, DateTimeOffset? endDate = null,
        string status = null, int? priority = null, string instanceName = null,
        string resultContains = null, int? take = null,
        CancellationToken cancellationToken = default)
    {
        TypedLogger.LogGetJobRuns(this.logger, Constants.LogKey, jobName, jobGroup);
        this.LogFilterOptions(startDate, endDate, status, priority, instanceName, resultContains, take);

        return provider.GetJobRunsAsync(jobName, jobGroup, startDate, endDate, status, priority, instanceName, resultContains, take, cancellationToken);
    }

    /// <summary>
    /// Retrieves aggregated statistics for a job's execution history from the database via the provider.
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
        TypedLogger.LogGetJobRunStats(this.logger, Constants.LogKey, jobName, jobGroup);
        this.LogDateRange(startDate, endDate);

        return provider.GetJobRunStatsAsync(jobName, jobGroup, startDate, endDate, cancellationToken);
    }

    /// <summary>
    /// Retrieves all triggers associated with a specific job from the live scheduler.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A collection of trigger information objects.</returns>
    public async Task<IEnumerable<TriggerInfo>> GetTriggersAsync(string jobName, string jobGroup, CancellationToken cancellationToken)
    {
        TypedLogger.LogGetTriggers(this.logger, Constants.LogKey, jobName, jobGroup);

        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var jobKey = new JobKey(jobName, jobGroup);
        var triggers = await scheduler.GetTriggersOfJob(jobKey, cancellationToken);
        return await Task.WhenAll(triggers.Select(async t => new TriggerInfo
        {
            Name = t.Key.Name,
            Group = t.Key.Group,
            Description = t.Description,
            CronExpression = t is ICronTrigger cron ? cron.CronExpressionString : null,
            NextFireTime = t.GetNextFireTimeUtc(),
            PreviousFireTime = t.GetPreviousFireTimeUtc(),
            State = (await scheduler.GetTriggerState(t.Key, cancellationToken)).ToString()
        }));
    }

    /// <summary>
    /// Saves or updates a job run record via the provider.
    /// </summary>
    /// <param name="jobRun">The job run record to save.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public Task SaveJobRunAsync(JobRun jobRun, CancellationToken cancellationToken)
    {
        TypedLogger.LogSaveJobRun(this.logger, Constants.LogKey, jobRun?.JobName, jobRun?.JobGroup, jobRun?.Id);

        return provider.SaveJobRunAsync(jobRun, cancellationToken);
    }

    /// <summary>
    /// Triggers a job to run immediately with optional data using the scheduler.
    /// </summary>
    /// <param name="jobName">The name of the job to trigger.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="data">Optional data to pass to the job execution.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task TriggerJobAsync(string jobName, string jobGroup, IDictionary<string, object> data, CancellationToken cancellationToken)
    {
        TypedLogger.LogTriggerJob(this.logger, Constants.LogKey, jobName, jobGroup);

        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var jobKey = new JobKey(jobName, jobGroup);
        var jobData = new JobDataMap(data ?? new Dictionary<string, object>());
        await scheduler.TriggerJob(jobKey, jobData, cancellationToken);
    }

    /// <summary>
    /// Pauses the execution of a specific job using the scheduler.
    /// </summary>
    /// <param name="jobName">The name of the job to pause.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task PauseJobAsync(string jobName, string jobGroup, CancellationToken cancellationToken)
    {
        TypedLogger.LogPauseJob(this.logger, Constants.LogKey, jobName, jobGroup);

        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        await scheduler.PauseJob(new JobKey(jobName, jobGroup), cancellationToken);
    }

    /// <summary>
    /// Resumes the execution of a paused job using the scheduler.
    /// </summary>
    /// <param name="jobName">The name of the job to resume.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task ResumeJobAsync(string jobName, string jobGroup, CancellationToken cancellationToken)
    {
        TypedLogger.LogResumeJob(this.logger, Constants.LogKey, jobName, jobGroup);

        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        await scheduler.ResumeJob(new JobKey(jobName, jobGroup), cancellationToken);
    }

    /// <summary>
    /// Purges job run history older than a specified date via the provider.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="olderThan">Delete runs older than this date.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public Task PurgeJobRunsAsync(string jobName, string jobGroup, DateTimeOffset olderThan, CancellationToken cancellationToken)
    {
        TypedLogger.LogPurgeJobRuns(this.logger, Constants.LogKey, jobName, jobGroup, olderThan);

        return provider.PurgeJobRunsAsync(jobName, jobGroup, olderThan, cancellationToken);
    }

    private async Task<string> GetJobStatusAsync(JobKey jobKey, IScheduler scheduler, CancellationToken cancellationToken)
    {
        var triggers = await scheduler.GetTriggersOfJob(jobKey, cancellationToken);
        if (triggers.Count == 0) return "No Triggers";

        var states = await Task.WhenAll(triggers.Select(t => scheduler.GetTriggerState(t.Key, cancellationToken)));

        return states.All(s => s == TriggerState.Paused) ? "Paused" : "Active";
    }

    private void LogFilterOptions(
        DateTimeOffset? startDate, DateTimeOffset? endDate, string status, int? priority,
        string instanceName, string resultContains, int? take)
    {
        this.LogDateRange(startDate, endDate);

        if (!string.IsNullOrEmpty(status))
            this.logger.LogDebug("{LogKey} store: filter status={Status}", Constants.LogKey, status);
        if (priority.HasValue)
            this.logger.LogDebug("{LogKey} store: filter priority={Priority}", Constants.LogKey, priority.Value);
        if (!string.IsNullOrEmpty(instanceName))
            this.logger.LogDebug("{LogKey} store: filter instanceName={InstanceName}", Constants.LogKey, instanceName);
        if (!string.IsNullOrEmpty(resultContains))
            this.logger.LogDebug("{LogKey} store: filter resultContains={ResultContains}", Constants.LogKey, resultContains);
        if (take.HasValue)
            this.logger.LogDebug("{LogKey} store: take={Take}", Constants.LogKey, take.Value);
    }

    private void LogDateRange(DateTimeOffset? startDate, DateTimeOffset? endDate)
    {
        if (startDate.HasValue && endDate.HasValue)
            this.logger.LogDebug("{LogKey} store: date range start={StartDate}, end={EndDate}", Constants.LogKey, startDate.Value, endDate.Value);
        else if (startDate.HasValue)
            this.logger.LogDebug("{LogKey} store: date range start={StartDate}", Constants.LogKey, startDate.Value);
        else if (endDate.HasValue)
            this.logger.LogDebug("{LogKey} store: date range end={EndDate}", Constants.LogKey, endDate.Value);
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Debug, "{LogKey} store: get jobs")]
        public static partial void LogGetJobs(ILogger logger, string logKey);

        [LoggerMessage(1, LogLevel.Debug, "{LogKey} store: get job (name={JobName}, group={JobGroup})")]
        public static partial void LogGetJob(ILogger logger, string logKey, string jobName, string jobGroup);

        [LoggerMessage(2, LogLevel.Debug, "{LogKey} store: get job runs (name={JobName}, group={JobGroup})")]
        public static partial void LogGetJobRuns(ILogger logger, string logKey, string jobName, string jobGroup);

        [LoggerMessage(3, LogLevel.Debug, "{LogKey} store: get job run stats (name={JobName}, group={JobGroup})")]
        public static partial void LogGetJobRunStats(ILogger logger, string logKey, string jobName, string jobGroup);

        [LoggerMessage(4, LogLevel.Debug, "{LogKey} store: get triggers (name={JobName}, group={JobGroup})")]
        public static partial void LogGetTriggers(ILogger logger, string logKey, string jobName, string jobGroup);

        [LoggerMessage(5, LogLevel.Debug, "{LogKey} store: save job run (name={JobName}, group={JobGroup}, id={EntryId})")]
        public static partial void LogSaveJobRun(ILogger logger, string logKey, string jobName, string jobGroup, string entryId);

        [LoggerMessage(6, LogLevel.Debug, "{LogKey} store: trigger job (name={JobName}, group={JobGroup})")]
        public static partial void LogTriggerJob(ILogger logger, string logKey, string jobName, string jobGroup);

        [LoggerMessage(7, LogLevel.Debug, "{LogKey} store: pause job (name={JobName}, group={JobGroup})")]
        public static partial void LogPauseJob(ILogger logger, string logKey, string jobName, string jobGroup);

        [LoggerMessage(8, LogLevel.Debug, "{LogKey} store: resume job (name={JobName}, group={JobGroup})")]
        public static partial void LogResumeJob(ILogger logger, string logKey, string jobName, string jobGroup);

        [LoggerMessage(9, LogLevel.Debug, "{LogKey} store: purge job runs (name={JobName}, group={JobGroup}, olderThan={OlderThan})")]
        public static partial void LogPurgeJobRuns(ILogger logger, string logKey, string jobName, string jobGroup, DateTimeOffset olderThan);
    }
}