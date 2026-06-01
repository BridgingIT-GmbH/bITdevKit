// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Jobs;

using System.Collections.Concurrent;
using BridgingIT.DevKit.Common;

/// <summary>
/// Provides the default in-memory Jobs persistence implementation for tests and local development.
/// </summary>
/// <param name="timeProvider">The clock used for deterministic lease timing.</param>
/// <param name="serializer">The serializer used for occurrence data and properties boundaries.</param>
/// <example>
/// <code>
/// services.AddJobScheduler()
///     .WithInMemoryStore();
/// </code>
/// </example>
public class InMemoryJobStoreProvider(TimeProvider timeProvider, ISerializer serializer) :
    IJobStoreProvider,
    IJobRuntimeStateStore,
    IJobTriggerRuntimeStateStore,
    IJobOccurrenceStore,
    IJobExecutionStore,
    IJobOccurrenceDependencyStore,
    IJobBatchStore,
    IJobLeaseStore,
    IJobExecutionHistoryStore,
    IJobBatchHistoryStore,
    IJobAcceptedEventStore,
    IJobPreviousExecutionStore,
    IJobSchedulerQueryStore
{
    private readonly object syncRoot = new();
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly ISerializer serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    private readonly ConcurrentDictionary<string, JobRuntimeState> jobRuntimeStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, JobTriggerRuntimeState> triggerRuntimeStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, StoredOccurrence> occurrences = [];
    private readonly ConcurrentDictionary<string, Guid> occurrenceKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, JobExecution> executions = [];
    private readonly ConcurrentDictionary<Guid, List<JobOccurrenceDependency>> dependenciesByDependent = [];
    private readonly ConcurrentDictionary<Guid, List<JobOccurrenceDependency>> dependenciesByPrerequisite = [];
    private readonly ConcurrentDictionary<Guid, JobBatch> batches = [];
    private readonly ConcurrentDictionary<Guid, List<JobBatchOccurrence>> batchMembership = [];
    private readonly ConcurrentDictionary<Guid, JobLeaseRecord> leases = [];
    private readonly ConcurrentDictionary<Guid, List<JobExecutionHistoryEntry>> historyByOccurrence = [];
    private readonly ConcurrentDictionary<Guid, List<JobBatchHistoryEntry>> historyByBatch = [];
    private readonly ConcurrentDictionary<Guid, JobAcceptedEvent> acceptedEvents = [];
    private readonly ConcurrentDictionary<string, Guid> acceptedEventKeys = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryJobStoreProvider"/> class using the system clock.
    /// </summary>
    public InMemoryJobStoreProvider(ISerializer serializer)
        : this(TimeProvider.System, serializer)
    {
    }

    /// <inheritdoc />
    public IJobRuntimeStateStore RuntimeStates => this;

    /// <inheritdoc />
    public IJobTriggerRuntimeStateStore TriggerRuntimeStates => this;

    /// <inheritdoc />
    public IJobOccurrenceStore Occurrences => this;

    /// <inheritdoc />
    public IJobExecutionStore Executions => this;

    /// <inheritdoc />
    public IJobOccurrenceDependencyStore Dependencies => this;

    /// <inheritdoc />
    public IJobBatchStore Batches => this;

    /// <inheritdoc />
    public IJobLeaseStore Leases => this;

    /// <inheritdoc />
    public IJobExecutionHistoryStore ExecutionHistory => this;

    /// <inheritdoc />
    public IJobBatchHistoryStore BatchHistory => this;

    /// <inheritdoc />
    public IJobAcceptedEventStore AcceptedEvents => this;

    /// <inheritdoc />
    public IJobPreviousExecutionStore PreviousExecutions => this;

    /// <inheritdoc />
    public IJobSchedulerQueryStore Queries => this;

    /// <inheritdoc />
    Task<JobRuntimeState> IJobRuntimeStateStore.GetAsync(string jobName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.jobRuntimeStates.TryGetValue(jobName ?? string.Empty, out var state);
        return Task.FromResult(state);
    }

    /// <inheritdoc />
    Task<IReadOnlyList<JobRuntimeState>> IJobRuntimeStateStore.ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<JobRuntimeState>>(this.jobRuntimeStates.Values.OrderBy(x => x.JobName).ToArray());
    }

    /// <inheritdoc />
    public Task UpsertAsync(JobRuntimeState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(state);
        this.jobRuntimeStates[state.JobName] = state;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string jobName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.jobRuntimeStates.TryRemove(jobName ?? string.Empty, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<JobTriggerRuntimeState> GetAsync(string jobName, string triggerName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.triggerRuntimeStates.TryGetValue(BuildTriggerKey(jobName, triggerName), out var state);
        return Task.FromResult(state);
    }

    /// <inheritdoc />
    Task<IReadOnlyList<(string JobName, string TriggerName, JobTriggerRuntimeState State)>> IJobTriggerRuntimeStateStore.ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var items = this.triggerRuntimeStates
            .Select(x =>
            {
                var parts = x.Key.Split('|', 2);
                return (parts[0], parts[1], x.Value);
            })
            .OrderBy(x => x.Item1)
            .ThenBy(x => x.Item2)
            .ToArray();
        return Task.FromResult<IReadOnlyList<(string JobName, string TriggerName, JobTriggerRuntimeState State)>>(items);
    }

    /// <inheritdoc />
    public Task UpsertAsync(string jobName, string triggerName, JobTriggerRuntimeState state, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(state);
        this.triggerRuntimeStates[BuildTriggerKey(jobName, triggerName)] = state;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string jobName, string triggerName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.triggerRuntimeStates.TryRemove(BuildTriggerKey(jobName, triggerName), out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> TryCreateAsync(JobOccurrence occurrence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(occurrence);

        lock (this.syncRoot)
        {
            if (this.occurrenceKeys.ContainsKey(occurrence.OccurrenceKey))
            {
                return Task.FromResult(false);
            }

            this.occurrenceKeys[occurrence.OccurrenceKey] = occurrence.OccurrenceId;
            this.occurrences[occurrence.OccurrenceId] = StoredOccurrence.From(occurrence, this.serializer);
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    Task<JobOccurrence> IJobOccurrenceStore.GetAsync(Guid occurrenceId, CancellationToken cancellationToken)
        => this.GetOccurrenceAsync(occurrenceId, cancellationToken);

    /// <inheritdoc />
    public Task<JobOccurrence> GetByKeyAsync(string occurrenceKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return this.occurrenceKeys.TryGetValue(occurrenceKey ?? string.Empty, out var occurrenceId)
            ? this.GetOccurrenceAsync(occurrenceId, cancellationToken)
            : Task.FromResult<JobOccurrence>(null);
    }

    /// <inheritdoc />
    Task<IReadOnlyList<JobOccurrence>> IJobOccurrenceStore.ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var items = this.occurrences.Values
            .Select(x => x.ToOccurrence(this.serializer))
            .OrderBy(x => x.DueUtc)
            .ThenBy(x => x.OccurrenceId)
            .ToArray();
        return Task.FromResult<IReadOnlyList<JobOccurrence>>(items);
    }

    /// <inheritdoc />
    public Task UpdateAsync(JobOccurrence occurrence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(occurrence);

        lock (this.syncRoot)
        {
            this.occurrences[occurrence.OccurrenceId] = StoredOccurrence.From(occurrence, this.serializer);
            this.occurrenceKeys[occurrence.OccurrenceKey] = occurrence.OccurrenceId;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    Task IJobOccurrenceStore.RemoveAsync(Guid occurrenceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.syncRoot)
        {
            if (this.occurrences.TryRemove(occurrenceId, out var stored))
            {
                this.occurrenceKeys.TryRemove(stored.OccurrenceKey, out _);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CreateAsync(JobExecution execution, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(execution);
        this.executions[execution.ExecutionId] = execution;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    Task<JobExecution> IJobExecutionStore.GetAsync(Guid executionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.executions.TryGetValue(executionId, out var execution);
        return Task.FromResult(execution);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobExecution>> ListByOccurrenceAsync(Guid occurrenceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var items = this.executions.Values
            .Where(x => x.OccurrenceId == occurrenceId)
            .OrderBy(x => x.AttemptNumber)
            .ThenBy(x => x.StartedUtc)
            .ToArray();
        return Task.FromResult<IReadOnlyList<JobExecution>>(items);
    }

    /// <inheritdoc />
    public Task UpdateAsync(JobExecution execution, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(execution);
        this.executions[execution.ExecutionId] = execution;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    Task<int> IJobExecutionStore.RemoveByOccurrenceAsync(Guid occurrenceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var removed = 0;

        foreach (var executionId in this.executions.Values.Where(x => x.OccurrenceId == occurrenceId).Select(x => x.ExecutionId).ToArray())
        {
            if (this.executions.TryRemove(executionId, out _))
            {
                removed++;
            }
        }

        return Task.FromResult(removed);
    }

    /// <inheritdoc />
    public Task AddAsync(JobOccurrenceDependency dependency, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(dependency);

        lock (this.syncRoot)
        {
            AddDependency(this.dependenciesByDependent, dependency.DependentOccurrenceId, dependency);
            AddDependency(this.dependenciesByPrerequisite, dependency.PrerequisiteOccurrenceId, dependency);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(JobOccurrenceDependency dependency, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(dependency);

        lock (this.syncRoot)
        {
            ReplaceDependency(this.dependenciesByDependent, dependency.DependentOccurrenceId, dependency);
            ReplaceDependency(this.dependenciesByPrerequisite, dependency.PrerequisiteOccurrenceId, dependency);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobOccurrenceDependency>> ListByDependentAsync(Guid dependentOccurrenceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<JobOccurrenceDependency>>(this.dependenciesByDependent.TryGetValue(dependentOccurrenceId, out var items)
            ? items.OrderBy(x => x.CreatedDate).ToArray()
            : []);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobOccurrenceDependency>> ListByPrerequisiteAsync(Guid prerequisiteOccurrenceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<JobOccurrenceDependency>>(this.dependenciesByPrerequisite.TryGetValue(prerequisiteOccurrenceId, out var items)
            ? items.OrderBy(x => x.CreatedDate).ToArray()
            : []);
    }

    /// <inheritdoc />
    Task<int> IJobOccurrenceDependencyStore.RemoveByOccurrenceAsync(Guid occurrenceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.syncRoot)
        {
            var removed = 0;

            if (this.dependenciesByDependent.TryRemove(occurrenceId, out var dependentItems))
            {
                removed += dependentItems.Count;
                foreach (var dependency in dependentItems)
                {
                    if (this.dependenciesByPrerequisite.TryGetValue(dependency.PrerequisiteOccurrenceId, out var items))
                    {
                        items.RemoveAll(x => x.DependencyId == dependency.DependencyId);
                        if (items.Count == 0)
                        {
                            this.dependenciesByPrerequisite.TryRemove(dependency.PrerequisiteOccurrenceId, out _);
                        }
                    }
                }
            }

            if (this.dependenciesByPrerequisite.TryRemove(occurrenceId, out var prerequisiteItems))
            {
                foreach (var dependency in prerequisiteItems)
                {
                    if (this.dependenciesByDependent.TryGetValue(dependency.DependentOccurrenceId, out var items))
                    {
                        if (items.RemoveAll(x => x.DependencyId == dependency.DependencyId) > 0)
                        {
                            removed++;
                        }

                        if (items.Count == 0)
                        {
                            this.dependenciesByDependent.TryRemove(dependency.DependentOccurrenceId, out _);
                        }
                    }
                }
            }

            return Task.FromResult(removed);
        }
    }

    /// <inheritdoc />
    public Task<bool> TryCreateAsync(JobBatch batch, IReadOnlyList<JobBatchOccurrence> occurrences, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(batch);

        lock (this.syncRoot)
        {
            if (!this.batches.TryAdd(batch.BatchId, batch))
            {
                return Task.FromResult(false);
            }

            this.batchMembership[batch.BatchId] = occurrences?.ToList() ?? [];
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task<bool> TryCreateWithChildrenAsync(
        JobBatch batch,
        IReadOnlyList<JobOccurrence> childOccurrences,
        IReadOnlyList<JobBatchOccurrence> memberships,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(batch);

        lock (this.syncRoot)
        {
            if (this.batches.ContainsKey(batch.BatchId))
            {
                return Task.FromResult(false);
            }

            if (!ValidateChildOccurrences(childOccurrences, this.occurrenceKeys, this.occurrences))
            {
                return Task.FromResult(false);
            }

            this.batches[batch.BatchId] = batch;
            foreach (var occurrence in childOccurrences ?? [])
            {
                if (this.occurrenceKeys.ContainsKey(occurrence.OccurrenceKey))
                {
                    continue;
                }

                this.occurrenceKeys[occurrence.OccurrenceKey] = occurrence.OccurrenceId;
                this.occurrences[occurrence.OccurrenceId] = StoredOccurrence.From(occurrence, this.serializer);
            }

            this.batchMembership[batch.BatchId] = DeduplicateMemberships(memberships);
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    Task<JobBatch> IJobBatchStore.GetAsync(Guid batchId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.batches.TryGetValue(batchId, out var batch);
        return Task.FromResult(batch);
    }

    /// <inheritdoc />
    Task<IReadOnlyList<JobBatch>> IJobBatchStore.ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<JobBatch>>(this.batches.Values.OrderBy(x => x.CreatedDate).ToArray());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobBatchOccurrence>> ListOccurrencesAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<JobBatchOccurrence>>(this.batchMembership.TryGetValue(batchId, out var items)
            ? items.OrderBy(x => x.Sequence ?? int.MaxValue).ThenBy(x => x.OccurrenceId).ToArray()
            : []);
    }

    /// <inheritdoc />
    public Task AttachAsync(Guid batchId, IReadOnlyList<JobBatchOccurrence> occurrences, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.syncRoot)
        {
            var list = this.batchMembership.GetOrAdd(batchId, _ => []);
            foreach (var occurrence in occurrences ?? [])
            {
                if (list.Any(x => x.OccurrenceId == occurrence.OccurrenceId))
                {
                    continue;
                }

                list.Add(occurrence);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> TryAttachChildrenAsync(
        Guid batchId,
        IReadOnlyList<JobOccurrence> childOccurrences,
        IReadOnlyList<JobBatchOccurrence> memberships,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.syncRoot)
        {
            if (!this.batches.ContainsKey(batchId))
            {
                return Task.FromResult(false);
            }

            if (!ValidateChildOccurrences(childOccurrences, this.occurrenceKeys, this.occurrences))
            {
                return Task.FromResult(false);
            }

            foreach (var occurrence in childOccurrences ?? [])
            {
                if (this.occurrenceKeys.ContainsKey(occurrence.OccurrenceKey))
                {
                    continue;
                }

                this.occurrenceKeys[occurrence.OccurrenceKey] = occurrence.OccurrenceId;
                this.occurrences[occurrence.OccurrenceId] = StoredOccurrence.From(occurrence, this.serializer);
            }

            var list = this.batchMembership.GetOrAdd(batchId, _ => []);
            foreach (var membership in memberships ?? [])
            {
                if (list.Any(x => x.OccurrenceId == membership.OccurrenceId))
                {
                    continue;
                }

                list.Add(membership);
            }

            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task ReplaceOccurrencesAsync(Guid batchId, IReadOnlyList<JobBatchOccurrence> occurrences, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.syncRoot)
        {
            this.batchMembership[batchId] = DeduplicateMemberships(occurrences);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(JobBatch batch, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(batch);
        this.batches[batch.BatchId] = batch;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<int> RemoveOccurrencesAsync(IReadOnlyCollection<Guid> occurrenceIds, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (occurrenceIds is null || occurrenceIds.Count == 0)
        {
            return Task.FromResult(0);
        }

        lock (this.syncRoot)
        {
            var selected = occurrenceIds.ToHashSet();
            var removed = 0;

            foreach (var key in this.batchMembership.Keys.ToArray())
            {
                if (!this.batchMembership.TryGetValue(key, out var items))
                {
                    continue;
                }

                removed += items.RemoveAll(x => selected.Contains(x.OccurrenceId));
            }

            return Task.FromResult(removed);
        }
    }

    /// <inheritdoc />
    Task<JobLeaseRecord> IJobLeaseStore.GetAsync(Guid occurrenceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.leases.TryGetValue(occurrenceId, out var lease);
        return Task.FromResult(lease);
    }

    /// <inheritdoc />
    Task<IReadOnlyList<JobLeaseRecord>> IJobLeaseStore.ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<JobLeaseRecord>>(this.leases.Values.OrderBy(x => x.OccurrenceId).ToArray());
    }

    /// <inheritdoc />
    public Task<JobLeaseRecord> TryAcquireAsync(
        Guid occurrenceId,
        string schedulerInstanceId,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        lock (this.syncRoot)
        {
            var nowUtc = this.timeProvider.GetUtcNow();
            if (this.leases.TryGetValue(occurrenceId, out var existing) && existing.ExpiresUtc > nowUtc)
            {
                return Task.FromResult<JobLeaseRecord>(null);
            }

            var lease = new JobLeaseRecord
            {
                OccurrenceId = occurrenceId,
                SchedulerInstanceId = schedulerInstanceId,
                OwnershipToken = Guid.NewGuid().ToString("N"),
                AcquiredUtc = nowUtc,
                ExpiresUtc = nowUtc.Add(duration),
                RenewalCount = 0,
                CreatedDate = nowUtc,
                UpdatedDate = nowUtc,
            };

            this.leases[occurrenceId] = lease;
            return Task.FromResult(lease);
        }
    }

    /// <inheritdoc />
    public Task<JobLeaseRecord> RenewAsync(
        Guid occurrenceId,
        string schedulerInstanceId,
        string ownershipToken,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        lock (this.syncRoot)
        {
            var nowUtc = this.timeProvider.GetUtcNow();
            if (!this.leases.TryGetValue(occurrenceId, out var existing)
                || existing.ExpiresUtc <= nowUtc
                || !string.Equals(existing.SchedulerInstanceId, schedulerInstanceId, StringComparison.Ordinal)
                || !string.Equals(existing.OwnershipToken, ownershipToken, StringComparison.Ordinal))
            {
                return Task.FromResult<JobLeaseRecord>(null);
            }

            var renewed = existing with
            {
                RenewedUtc = nowUtc,
                ExpiresUtc = nowUtc.Add(duration),
                RenewalCount = existing.RenewalCount + 1,
                UpdatedDate = nowUtc,
            };

            this.leases[occurrenceId] = renewed;
            return Task.FromResult(renewed);
        }
    }

    /// <inheritdoc />
    public Task<bool> VerifyOwnershipAsync(
        Guid occurrenceId,
        string schedulerInstanceId,
        string ownershipToken,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.syncRoot)
        {
            var nowUtc = this.timeProvider.GetUtcNow();
            var isOwned = this.leases.TryGetValue(occurrenceId, out var existing)
                && existing.ExpiresUtc > nowUtc
                && string.Equals(existing.SchedulerInstanceId, schedulerInstanceId, StringComparison.Ordinal)
                && string.Equals(existing.OwnershipToken, ownershipToken, StringComparison.Ordinal);

            return Task.FromResult(isOwned);
        }
    }

    /// <inheritdoc />
    public Task<bool> ReleaseAsync(
        Guid occurrenceId,
        string schedulerInstanceId,
        string ownershipToken,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.syncRoot)
        {
            if (!this.leases.TryGetValue(occurrenceId, out var existing)
                || !string.Equals(existing.SchedulerInstanceId, schedulerInstanceId, StringComparison.Ordinal)
                || !string.Equals(existing.OwnershipToken, ownershipToken, StringComparison.Ordinal))
            {
                return Task.FromResult(false);
            }

            this.leases.TryRemove(occurrenceId, out _);
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobLeaseRecord>> ListExpiredAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.syncRoot)
        {
            return Task.FromResult<IReadOnlyList<JobLeaseRecord>>(
                this.leases.Values
                    .Where(x => x.ExpiresUtc <= asOfUtc)
                    .OrderBy(x => x.ExpiresUtc)
                    .ThenBy(x => x.OccurrenceId)
                    .ToArray());
        }
    }

    /// <inheritdoc />
    public Task UpsertAsync(JobLeaseRecord lease, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(lease);
        this.leases[lease.OccurrenceId] = lease;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(Guid occurrenceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.leases.TryRemove(occurrenceId, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task AppendAsync(JobExecutionHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(entry);

        lock (this.syncRoot)
        {
            var list = this.historyByOccurrence.GetOrAdd(entry.OccurrenceId, _ => []);
            list.Add(entry);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobExecutionHistoryEntry>> ListAsync(Guid occurrenceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<JobExecutionHistoryEntry>>(this.historyByOccurrence.TryGetValue(occurrenceId, out var items)
            ? items.OrderBy(x => x.RecordedAt).ThenBy(x => x.HistoryId).ToArray()
            : []);
    }

    /// <inheritdoc />
    public Task<int> PurgeAsync(DateTimeOffset olderThanUtc, IReadOnlyCollection<Guid> historyIds, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (historyIds is null || historyIds.Count == 0)
        {
            return Task.FromResult(0);
        }

        var removed = 0;
        lock (this.syncRoot)
        {
            foreach (var key in this.historyByOccurrence.Keys.ToArray())
            {
                if (!this.historyByOccurrence.TryGetValue(key, out var items))
                {
                    continue;
                }

                removed += items.RemoveAll(x => x.RecordedAt <= olderThanUtc && historyIds.Contains(x.HistoryId));
                if (items.Count == 0)
                {
                    this.historyByOccurrence.TryRemove(key, out _);
                }
            }
        }

        return Task.FromResult(removed);
    }

    /// <inheritdoc />
    Task IJobBatchHistoryStore.AppendAsync(JobBatchHistoryEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(entry);

        lock (this.syncRoot)
        {
            var list = this.historyByBatch.GetOrAdd(entry.BatchId, _ => []);
            list.Add(entry);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    Task<IReadOnlyList<JobBatchHistoryEntry>> IJobBatchHistoryStore.ListAsync(Guid batchId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<JobBatchHistoryEntry>>(this.historyByBatch.TryGetValue(batchId, out var items)
            ? items.OrderBy(x => x.RecordedAt).ThenBy(x => x.HistoryId).ToArray()
            : []);
    }

    /// <inheritdoc />
    Task<int> IJobBatchHistoryStore.PurgeAsync(DateTimeOffset olderThanUtc, IReadOnlyCollection<Guid> historyIds, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (historyIds is null || historyIds.Count == 0)
        {
            return Task.FromResult(0);
        }

        var removed = 0;
        lock (this.syncRoot)
        {
            foreach (var key in this.historyByBatch.Keys.ToArray())
            {
                if (!this.historyByBatch.TryGetValue(key, out var items))
                {
                    continue;
                }

                removed += items.RemoveAll(x => x.RecordedAt <= olderThanUtc && historyIds.Contains(x.HistoryId));
                if (items.Count == 0)
                {
                    this.historyByBatch.TryRemove(key, out _);
                }
            }
        }

        return Task.FromResult(removed);
    }

    /// <inheritdoc />
    public Task<bool> TryAcceptAsync(JobAcceptedEvent acceptedEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(acceptedEvent);

        lock (this.syncRoot)
        {
            var dedupeKey = BuildAcceptedEventKey(acceptedEvent.Source, acceptedEvent.IdempotencyKey);
            if (this.acceptedEventKeys.ContainsKey(dedupeKey))
            {
                return Task.FromResult(false);
            }

            this.acceptedEventKeys[dedupeKey] = acceptedEvent.AcceptedEventId;
            this.acceptedEvents[acceptedEvent.AcceptedEventId] = acceptedEvent;
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobAcceptedEvent>> ListPendingAsync(
        string source,
        Type eventDataType,
        DateTimeOffset? afterAcceptedUtc,
        Guid? afterAcceptedEventId,
        int take,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedTake = Math.Max(1, take);
        var items = this.acceptedEvents.Values
            .Where(x => string.Equals(x.Source, source, StringComparison.OrdinalIgnoreCase))
            .Where(x => eventDataType is null || eventDataType.IsAssignableFrom(x.DataType))
            .Where(x => afterAcceptedUtc is null
                || x.AcceptedUtc > afterAcceptedUtc.Value
                || (x.AcceptedUtc == afterAcceptedUtc.Value && afterAcceptedEventId.HasValue && x.AcceptedEventId.CompareTo(afterAcceptedEventId.Value) > 0))
            .OrderBy(x => x.AcceptedUtc)
            .ThenBy(x => x.AcceptedEventId)
            .Take(normalizedTake)
            .ToArray();

        return Task.FromResult<IReadOnlyList<JobAcceptedEvent>>(items);
    }

    /// <inheritdoc />
    public Task<JobExecution> GetPreviousExecutionAsync(Guid occurrenceId, Guid currentExecutionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var previous = this.executions.Values
            .Where(x => x.OccurrenceId == occurrenceId && x.ExecutionId != currentExecutionId)
            .OrderByDescending(x => x.AttemptNumber)
            .ThenByDescending(x => x.StartedUtc)
            .FirstOrDefault();
        return Task.FromResult(previous);
    }

    /// <inheritdoc />
    public Task<JobExecution> GetPreviousSuccessfulExecutionAsync(string jobName, string triggerName, DateTimeOffset beforeUtc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var previous = this.executions.Values
            .Where(x => x.JobName == jobName && x.TriggerName == triggerName && x.Status == JobExecutionStatus.Completed && x.CompletedUtc.HasValue && x.CompletedUtc.Value < beforeUtc)
            .OrderByDescending(x => x.CompletedUtc)
            .ThenByDescending(x => x.AttemptNumber)
            .FirstOrDefault();
        return Task.FromResult(previous);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobOccurrence>> ListOccurrencesAsync(CancellationToken cancellationToken = default) => ((IJobOccurrenceStore)this).ListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<JobExecution>> ListExecutionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<JobExecution>>(this.executions.Values.OrderBy(x => x.StartedUtc).ThenBy(x => x.AttemptNumber).ToArray());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobExecutionHistoryEntry>> ListExecutionHistoryAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<JobExecutionHistoryEntry>>(this.historyByOccurrence.Values.SelectMany(x => x).OrderBy(x => x.RecordedAt).ThenBy(x => x.HistoryId).ToArray());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobOccurrenceDependency>> ListDependenciesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<JobOccurrenceDependency>>(this.dependenciesByDependent.Values.SelectMany(x => x).OrderBy(x => x.CreatedDate).ThenBy(x => x.DependencyId).ToArray());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobBatch>> ListBatchesAsync(CancellationToken cancellationToken = default) => ((IJobBatchStore)this).ListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<JobBatchHistoryEntry>> ListBatchHistoryAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<JobBatchHistoryEntry>>(this.historyByBatch.Values.SelectMany(x => x).OrderBy(x => x.RecordedAt).ThenBy(x => x.HistoryId).ToArray());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobBatchOccurrence>> ListBatchOccurrencesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<JobBatchOccurrence>>(this.batchMembership.Values.SelectMany(x => x).OrderBy(x => x.BatchId).ThenBy(x => x.Sequence ?? int.MaxValue).ToArray());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobLeaseRecord>> ListLeasesAsync(CancellationToken cancellationToken = default) => ((IJobLeaseStore)this).ListAsync(cancellationToken);

    private Task<JobOccurrence> GetOccurrenceAsync(Guid occurrenceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(this.occurrences.TryGetValue(occurrenceId, out var stored)
            ? stored.ToOccurrence(this.serializer)
            : null);
    }

    private static string BuildTriggerKey(string jobName, string triggerName) => $"{jobName}|{triggerName}";

    private static string BuildAcceptedEventKey(string source, string idempotencyKey) => $"{source}|{idempotencyKey}";

    private static bool ValidateChildOccurrences(
        IReadOnlyList<JobOccurrence> childOccurrences,
        ConcurrentDictionary<string, Guid> occurrenceKeys,
        ConcurrentDictionary<Guid, StoredOccurrence> occurrences)
    {
        foreach (var occurrence in childOccurrences ?? [])
        {
            if (occurrenceKeys.TryGetValue(occurrence.OccurrenceKey, out var existingOccurrenceId)
                && existingOccurrenceId != occurrence.OccurrenceId)
            {
                return false;
            }

            if (occurrences.ContainsKey(occurrence.OccurrenceId)
                && (!occurrenceKeys.TryGetValue(occurrence.OccurrenceKey, out var storedOccurrenceId) || storedOccurrenceId != occurrence.OccurrenceId))
            {
                return false;
            }
        }

        return true;
    }

    private static List<JobBatchOccurrence> DeduplicateMemberships(IReadOnlyList<JobBatchOccurrence> occurrences)
    {
        return (occurrences ?? [])
            .GroupBy(x => x.OccurrenceId)
            .Select(x => x.OrderByDescending(y => y.UpdatedDate).First())
            .OrderBy(x => x.Sequence ?? int.MaxValue)
            .ThenBy(x => x.OccurrenceId)
            .ToList();
    }

    private static void AddDependency(ConcurrentDictionary<Guid, List<JobOccurrenceDependency>> map, Guid key, JobOccurrenceDependency dependency)
    {
        var list = map.GetOrAdd(key, _ => []);
        list.Add(dependency);
    }

    private static void ReplaceDependency(ConcurrentDictionary<Guid, List<JobOccurrenceDependency>> map, Guid key, JobOccurrenceDependency dependency)
    {
        var list = map.GetOrAdd(key, _ => []);
        var index = list.FindIndex(x => x.DependencyId == dependency.DependencyId);
        if (index >= 0)
        {
            list[index] = dependency;
        }
        else
        {
            list.Add(dependency);
        }
    }

    private sealed record StoredOccurrence(
        Guid OccurrenceId,
        string OccurrenceKey,
        string JobName,
        string TriggerName,
        JobTriggerType TriggerType,
        JobOccurrenceStatus Status,
        DateTimeOffset DueUtc,
        DateTimeOffset? ScheduledUtc,
        byte[] DataBytes,
        byte[] PropertiesBytes,
        string DataTypeName,
        string CorrelationId,
        string CausationId,
        string IdempotencyKey,
        string BlockedReason,
        DateTimeOffset CreatedDate,
        DateTimeOffset UpdatedDate)
    {
        public static StoredOccurrence From(JobOccurrence occurrence, ISerializer serializer)
        {
            return new StoredOccurrence(
                occurrence.OccurrenceId,
                occurrence.OccurrenceKey,
                occurrence.JobName,
                occurrence.TriggerName,
                occurrence.TriggerType,
                occurrence.Status,
                occurrence.DueUtc,
                occurrence.ScheduledUtc,
                Serialize(serializer, occurrence.Data),
                Serialize(serializer, occurrence.Properties),
                occurrence.DataType?.AssemblyQualifiedName,
                occurrence.CorrelationId,
                occurrence.CausationId,
                occurrence.IdempotencyKey,
                occurrence.BlockedReason,
                occurrence.CreatedDate,
                occurrence.UpdatedDate);
        }

        public JobOccurrence ToOccurrence(ISerializer serializer)
        {
            var dataType = this.DataTypeName is null ? typeof(Unit) : Type.GetType(this.DataTypeName, throwOnError: true);
            return new JobOccurrence
            {
                OccurrenceId = this.OccurrenceId,
                OccurrenceKey = this.OccurrenceKey,
                JobName = this.JobName,
                TriggerName = this.TriggerName,
                TriggerType = this.TriggerType,
                Status = this.Status,
                DueUtc = this.DueUtc,
                ScheduledUtc = this.ScheduledUtc,
                Data = Deserialize(serializer, this.DataBytes, dataType) ?? Unit.Value,
                DataType = dataType,
                Properties = Deserialize(serializer, this.PropertiesBytes, typeof(PropertyBag)) as PropertyBag ?? new PropertyBag(),
                CorrelationId = this.CorrelationId,
                CausationId = this.CausationId,
                IdempotencyKey = this.IdempotencyKey,
                BlockedReason = this.BlockedReason,
                CreatedDate = this.CreatedDate,
                UpdatedDate = this.UpdatedDate,
            };
        }

        private static byte[] Serialize(ISerializer serializer, object value)
        {
            if (value is null)
            {
                return [];
            }

            using var stream = new MemoryStream();
            serializer.Serialize(value, stream);
            return stream.ToArray();
        }

        private static object Deserialize(ISerializer serializer, byte[] payload, Type type)
        {
            if (payload is null || payload.Length == 0)
            {
                return null;
            }

            using var stream = new MemoryStream(payload, writable: false);
            return serializer.Deserialize(stream, type);
        }
    }
}
