// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

using Microsoft.Extensions.Logging;

public class NullJobStoreProvider(ILoggerFactory loggerFactory) : IJobStoreProvider
{
    private readonly ILogger<NullJobStoreProvider> logger = loggerFactory?.CreateLogger<NullJobStoreProvider>() ?? NullLogger<NullJobStoreProvider>.Instance;

    /// <summary>
    /// Attempts to retrieve job run history, but returns an empty collection as no persistence is available.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="startDate">Ignored in this implementation.</param>
    /// <param name="endDate">Ignored in this implementation.</param>
    /// <param name="status">Ignored in this implementation.</param>
    /// <param name="priority">Ignored in this implementation.</param>
    /// <param name="instanceName">Ignored in this implementation.</param>
    /// <param name="resultContains">Ignored in this implementation.</param>
    /// <param name="take">Ignored in this implementation.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>An empty collection of job runs.</returns>
    public Task<IEnumerable<JobRun>> GetJobRunsAsync(
        string jobName, string jobGroup,
        DateTimeOffset? startDate, DateTimeOffset? endDate,
        string status, int? priority, string instanceName,
        string resultContains, int? take,
        CancellationToken cancellationToken)
    {
        this.logger.LogWarning("NullJobStoreProvider: No run history available.");
        return Task.FromResult(Enumerable.Empty<JobRun>());
    }

    /// <summary>
    /// Attempts to retrieve job run statistics, but returns an empty stats object as no persistence is available.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="startDate">Ignored in this implementation.</param>
    /// <param name="endDate">Ignored in this implementation.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>An empty job run statistics object.</returns>
    public Task<JobRunStats> GetJobRunStatsAsync(
        string jobName, string jobGroup,
        DateTimeOffset? startDate, DateTimeOffset? endDate,
        CancellationToken cancellationToken)
    {
        this.logger.LogWarning("NullJobStoreProvider: No run statistics available.");
        return Task.FromResult(new JobRunStats());
    }

    /// <summary>
    /// Attempts to save a job run record, but does nothing as no persistence is available.
    /// </summary>
    /// <param name="jobRun">The job run record to save.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public Task SaveJobRunAsync(JobRun jobRun, CancellationToken cancellationToken)
    {
        this.logger.LogWarning("NullJobStoreProvider: No persistence available for saving job run {JobName} ({EntryId}).", jobRun.JobName, jobRun.Id);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Attempts to purge job run history, but does nothing as no persistence is available.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="jobGroup">The group the job belongs to.</param>
    /// <param name="olderThan">Ignored in this implementation.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public Task PurgeJobRunsAsync(string jobName, string jobGroup, DateTimeOffset olderThan, CancellationToken cancellationToken)
    {
        this.logger.LogWarning("NullJobStoreProvider: No run history to purge.");
        return Task.CompletedTask;
    }
}