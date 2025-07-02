// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using System.Linq;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;

public partial class JobService(
    ILoggerFactory loggerFactory,
    ISchedulerFactory schedulerFactory,
    IJobStoreProvider provider) : IJobService
{
    private readonly ILogger<JobService> logger = loggerFactory?.CreateLogger<JobService>() ?? NullLogger<JobService>.Instance;

    /// <summary>
    /// Retrieves all jobs currently registered with the scheduler, enriched with persistent run data from the provider.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A collection of job information objects with latest run and stats.</returns>
    public async Task<IEnumerable<JobInfo>> GetJobsAsync(CancellationToken cancellationToken = default)
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
    public async Task<JobInfo> GetJobAsync(string jobName, string jobGroup = null, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNullOrEmpty(jobName, nameof(jobName));
        jobGroup ??= "DEFAULT";
        TypedLogger.LogGetJob(this.logger, Constants.LogKey, jobName, jobGroup);

        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var jobKey = new JobKey(jobName, jobGroup);
        var detail = await scheduler.GetJobDetail(jobKey, cancellationToken);
        if (detail == null)
        {
            this.logger.LogDebug("{LogKey} jobservice: job not found (name={JobName}, group={JobGroup})", Constants.LogKey, jobName, jobGroup);
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
    public async Task<IEnumerable<JobRun>> GetJobRunsAsync(
        string jobName, string jobGroup = null,
        DateTimeOffset? startDate = null, DateTimeOffset? endDate = null,
        string status = null, int? priority = null, string instanceName = null,
        string resultContains = null, int? take = null,
        CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNullOrEmpty(jobName, nameof(jobName));
        jobGroup ??= "DEFAULT";
        TypedLogger.LogGetJobRuns(this.logger, Constants.LogKey, jobName, jobGroup);
        this.LogFilterOptions(startDate, endDate, status, priority, instanceName, resultContains, take);

        var runs = await provider.GetJobRunsAsync(jobName, jobGroup, startDate, endDate, status, priority, instanceName, resultContains, take, cancellationToken);

        foreach (var run in runs.SafeNull()) // fix the duration for still running jobs (no endtime/duration)
        {
            if (run.Status == "Started"/* && run.EndTime == null && run.DurationMs == null*/)
            {
                // Create a new DateTimeOffset from the original local time, but with zero offset for the calculation
                var startTime = new DateTimeOffset(run.StartTime.DateTime, TimeSpan.Zero);
                var now = new DateTimeOffset(DateTimeOffset.UtcNow.DateTime, TimeSpan.Zero);
                run.DurationMs = (long)(now - startTime).TotalMilliseconds;
            }
        }

        return runs;
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
        string jobName, string jobGroup = null,
        DateTimeOffset? startDate = null, DateTimeOffset? endDate = null,
        CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNullOrEmpty(jobName, nameof(jobName));
        jobGroup ??= "DEFAULT";
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
    public async Task<IEnumerable<TriggerInfo>> GetTriggersAsync(string jobName, string jobGroup = null, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNullOrEmpty(jobName, nameof(jobName));
        jobGroup ??= "DEFAULT";
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
    public Task SaveJobRunAsync(JobRun jobRun, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(jobRun, nameof(jobRun));
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
    public async Task TriggerJobAsync(string jobName, string jobGroup = null, IDictionary<string, object> data = null, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNullOrEmpty(jobName, nameof(jobName));
        jobGroup ??= "DEFAULT";
        TypedLogger.LogTriggerJob(this.logger, Constants.LogKey, jobName, jobGroup);

        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var jobKey = new JobKey(jobName, jobGroup);
        var jobData = new JobDataMap(data ?? new Dictionary<string, object>());

        await scheduler.TriggerJob(jobKey, jobData, cancellationToken);
    }

    /// <summary>
    /// Triggers multiple jobs to run immediately with optional data for each job.
    /// </summary>
    /// <param name="jobNames">The collection of job names to trigger.</param>
    /// <param name="jobGroup">The common group all jobs belong to.</param>
    /// <param name="jobDatas">Optional dictionary mapping each job name to its specific data. Jobs with no entry will receive null data.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task TriggerJobsAsync(
        IEnumerable<string> jobNames,
        string jobGroup = null,
        IDictionary<string, object> data = null,
        IDictionary<string, IDictionary<string, object>> jobDatas = null,
        CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(jobNames, nameof(jobNames));
        if (!jobNames.Any())
        {
            throw new ArgumentException("At least one job name must be provided.", nameof(jobNames));
        }

        jobGroup ??= "DEFAULT";
        var correlationId = GuidGenerator.CreateSequential().ToString("N");

        using (this.logger.BeginScope(new Dictionary<string, object>
        {
            [Constants.CorrelationIdKey] = correlationId,
        }))
        {
            this.logger.LogDebug("{LogKey} jobservice: trigger jobs (count={JobCount}, group={JobGroup})", Constants.LogKey, jobNames.Count(), jobGroup);

            var triggerTasks = new List<Task>();
            foreach (var jobName in jobNames)
            {
                var jobData = jobDatas != null && jobDatas.TryGetValue(jobName, out var jobSpecificData)
                    ? jobSpecificData
                    : new Dictionary<string, object>();
                if (data != null)
                {
                    jobData = jobData.Concat(data).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                }

                jobData.AddOrUpdate(Constants.CorrelationIdKey, correlationId);

                triggerTasks.Add(this.TriggerJobAsync(jobName, jobGroup, jobData, cancellationToken));
            }

            await Task.WhenAll(triggerTasks);
        }
    }

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
    public async Task<JobInfo> TriggerJobAndWaitAsync(
        string jobName,
        string jobGroup = null,
        IDictionary<string, object> data = null,
        int checkInterval = 1000,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNullOrEmpty(jobName, nameof(jobName));
        EnsureArg.IsGte(checkInterval, 100, nameof(checkInterval)); // Minimum reasonable interval

        jobGroup ??= "DEFAULT";
        timeout ??= TimeSpan.FromMinutes(10); // Default timeout of 10 minutes

        this.logger.LogDebug("{LogKey} jobservice: trigger job and wait (name={JobName}, group={JobGroup}, checkInterval={CheckInterval}ms, timeout={Timeout})",
            Constants.LogKey, jobName, jobGroup, checkInterval, timeout);

        // First trigger the job
        await this.TriggerJobAsync(jobName, jobGroup, data, cancellationToken);

        // Add a small initial delay to allow job to start
        await Task.Delay(500, cancellationToken);

        var jobInfo = await this.GetJobAsync(jobName, jobGroup, cancellationToken) ?? throw new InvalidOperationException($"Job '{jobName}' in group '{jobGroup}' not found after triggering.");
        var startTime = DateTimeOffset.UtcNow;
        var timeoutTime = startTime.Add(timeout.Value);

        while (jobInfo.LastRun?.Status == "Started") // Poll until job completes or timeout occurs
        {
            if (DateTimeOffset.UtcNow >= timeoutTime)
            {
                throw new TimeoutException($"Job '{jobName}' in group '{jobGroup}' did not complete within the timeout period of {timeout.Value}.");
            }

            await Task.Delay(checkInterval, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            jobInfo = await this.GetJobAsync(jobName, jobGroup, cancellationToken); // Get the latest job info
            if (jobInfo == null)
            {
                throw new InvalidOperationException($"Job '{jobName}' in group '{jobGroup}' could not be found during status polling.");
            }
        }

        return jobInfo;
    }

    /// <summary>
    /// Triggers multiple jobs to run immediately with optional data for each job and waits for their completion.
    /// </summary>
    /// <param name="jobNames">The collection of job names to trigger.</param>
    /// <param name="jobGroup">The common group all jobs belong to.</param>
    /// <param name="jobDatas">Optional dictionary mapping each job name to its specific data. Jobs with no entry will receive null data.</param>
    /// <param name="sequentially">Whether to run jobs sequentially (true) or concurrently (false). Default is true (sequentially).</param>
    /// <param name="checkInterval">The time interval in milliseconds between status checks. Default is 1000ms.</param>
    /// <param name="timeout">The maximum time to wait for all jobs to complete. Default is 10 minutes.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A dictionary mapping job names to their final job information with execution results.</returns>
    /// <exception cref="ArgumentException">Thrown when jobNames is null or empty.</exception>
    /// <exception cref="TimeoutException">Thrown when one or more jobs do not complete within the specified timeout period.</exception>
    public async Task<IDictionary<string, JobInfo>> TriggerJobsAndWaitAsync(
        IEnumerable<string> jobNames,
        string jobGroup = null,
        IDictionary<string, object> data = null,
        IDictionary<string, IDictionary<string, object>> jobDatas = null,
        bool sequentially = true,
        bool continueOnFailed = false,
        int checkInterval = 1000,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(jobNames, nameof(jobNames));
        if (!jobNames.Any())
        {
            throw new ArgumentException("At least one job name must be provided.", nameof(jobNames));
        }

        EnsureArg.IsGte(checkInterval, 100, nameof(checkInterval)); // Minimum reasonable interval
        jobGroup ??= "DEFAULT";
        timeout ??= TimeSpan.FromMinutes(10); // Default timeout of 10 minutes
        var correlationId = GuidGenerator.CreateSequential().ToString("N");

        using (this.logger.BeginScope(new Dictionary<string, object>
        {
            [Constants.CorrelationIdKey] = correlationId,
        }))
        {
            this.logger.LogDebug("{LogKey} jobservice: trigger jobs and wait (count={JobCount}, group={JobGroup}, sequential={Sequential}, haltOnError={HaltOnError}, checkInterval={CheckInterval}ms, timeout={Timeout})", Constants.LogKey, jobNames.Count(), jobGroup, sequentially, continueOnFailed, checkInterval, timeout);

            var results = new Dictionary<string, JobInfo>();

            if (sequentially)
            {
                // Run jobs one after the other
                foreach (var jobName in jobNames)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var jobData = jobDatas != null && jobDatas.TryGetValue(jobName, out var jobSpecificData)
                        ? jobSpecificData
                        : new Dictionary<string, object>();
                    if (data != null)
                    {
                        jobData = jobData.Concat(data).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    }

                    jobData.AddOrUpdate(Constants.CorrelationIdKey, correlationId);

                    //this.logger.LogDebug("{LogKey} jobservice: trigger sequential job {JobName} in group {JobGroup}",
                    //    Constants.LogKey, jobName, jobGroup);

                    results[jobName] = await this.TriggerJobAndWaitAsync(jobName, jobGroup, jobData, checkInterval, timeout, cancellationToken);

                    if (!continueOnFailed && results[jobName].LastRun?.Status == "Failed")
                    {
                        return results; // Stop further execution if an error occurs and continueOnFailed is false
                    }
                }
            }
            else
            {
                // Start all jobs concurrently
                var triggerTasks = new List<Task>();
                foreach (var jobName in jobNames)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var jobData = jobDatas != null && jobDatas.TryGetValue(jobName, out var jobSpecificData)
                        ? jobSpecificData
                        : new Dictionary<string, object>();
                    if (data != null)
                    {
                        jobData = jobData.Concat(data).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    }

                    jobData.AddOrUpdate(Constants.CorrelationIdKey, correlationId);

                    //this.logger.LogDebug("{LogKey} jobservice: trigger concurrent job {JobName} in group {JobGroup}",
                    //    Constants.LogKey, jobName, jobGroup);

                    triggerTasks.Add(this.TriggerJobAsync(jobName, jobGroup, jobData, cancellationToken));
                }

                await Task.WhenAll(triggerTasks); // Wait for all jobs to start
                await Task.Delay(500, cancellationToken); // Initial delay to allow jobs to start

                // Get initial job info
                var jobInfos = new Dictionary<string, JobInfo>();
                foreach (var jobName in jobNames)
                {
                    var jobInfo = await this.GetJobAsync(jobName, jobGroup, cancellationToken);
                    jobInfos[jobName] = jobInfo ?? throw new InvalidOperationException($"Job '{jobName}' in group '{jobGroup}' not found after triggering.");
                }

                var startTime = DateTimeOffset.UtcNow;
                var timeoutTime = startTime.Add(timeout.Value);
                var runningJobs = new HashSet<string>(jobNames);

                while (runningJobs.Count > 0) // Poll until all jobs complete or timeout occurs
                {
                    if (DateTimeOffset.UtcNow >= timeoutTime)
                    {
                        var stillRunning = string.Join(", ", runningJobs);
                        throw new TimeoutException($"Jobs [{stillRunning}] in group '{jobGroup}' did not complete within the timeout period of {timeout.Value}.");
                    }

                    await Task.Delay(checkInterval, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    foreach (var jobName in runningJobs.ToList()) // Check the status of each still-running job
                    {
                        var jobInfo = await this.GetJobAsync(jobName, jobGroup, cancellationToken);
                        jobInfos[jobName] = jobInfo ?? throw new InvalidOperationException($"Job '{jobName}' in group '{jobGroup}' could not be found during status polling.");

                        if (jobInfo.LastRun?.Status != "Started") // If job is no longer "Started", remove from running list
                        {
                            runningJobs.Remove(jobName);
                            this.logger.LogDebug("{LogKey} jobservice: job completion detected (name={JobName}, group={JobGroup}, status={Status})",
                                Constants.LogKey, jobName, jobGroup, jobInfo.LastRun?.Status);
                        }
                        else if (!continueOnFailed && jobInfo.LastRun?.Status != "Failed")
                        {
                            return results; // Stop further execution if an error occurs and continueOnFailed is false
                        }
                    }
                }

                results = jobInfos;
            }

            var totalDuration = (DateTimeOffset.UtcNow - (results.Values.FirstOrDefault()?.LastRun?.StartTime ?? DateTimeOffset.UtcNow)).TotalMilliseconds;
            this.logger.LogDebug("{LogKey} jobservice: all jobs completed (count={JobCount}, group={JobGroup}, mode={Mode}, totalDuration={TotalDurationMs}ms)", Constants.LogKey, jobNames.Count(), jobGroup, sequentially ? "Sequential" : "Concurrent", (long)totalDuration);

            return results;
        }
    }

    /// <summary>
    /// Interrupts a scheduled job identified by its name and group. The operation can be canceled using a provided
    /// token.
    /// </summary>
    /// <param name="jobName">Specifies the name of the job to be interrupted.</param>
    /// <param name="jobGroup">Indicates the group to which the job belongs.</param>
    /// <param name="cancellationToken">Allows the operation to be canceled if needed.</param>
    public async Task InterruptJobAsync(string jobName, string jobGroup = null, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNullOrEmpty(jobName, nameof(jobName));
        jobGroup ??= "DEFAULT";
        TypedLogger.LogTriggerJob(this.logger, Constants.LogKey, jobName, jobGroup);

        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var jobKey = new JobKey(jobName, jobGroup);

        await scheduler.Interrupt(jobKey, cancellationToken);
    }

    /// <summary>
    /// Pauses the execution of a specific job using the scheduler.
    /// </summary>
    /// <param name="jobName">The name of the job to pause.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task PauseJobAsync(string jobName, string jobGroup = null, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNullOrEmpty(jobName, nameof(jobName));
        jobGroup ??= "DEFAULT";
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
    public async Task ResumeJobAsync(string jobName, string jobGroup = null, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNullOrEmpty(jobName, nameof(jobName));
        jobGroup ??= "DEFAULT";
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
    public Task PurgeJobRunsAsync(string jobName, string jobGroup, DateTimeOffset? olderThan = null, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNullOrEmpty(jobName, nameof(jobName));
        jobGroup ??= "DEFAULT";
        olderThan ??= DateTimeOffset.UtcNow;
        TypedLogger.LogPurgeJobRuns(this.logger, Constants.LogKey, jobName, jobGroup, olderThan.Value);

        return provider.PurgeJobRunsAsync(jobName, jobGroup, olderThan.Value, cancellationToken);
    }

    private async Task<string> GetJobStatusAsync(JobKey jobKey, IScheduler scheduler, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(jobKey, nameof(jobKey));
        var triggers = await scheduler.GetTriggersOfJob(jobKey, cancellationToken);
        if (triggers.Count == 0)
        {
            return "No Triggers";
        }

        var states = await Task.WhenAll(triggers.Select(t => scheduler.GetTriggerState(t.Key, cancellationToken)));

        return states.All(s => s == TriggerState.Paused) ? "Paused" : "Active";
    }

    private void LogFilterOptions(
        DateTimeOffset? startDate, DateTimeOffset? endDate, string status, int? priority,
        string instanceName, string resultContains, int? take)
    {
        this.LogDateRange(startDate, endDate);

        if (!string.IsNullOrEmpty(status))
        {
            this.logger.LogDebug("{LogKey} jobservice: filter status={Status}", Constants.LogKey, status);
        }

        if (priority.HasValue)
        {
            this.logger.LogDebug("{LogKey} jobservice: filter priority={Priority}", Constants.LogKey, priority.Value);
        }

        if (!string.IsNullOrEmpty(instanceName))
        {
            this.logger.LogDebug("{LogKey} jobservice: filter instanceName={InstanceName}", Constants.LogKey, instanceName);
        }

        if (!string.IsNullOrEmpty(resultContains))
        {
            this.logger.LogDebug("{LogKey} jobservice: filter resultContains={ResultContains}", Constants.LogKey, resultContains);
        }

        if (take.HasValue)
        {
            this.logger.LogDebug("{LogKey} jobservice: take={Take}", Constants.LogKey, take.Value);
        }
    }

    private void LogDateRange(DateTimeOffset? startDate, DateTimeOffset? endDate)
    {
        if (startDate.HasValue && endDate.HasValue)
        {
            this.logger.LogDebug("{LogKey} jobservice: date range start={StartDate}, end={EndDate}", Constants.LogKey, startDate.Value, endDate.Value);
        }
        else if (startDate.HasValue)
        {
            this.logger.LogDebug("{LogKey} jobservice: date range start={StartDate}", Constants.LogKey, startDate.Value);
        }
        else if (endDate.HasValue)
        {
            this.logger.LogDebug("{LogKey} jobservice: date range end={EndDate}", Constants.LogKey, endDate.Value);
        }
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Debug, "{LogKey} jobservice: get jobs")]
        public static partial void LogGetJobs(ILogger logger, string logKey);

        [LoggerMessage(1, LogLevel.Debug, "{LogKey} jobservice: get job (name={JobName}, group={JobGroup})")]
        public static partial void LogGetJob(ILogger logger, string logKey, string jobName, string jobGroup);

        [LoggerMessage(2, LogLevel.Debug, "{LogKey} jobservice: get job runs (name={JobName}, group={JobGroup})")]
        public static partial void LogGetJobRuns(ILogger logger, string logKey, string jobName, string jobGroup);

        [LoggerMessage(3, LogLevel.Debug, "{LogKey} jobservice: get job run stats (name={JobName}, group={JobGroup})")]
        public static partial void LogGetJobRunStats(ILogger logger, string logKey, string jobName, string jobGroup);

        [LoggerMessage(4, LogLevel.Debug, "{LogKey} jobservice: get triggers (name={JobName}, group={JobGroup})")]
        public static partial void LogGetTriggers(ILogger logger, string logKey, string jobName, string jobGroup);

        [LoggerMessage(5, LogLevel.Debug, "{LogKey} jobservice: save job run (name={JobName}, group={JobGroup}, id={EntryId})")]
        public static partial void LogSaveJobRun(ILogger logger, string logKey, string jobName, string jobGroup, string entryId);

        [LoggerMessage(6, LogLevel.Debug, "{LogKey} jobservice: trigger job (name={JobName}, group={JobGroup})")]
        public static partial void LogTriggerJob(ILogger logger, string logKey, string jobName, string jobGroup);

        [LoggerMessage(7, LogLevel.Debug, "{LogKey} jobservice: pause job (name={JobName}, group={JobGroup})")]
        public static partial void LogPauseJob(ILogger logger, string logKey, string jobName, string jobGroup);

        [LoggerMessage(8, LogLevel.Debug, "{LogKey} jobservice: resume job (name={JobName}, group={JobGroup})")]
        public static partial void LogResumeJob(ILogger logger, string logKey, string jobName, string jobGroup);

        [LoggerMessage(9, LogLevel.Debug, "{LogKey} jobservice: purge job runs (name={JobName}, group={JobGroup}, olderThan={OlderThan})")]
        public static partial void LogPurgeJobRuns(ILogger logger, string logKey, string jobName, string jobGroup, DateTimeOffset olderThan);
    }
}