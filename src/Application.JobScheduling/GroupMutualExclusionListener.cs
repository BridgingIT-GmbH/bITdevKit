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
/// <param name="loggerFactory"></param>
/// <param name="options"></param>
public partial class ConcurrentGroupExecutionListener(
    ILoggerFactory loggerFactory,
    JobGroupOptions options) : IJobListener, IDisposable
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> groupSemaphores = [];
    private readonly ILogger<ConcurrentGroupExecutionListener> logger = loggerFactory?.CreateLogger<ConcurrentGroupExecutionListener>() ?? NullLogger<ConcurrentGroupExecutionListener>.Instance;
    private readonly JobGroupOptions options = options ?? throw new ArgumentNullException(nameof(options));

    public string Name => nameof(ConcurrentGroupExecutionListener);

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

    public void Dispose()
    {
        foreach (var semaphore in groupSemaphores.Values)
        {
            semaphore?.Dispose();
        }

        groupSemaphores.Clear();
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Debug, "{LogKey} job not handled by group mutual exclusion (name={JobName}, group={JobGroup}, entryId={EntryId})")]
        public static partial void LogJobNotHandled(ILogger logger, string logKey, string jobName, string jobGroup, string entryId);

        [LoggerMessage(1, LogLevel.Information, "{LogKey} job waiting for exclusive group access (name={JobName}, group={JobGroup}, entryId={EntryId})")]
        public static partial void LogJobWaitingForExclusiveAccess(ILogger logger, string logKey, string jobName, string jobGroup, string entryId);

        [LoggerMessage(2, LogLevel.Information, "{LogKey} job acquired exclusive group access (name={JobName}, group={JobGroup}, entryId={EntryId})")]
        public static partial void LogJobAcquiredExclusiveAccess(ILogger logger, string logKey, string jobName, string jobGroup, string entryId);

        [LoggerMessage(3, LogLevel.Information, "{LogKey} job execution vetoed, releasing exclusive group access (name={JobName}, group={JobGroup}, entryId={EntryId})")]
        public static partial void LogJobExecutionVetoed(ILogger logger, string logKey, string jobName, string jobGroup, string entryId);

        [LoggerMessage(4, LogLevel.Information, "{LogKey} job releasing exclusive group access (name={JobName}, group={JobGroup}, entryId={EntryId})")]
        public static partial void LogJobReleasingExclusiveAccess(ILogger logger, string logKey, string jobName, string jobGroup, string entryId);
    }
}