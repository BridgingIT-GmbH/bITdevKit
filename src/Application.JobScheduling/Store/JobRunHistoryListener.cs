// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Quartz;

public partial class JobRunHistoryListener(ILoggerFactory loggerFactory, IJobStore jobStore) : IJobListener
{
    private readonly ILogger<JobRunHistoryListener> logger = loggerFactory?.CreateLogger<JobRunHistoryListener>() ?? NullLogger<JobRunHistoryListener>.Instance;

    public string Name => nameof(JobRunHistoryListener);

    /// <summary>
    /// Called before a job is executed, logs the start of the job run via the job store.
    /// </summary>
    /// <param name="context">The execution context of the job.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var entryId = context.FireInstanceId;
        var jobKey = context.JobDetail.Key;
        var triggerKey = context.Trigger.Key;
        var scheduledTime = context.ScheduledFireTimeUtc ?? DateTimeOffset.UtcNow;
        var startTime = DateTimeOffset.UtcNow;

        TypedLogger.LogJobStarting(this.logger, Constants.LogKey, jobKey.Name, jobKey.Group, entryId);

        var jobRun = new JobRun
        {
            Id = entryId,
            JobName = jobKey.Name,
            JobGroup = jobKey.Group,
            TriggerName = triggerKey.Name,
            TriggerGroup = triggerKey.Group,
            Description = context.JobDetail.Description,
            Data = context.JobDetail.JobDataMap.ToDictionary(),
            StartTime = startTime,
            ScheduledTime = scheduledTime,
            Status = "Started",
            InstanceName = context.Scheduler.SchedulerInstanceId,
            Priority = context.Trigger.Priority,
            Category = context.JobDetail.JobDataMap.ContainsKey("Category")
                ? context.JobDetail.JobDataMap.GetString("Category")
                : null
        };

        try
        {
            await jobStore.SaveJobRunAsync(jobRun, cancellationToken);
            this.logger.LogInformation("{LogKey} listener: job started (name={JobName}, group={JobGroup}, entryId={EntryId})", Constants.LogKey, jobKey.Name, jobKey.Group, entryId);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} listener: failed to log job start (name={JobName}, group={JobGroup}, entryId={EntryId})", Constants.LogKey, jobKey.Name, jobKey.Group, entryId);
        }
    }

    /// <summary>
    /// Called when a job execution is vetoed, no action taken in this implementation.
    /// </summary>
    /// <param name="context">The execution context of the job.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        // No logging added here as per original implementation (no action taken)
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called after a job has executed, updates the job run record via the job store.
    /// </summary>
    /// <param name="context">The execution context of the job.</param>
    /// <param name="jobException">Exception thrown during execution, if any.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task JobWasExecuted(IJobExecutionContext context, JobExecutionException jobException, CancellationToken cancellationToken = default)
    {
        var entryId = context.FireInstanceId;
        var jobKey = context.JobDetail.Key;
        var endTime = DateTimeOffset.UtcNow;
        var runTimeMs = (long)(endTime - context.FireTimeUtc).TotalMilliseconds;
        var status = jobException == null ? "Success" : "Failed";

        TypedLogger.LogJobCompleted(this.logger, Constants.LogKey, jobKey.Name, jobKey.Group, entryId, status);

        var jobRun = new JobRun
        {
            Id = entryId,
            JobName = jobKey.Name,
            JobGroup = jobKey.Group,
            TriggerName = context.Trigger.Key.Name,
            TriggerGroup = context.Trigger.Key.Group,
            Description = context.JobDetail.Description,
            Data = context.JobDetail.JobDataMap.ToDictionary(),
            StartTime = context.FireTimeUtc,
            EndTime = endTime,
            ScheduledTime = context.ScheduledFireTimeUtc ?? DateTimeOffset.UtcNow,
            RunTimeMs = runTimeMs,
            Status = status,
            ErrorMessage = jobException?.Message,
            InstanceName = context.Scheduler.SchedulerInstanceId,
            Priority = context.Trigger.Priority,
            Result = context.Result?.ToString(),
            RetryCount = context.RefireCount,
            Category = context.JobDetail.JobDataMap.ContainsKey("Category")
                ? context.JobDetail.JobDataMap.GetString("Category")
                : null
        };

        try
        {
            await jobStore.SaveJobRunAsync(jobRun, cancellationToken);
            this.logger.LogInformation("{LogKey} listener: job completed (name={JobName}, group={JobGroup}, entryId={EntryId}, status={Status})", Constants.LogKey, jobKey.Name, jobKey.Group, entryId, status);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} listener: failed to log job completion (name={JobName}, group={JobGroup}, entryId={EntryId}, status={Status})", Constants.LogKey, jobKey.Name, jobKey.Group, entryId, status);
        }
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "{LogKey} listener: job starting (name={JobName}, group={JobGroup}, entryId={EntryId})")]
        public static partial void LogJobStarting(ILogger logger, string logKey, string jobName, string jobGroup, string entryId);

        [LoggerMessage(1, LogLevel.Information, "{LogKey} listener: job completed (name={JobName}, group={JobGroup}, entryId={EntryId}, status={Status})")]
        public static partial void LogJobCompleted(ILogger logger, string logKey, string jobName, string jobGroup, string entryId, string status);
    }
}