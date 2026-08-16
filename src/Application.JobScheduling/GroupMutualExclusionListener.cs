// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Quartz;

/// <summary>
/// Provides a listener that ensures mutual exclusion for job execution within specified groups.
/// </summary>
/// <remarks>This listener uses semaphores to enforce that only one job within a specified group can execute at a
/// time. It is configured with a set of groups for which mutual exclusion is required. Jobs in other groups are not
/// affected.</remarks>
public partial class ConcurrentGroupExecutionListener(
    ILoggerFactory loggerFactory,
    JobGroupOptions options) : IJobListener, IDisposable
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> groupSemaphores = [];
    private readonly ILogger<ConcurrentGroupExecutionListener> logger = loggerFactory?.CreateLogger<ConcurrentGroupExecutionListener>() ?? NullLogger<ConcurrentGroupExecutionListener>.Instance;
    private readonly JobGroupOptions options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name => nameof(ConcurrentGroupExecutionListener);

    /// <summary>
    /// Executes the job to be executed operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task JobToBeExecuted(IJobExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var groupName = context.JobDetail.Key.Group;
        var jobName = context.JobDetail.Key.Name;
        var entryId = context.FireInstanceId;

        if (!this.ShouldHandleGroup(groupName)) // Only handle configured groups
        {
            TypedLogger.LogJobNotHandled(this.logger, Constants.LogKey, jobName, groupName, entryId);

            return;
        }

        var semaphore = groupSemaphores.GetOrAdd(groupName, _ => new SemaphoreSlim(1, 1));

        TypedLogger.LogJobWaitingForExclusiveAccess(this.logger, Constants.LogKey, jobName, groupName, entryId);

        await semaphore.WaitAsync(cancellationToken);

        TypedLogger.LogJobAcquiredExclusiveAccess(this.logger, Constants.LogKey, jobName, groupName, entryId);
    }

    /// <summary>
    /// Executes the job execution vetoed operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task JobExecutionVetoed(IJobExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var groupName = context.JobDetail.Key.Group;
        var jobName = context.JobDetail.Key.Name;
        var entryId = context.FireInstanceId;

        if (this.ShouldHandleGroup(groupName))
        {
            TypedLogger.LogJobExecutionVetoed(this.logger, Constants.LogKey, jobName, groupName, entryId);

            this.ReleaseSemaphore(groupName);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes the job was executed operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <param name="jobException">The job exception used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task JobWasExecuted(IJobExecutionContext context,
        JobExecutionException jobException,
        CancellationToken cancellationToken = default)
    {
        var groupName = context.JobDetail.Key.Group;
        var jobName = context.JobDetail.Key.Name;
        var entryId = context.FireInstanceId;

        if (this.ShouldHandleGroup(groupName))
        {
            TypedLogger.LogJobReleasingExclusiveAccess(this.logger, Constants.LogKey, jobName, groupName, entryId);

            this.ReleaseSemaphore(groupName);
        }

        return Task.CompletedTask;
    }

    private bool ShouldHandleGroup(string groupName)
    {
        if (groupName == "DEFAULT" && this.options.DisallowConcurrentExecutionDefaultGroup)
        {
            return true;
        }

        return this.options.DisallowConcurrentExecutionGroups.Contains(groupName);
    }

    private void ReleaseSemaphore(string groupName)
    {
        if (groupSemaphores.TryGetValue(groupName, out var semaphore))
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    public void Dispose()
    {
        foreach (var semaphore in groupSemaphores.Values)
        {
            semaphore?.Dispose();
        }

        groupSemaphores.Clear();
    }

    /// <summary>
    /// Represents typed logger.
    /// </summary>
    public static partial class TypedLogger
    {
        /// <summary>
        /// Writes a log entry for the job not handled operation.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="jobName">The job name used by the operation.</param>
        /// <param name="jobGroup">The job group used by the operation.</param>
        /// <param name="entryId">The entry id used by the operation.</param>
        [LoggerMessage(0, LogLevel.Debug, "[{LogKey}] job not handled by group mutual exclusion (name={JobName}, group={JobGroup}, entryId={EntryId})")]
        public static partial void LogJobNotHandled(ILogger logger, string logKey, string jobName, string jobGroup, string entryId);

        /// <summary>
        /// Writes a log entry before waiting for exclusive group access.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="jobName">The job name.</param>
        /// <param name="jobGroup">The job group.</param>
        /// <param name="entryId">The scheduler fire-instance identifier.</param>
        [LoggerMessage(1, LogLevel.Information, "[{LogKey}] job waiting for exclusive group access (name={JobName}, group={JobGroup}, entryId={EntryId})")]
        public static partial void LogJobWaitingForExclusiveAccess(ILogger logger, string logKey, string jobName, string jobGroup, string entryId);

        /// <summary>
        /// Writes a log entry after exclusive group access is acquired.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="jobName">The job name.</param>
        /// <param name="jobGroup">The job group.</param>
        /// <param name="entryId">The scheduler fire-instance identifier.</param>
        [LoggerMessage(2, LogLevel.Information, "[{LogKey}] job acquired exclusive group access (name={JobName}, group={JobGroup}, entryId={EntryId})")]
        public static partial void LogJobAcquiredExclusiveAccess(ILogger logger, string logKey, string jobName, string jobGroup, string entryId);

        /// <summary>
        /// Writes a log entry when a vetoed execution releases exclusive group access.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="jobName">The job name.</param>
        /// <param name="jobGroup">The job group.</param>
        /// <param name="entryId">The scheduler fire-instance identifier.</param>
        [LoggerMessage(3, LogLevel.Information, "[{LogKey}] job execution vetoed, releasing exclusive group access (name={JobName}, group={JobGroup}, entryId={EntryId})")]
        public static partial void LogJobExecutionVetoed(ILogger logger, string logKey, string jobName, string jobGroup, string entryId);

        /// <summary>
        /// Writes a log entry before releasing exclusive group access.
        /// </summary>
        /// <param name="logger">The logger that receives diagnostic events.</param>
        /// <param name="logKey">The structured logging key.</param>
        /// <param name="jobName">The job name.</param>
        /// <param name="jobGroup">The job group.</param>
        /// <param name="entryId">The scheduler fire-instance identifier.</param>
        [LoggerMessage(4, LogLevel.Information, "[{LogKey}] job releasing exclusive group access (name={JobName}, group={JobGroup}, entryId={EntryId})")]
        public static partial void LogJobReleasingExclusiveAccess(ILogger logger, string logKey, string jobName, string jobGroup, string entryId);
    }
}
