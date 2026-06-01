// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Jobs;

using BridgingIT.DevKit.Application.Jobs;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Provides an Entity Framework backed jobs persistence implementation.
/// </summary>
/// <typeparam name="TContext">The database context type that implements <see cref="IJobsContext"/>.</typeparam>
public class EntityFrameworkJobStoreProvider<TContext> :
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
    where TContext : DbContext, IJobsContext
{
    private readonly IServiceProvider serviceProvider;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<EntityFrameworkJobStoreProvider<TContext>> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityFrameworkJobStoreProvider{TContext}"/> class.
    /// </summary>
    /// <param name="serviceProvider">The root service provider.</param>
    /// <param name="options">The provider options.</param>
    /// <param name="loggerFactory">The optional logger factory.</param>
    public EntityFrameworkJobStoreProvider(
        IServiceProvider serviceProvider,
        EntityFrameworkJobsOptions options,
        TimeProvider timeProvider = null,
        ILoggerFactory loggerFactory = null)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        this.Options = options ?? throw new ArgumentNullException(nameof(options));
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<EntityFrameworkJobStoreProvider<TContext>>();
    }

    public IJobRuntimeStateStore RuntimeStates => this;

    public IJobTriggerRuntimeStateStore TriggerRuntimeStates => this;

    public IJobOccurrenceStore Occurrences => this;

    public IJobExecutionStore Executions => this;

    public IJobOccurrenceDependencyStore Dependencies => this;

    public IJobBatchStore Batches => this;

    public IJobLeaseStore Leases => this;

    public IJobExecutionHistoryStore ExecutionHistory => this;

    public IJobBatchHistoryStore BatchHistory => this;

    public IJobAcceptedEventStore AcceptedEvents => this;

    public IJobPreviousExecutionStore PreviousExecutions => this;

    public IJobSchedulerQueryStore Queries => this;

    /// <summary>
    /// Gets the provider options.
    /// </summary>
    public EntityFrameworkJobsOptions Options { get; }

    /// <inheritdoc />
    public Task<JobRuntimeState> GetAsync(string jobName, CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var row = await dbContext.JobRuntimeStates.AsNoTracking()
                .SingleOrDefaultAsync(x => x.JobName == jobName, cancellationToken)
                .ConfigureAwait(false);

            return row is null ? null : ToModel(row);
        });
    }

    /// <inheritdoc />
    Task<IReadOnlyList<JobRuntimeState>> IJobRuntimeStateStore.ListAsync(CancellationToken cancellationToken)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var items = await dbContext.JobRuntimeStates.AsNoTracking()
                .OrderBy(x => x.JobName)
                .Select(x => ToModel(x))
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);

            return (IReadOnlyList<JobRuntimeState>)items;
        });
    }

    /// <inheritdoc />
    public Task UpsertAsync(JobRuntimeState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        return this.ExecuteAsync(async dbContext =>
        {
            var row = await dbContext.JobRuntimeStates
                .SingleOrDefaultAsync(x => x.JobName == state.JobName, cancellationToken)
                .ConfigureAwait(false);

            if (row is null)
            {
                dbContext.JobRuntimeStates.Add(ToEntity(state));
            }
            else
            {
                row.Enabled = state.Enabled;
                row.Paused = state.Paused;
                row.CreatedDate = state.CreatedDate;
                row.UpdatedDate = state.UpdatedDate;
                row.AdvanceConcurrencyVersion();
            }

            await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
            return 0;
        });
    }

    /// <inheritdoc />
    public Task RemoveAsync(string jobName, CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var row = await dbContext.JobRuntimeStates
                .SingleOrDefaultAsync(x => x.JobName == jobName, cancellationToken)
                .ConfigureAwait(false);

            if (row is not null)
            {
                dbContext.JobRuntimeStates.Remove(row);
                await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
            }

            return 0;
        });
    }

    /// <inheritdoc />
    public Task<JobTriggerRuntimeState> GetAsync(string jobName, string triggerName, CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var row = await dbContext.JobTriggerRuntimeStates.AsNoTracking()
                .SingleOrDefaultAsync(x => x.JobName == jobName && x.TriggerName == triggerName, cancellationToken)
                .ConfigureAwait(false);

            return row is null ? null : ToModel(row);
        });
    }

    /// <inheritdoc />
    Task<IReadOnlyList<(string JobName, string TriggerName, JobTriggerRuntimeState State)>> IJobTriggerRuntimeStateStore.ListAsync(CancellationToken cancellationToken)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var items = await dbContext.JobTriggerRuntimeStates.AsNoTracking()
                .OrderBy(x => x.JobName)
                .ThenBy(x => x.TriggerName)
                .Select(x => new ValueTuple<string, string, JobTriggerRuntimeState>(x.JobName, x.TriggerName, ToModel(x)))
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);

            return (IReadOnlyList<(string JobName, string TriggerName, JobTriggerRuntimeState State)>)items;
        });
    }

    /// <inheritdoc />
    public Task UpsertAsync(string jobName, string triggerName, JobTriggerRuntimeState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        return this.ExecuteAsync(async dbContext =>
        {
            var row = await dbContext.JobTriggerRuntimeStates
                .SingleOrDefaultAsync(x => x.JobName == jobName && x.TriggerName == triggerName, cancellationToken)
                .ConfigureAwait(false);

            if (row is null)
            {
                dbContext.JobTriggerRuntimeStates.Add(ToEntity(jobName, triggerName, state));
            }
            else
            {
                row.ActivatedUtc = state.ActivatedUtc;
                row.DueUtc = state.DueUtc;
                row.LastMaterializedScheduledUtc = state.LastMaterializedScheduledUtc;
                row.HasMaterializedOccurrence = state.HasMaterializedOccurrence;
                row.Enabled = state.Enabled;
                row.Paused = state.Paused;
                row.CreatedDate = state.CreatedDate;
                row.UpdatedDate = state.UpdatedDate;
                row.AdvanceConcurrencyVersion();
            }

            await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
            return 0;
        });
    }

    /// <inheritdoc />
    public Task RemoveAsync(string jobName, string triggerName, CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var row = await dbContext.JobTriggerRuntimeStates
                .SingleOrDefaultAsync(x => x.JobName == jobName && x.TriggerName == triggerName, cancellationToken)
                .ConfigureAwait(false);

            if (row is not null)
            {
                dbContext.JobTriggerRuntimeStates.Remove(row);
                await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
            }

            return 0;
        });
    }

    /// <inheritdoc />
    public Task<bool> TryCreateAsync(JobOccurrence occurrence, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(occurrence);

        return this.ExecuteAsync(async dbContext =>
        {
            if (await dbContext.JobOccurrences.AsNoTracking().AnyAsync(x => x.OccurrenceKey == occurrence.OccurrenceKey, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            dbContext.JobOccurrences.Add(ToEntity(occurrence, this.Options.Serializer));
            try
            {
                await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (DbUpdateException)
            {
                if (await dbContext.JobOccurrences.AsNoTracking().AnyAsync(x => x.OccurrenceKey == occurrence.OccurrenceKey, cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }

                throw;
            }
        });
    }

    /// <inheritdoc />
    Task<JobOccurrence> IJobOccurrenceStore.GetAsync(Guid occurrenceId, CancellationToken cancellationToken)
    {
        return this.GetOccurrenceInternalAsync(occurrenceId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<JobOccurrence> GetByKeyAsync(string occurrenceKey, CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var row = await dbContext.JobOccurrences.AsNoTracking()
                .SingleOrDefaultAsync(x => x.OccurrenceKey == occurrenceKey, cancellationToken)
                .ConfigureAwait(false);

            return row is null ? null : ToModel(row, this.Options.Serializer);
        });
    }

    /// <inheritdoc />
    Task<IReadOnlyList<JobOccurrence>> IJobOccurrenceStore.ListAsync(CancellationToken cancellationToken)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var query = dbContext.JobOccurrences.AsNoTracking();
            var items = IsSqlite(dbContext)
                ? (await query.ToArrayAsync(cancellationToken).ConfigureAwait(false))
                    .OrderBy(x => x.DueUtc)
                    .ThenBy(x => x.OccurrenceId)
                    .ToArray()
                : await query
                    .OrderBy(x => x.DueUtc)
                    .ThenBy(x => x.OccurrenceId)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);

            return (IReadOnlyList<JobOccurrence>)items.Select(x => ToModel(x, this.Options.Serializer)).ToArray();
        });
    }

    /// <inheritdoc />
    public Task UpdateAsync(JobOccurrence occurrence, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(occurrence);

        return this.ExecuteAsync(async dbContext =>
        {
            var row = await dbContext.JobOccurrences
                .SingleOrDefaultAsync(x => x.OccurrenceId == occurrence.OccurrenceId, cancellationToken)
                .ConfigureAwait(false);

            if (row is null)
            {
                dbContext.JobOccurrences.Add(ToEntity(occurrence, this.Options.Serializer));
            }
            else
            {
                Apply(row, occurrence, this.Options.Serializer);
            }

            await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
            return 0;
        });
    }

    /// <inheritdoc />
    Task IJobOccurrenceStore.RemoveAsync(Guid occurrenceId, CancellationToken cancellationToken)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var row = await dbContext.JobOccurrences
                .SingleOrDefaultAsync(x => x.OccurrenceId == occurrenceId, cancellationToken)
                .ConfigureAwait(false);

            if (row is null)
            {
                return 0;
            }

            dbContext.JobOccurrences.Remove(row);
            await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
            return 0;
        });
    }

    /// <inheritdoc />
    public Task CreateAsync(JobExecution execution, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(execution);

        return this.ExecuteAsync(async dbContext =>
        {
            dbContext.JobExecutions.Add(ToEntity(execution));
            await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
            return 0;
        });
    }

    /// <inheritdoc />
    Task<JobExecution> IJobExecutionStore.GetAsync(Guid executionId, CancellationToken cancellationToken)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var row = await dbContext.JobExecutions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.ExecutionId == executionId, cancellationToken)
                .ConfigureAwait(false);

            return row is null ? null : ToModel(row);
        });
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobExecution>> ListByOccurrenceAsync(Guid occurrenceId, CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var query = dbContext.JobExecutions.AsNoTracking()
                .Where(x => x.OccurrenceId == occurrenceId);
            var items = IsSqlite(dbContext)
                ? (await query.ToArrayAsync(cancellationToken).ConfigureAwait(false))
                    .OrderBy(x => x.AttemptNumber)
                    .ThenBy(x => x.StartedUtc)
                    .ToArray()
                : await query
                    .OrderBy(x => x.AttemptNumber)
                    .ThenBy(x => x.StartedUtc)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);

            return (IReadOnlyList<JobExecution>)items.Select(ToModel).ToArray();
        });
    }

    /// <inheritdoc />
    public Task UpdateAsync(JobExecution execution, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(execution);

        return this.ExecuteAsync(async dbContext =>
        {
            var row = await dbContext.JobExecutions
                .SingleOrDefaultAsync(x => x.ExecutionId == execution.ExecutionId, cancellationToken)
                .ConfigureAwait(false);

            if (row is null)
            {
                dbContext.JobExecutions.Add(ToEntity(execution));
            }
            else
            {
                Apply(row, execution);
            }

            await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
            return 0;
        });
    }

    /// <inheritdoc />
    Task<int> IJobExecutionStore.RemoveByOccurrenceAsync(Guid occurrenceId, CancellationToken cancellationToken)
    {
        return this.ExecuteAsync(async dbContext =>
            await dbContext.JobExecutions
                .Where(x => x.OccurrenceId == occurrenceId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false));
    }

    /// <inheritdoc />
    public Task AddAsync(JobOccurrenceDependency dependency, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dependency);

        return this.ExecuteAsync(async dbContext =>
        {
            dbContext.JobOccurrenceDependencies.Add(ToEntity(dependency, this.Options.Serializer));
            await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
            return 0;
        });
    }

    /// <inheritdoc />
    public Task UpdateAsync(JobOccurrenceDependency dependency, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dependency);

        return this.ExecuteAsync(async dbContext =>
        {
            var row = await dbContext.JobOccurrenceDependencies
                .SingleOrDefaultAsync(x => x.DependencyId == dependency.DependencyId, cancellationToken)
                .ConfigureAwait(false);

            if (row is null)
            {
                dbContext.JobOccurrenceDependencies.Add(ToEntity(dependency, this.Options.Serializer));
            }
            else
            {
                Apply(row, dependency, this.Options.Serializer);
            }

            await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
            return 0;
        });
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobOccurrenceDependency>> ListByDependentAsync(Guid dependentOccurrenceId, CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var query = dbContext.JobOccurrenceDependencies.AsNoTracking()
                .Where(x => x.DependentOccurrenceId == dependentOccurrenceId);
            var items = IsSqlite(dbContext)
                ? (await query.ToArrayAsync(cancellationToken).ConfigureAwait(false))
                    .OrderBy(x => x.CreatedDate)
                    .ToArray()
                : await query
                    .OrderBy(x => x.CreatedDate)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);

            return (IReadOnlyList<JobOccurrenceDependency>)items.Select(x => ToModel(x, this.Options.Serializer)).ToArray();
        });
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobOccurrenceDependency>> ListByPrerequisiteAsync(Guid prerequisiteOccurrenceId, CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var query = dbContext.JobOccurrenceDependencies.AsNoTracking()
                .Where(x => x.PrerequisiteOccurrenceId == prerequisiteOccurrenceId);
            var items = IsSqlite(dbContext)
                ? (await query.ToArrayAsync(cancellationToken).ConfigureAwait(false))
                    .OrderBy(x => x.CreatedDate)
                    .ToArray()
                : await query
                    .OrderBy(x => x.CreatedDate)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);

            return (IReadOnlyList<JobOccurrenceDependency>)items.Select(x => ToModel(x, this.Options.Serializer)).ToArray();
        });
    }

    /// <inheritdoc />
    Task<int> IJobOccurrenceDependencyStore.RemoveByOccurrenceAsync(Guid occurrenceId, CancellationToken cancellationToken)
    {
        return this.ExecuteAsync(async dbContext =>
            await dbContext.JobOccurrenceDependencies
                .Where(x => x.DependentOccurrenceId == occurrenceId || x.PrerequisiteOccurrenceId == occurrenceId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false));
    }

    /// <inheritdoc />
    public Task<bool> TryCreateAsync(JobBatch batch, IReadOnlyList<JobBatchOccurrence> occurrences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        return this.ExecuteInTransactionAsync(async dbContext =>
        {
            if (await dbContext.JobBatches.AsNoTracking().AnyAsync(x => x.BatchId == batch.BatchId, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            dbContext.JobBatches.Add(ToEntity(batch, this.Options.Serializer));
            foreach (var occurrence in DeduplicateMemberships(occurrences))
            {
                dbContext.JobBatchOccurrences.Add(ToEntity(occurrence));
            }

            await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
            return true;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> TryCreateWithChildrenAsync(JobBatch batch, IReadOnlyList<JobOccurrence> childOccurrences, IReadOnlyList<JobBatchOccurrence> memberships, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        return this.ExecuteInTransactionAsync(async dbContext =>
        {
            if (await dbContext.JobBatches.AsNoTracking().AnyAsync(x => x.BatchId == batch.BatchId, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            if (!await ValidateChildOccurrencesAsync(dbContext, childOccurrences, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            dbContext.JobBatches.Add(ToEntity(batch, this.Options.Serializer));
            await AddMissingChildOccurrencesAsync(dbContext, childOccurrences, cancellationToken).ConfigureAwait(false);

            foreach (var membership in DeduplicateMemberships(memberships))
            {
                dbContext.JobBatchOccurrences.Add(ToEntity(membership));
            }

            await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
            return true;
        }, cancellationToken);
    }

    /// <inheritdoc />
    Task<JobBatch> IJobBatchStore.GetAsync(Guid batchId, CancellationToken cancellationToken)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var row = await dbContext.JobBatches.AsNoTracking()
                .SingleOrDefaultAsync(x => x.BatchId == batchId, cancellationToken)
                .ConfigureAwait(false);

            return row is null ? null : ToModel(row, this.Options.Serializer);
        });
    }

    /// <inheritdoc />
    Task<IReadOnlyList<JobBatch>> IJobBatchStore.ListAsync(CancellationToken cancellationToken)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var query = dbContext.JobBatches.AsNoTracking();
            var items = IsSqlite(dbContext)
                ? (await query.ToArrayAsync(cancellationToken).ConfigureAwait(false))
                    .OrderBy(x => x.CreatedDate)
                    .ToArray()
                : await query
                    .OrderBy(x => x.CreatedDate)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);

            return (IReadOnlyList<JobBatch>)items.Select(x => ToModel(x, this.Options.Serializer)).ToArray();
        });
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobBatchOccurrence>> ListOccurrencesAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var items = await dbContext.JobBatchOccurrences.AsNoTracking()
                .Where(x => x.BatchId == batchId)
                .OrderBy(x => x.Sequence ?? int.MaxValue)
                .ThenBy(x => x.OccurrenceId)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);

            return (IReadOnlyList<JobBatchOccurrence>)items.Select(ToModel).ToArray();
        });
    }

    /// <inheritdoc />
    public Task AttachAsync(Guid batchId, IReadOnlyList<JobBatchOccurrence> occurrences, CancellationToken cancellationToken = default)
    {
        return this.ExecuteInTransactionAsync(async dbContext =>
        {
            var existingIds = await dbContext.JobBatchOccurrences.AsNoTracking()
                .Where(x => x.BatchId == batchId)
                .Select(x => x.OccurrenceId)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var occurrence in DeduplicateMemberships(occurrences).Where(x => !existingIds.Contains(x.OccurrenceId)))
            {
                dbContext.JobBatchOccurrences.Add(ToEntity(occurrence));
            }

            await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
            return 0;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> TryAttachChildrenAsync(Guid batchId, IReadOnlyList<JobOccurrence> childOccurrences, IReadOnlyList<JobBatchOccurrence> memberships, CancellationToken cancellationToken = default)
    {
        return this.ExecuteInTransactionAsync(async dbContext =>
        {
            var exists = await dbContext.JobBatches.AsNoTracking().AnyAsync(x => x.BatchId == batchId, cancellationToken).ConfigureAwait(false);
            if (!exists)
            {
                return false;
            }

            if (!await ValidateChildOccurrencesAsync(dbContext, childOccurrences, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            await AddMissingChildOccurrencesAsync(dbContext, childOccurrences, cancellationToken).ConfigureAwait(false);

            var existingIds = await dbContext.JobBatchOccurrences.AsNoTracking()
                .Where(x => x.BatchId == batchId)
                .Select(x => x.OccurrenceId)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var membership in DeduplicateMemberships(memberships).Where(x => !existingIds.Contains(x.OccurrenceId)))
            {
                dbContext.JobBatchOccurrences.Add(ToEntity(membership));
            }

            await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
            return true;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task ReplaceOccurrencesAsync(Guid batchId, IReadOnlyList<JobBatchOccurrence> occurrences, CancellationToken cancellationToken = default)
    {
        return this.ReplaceOccurrencesCoreAsync(batchId, occurrences, retryOnDuplicateKey: true, cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> RemoveOccurrencesAsync(IReadOnlyCollection<Guid> occurrenceIds, CancellationToken cancellationToken = default)
    {
        if (occurrenceIds is null || occurrenceIds.Count == 0)
        {
            return Task.FromResult(0);
        }

        var selectedIds = occurrenceIds.Where(x => x != Guid.Empty).Distinct().ToArray();
        if (selectedIds.Length == 0)
        {
            return Task.FromResult(0);
        }

        return this.ExecuteAsync(async dbContext =>
            await dbContext.JobBatchOccurrences
                .Where(x => selectedIds.Contains(x.OccurrenceId))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false));
    }

    private async Task ReplaceOccurrencesCoreAsync(Guid batchId, IReadOnlyList<JobBatchOccurrence> occurrences, bool retryOnDuplicateKey, CancellationToken cancellationToken)
    {
        try
        {
            await this.ExecuteInTransactionAsync(async dbContext =>
            {
                var existing = await dbContext.JobBatchOccurrences
                    .Where(x => x.BatchId == batchId)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);

                var desired = DeduplicateMemberships(occurrences)
                    .ToDictionary(x => x.OccurrenceId);

                foreach (var row in existing)
                {
                    if (desired.TryGetValue(row.OccurrenceId, out var occurrence))
                    {
                        Apply(row, occurrence);
                        desired.Remove(row.OccurrenceId);
                    }
                    else
                    {
                        dbContext.JobBatchOccurrences.Remove(row);
                    }
                }

                foreach (var occurrence in desired.Values)
                {
                    dbContext.JobBatchOccurrences.Add(ToEntity(occurrence));
                }

                await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
                return 0;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (retryOnDuplicateKey && IsBatchOccurrenceDuplicateKeyViolation(exception))
        {
            await this.ReplaceOccurrencesCoreAsync(batchId, occurrences, retryOnDuplicateKey: false, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException exception) when (retryOnDuplicateKey && IsJobsPersistenceConcurrencyViolation(exception))
        {
            await this.ReplaceOccurrencesCoreAsync(batchId, occurrences, retryOnDuplicateKey: false, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task UpdateAsync(JobBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        return this.ExecuteAsync(async dbContext =>
        {
            var row = await dbContext.JobBatches
                .SingleOrDefaultAsync(x => x.BatchId == batch.BatchId, cancellationToken)
                .ConfigureAwait(false);

            if (row is null)
            {
                dbContext.JobBatches.Add(ToEntity(batch, this.Options.Serializer));
            }
            else
            {
                Apply(row, batch, this.Options.Serializer);
            }

            await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
            return 0;
        });
    }

    /// <inheritdoc />
    Task<JobLeaseRecord> IJobLeaseStore.GetAsync(Guid occurrenceId, CancellationToken cancellationToken)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var row = await dbContext.JobLeases.AsNoTracking()
                .SingleOrDefaultAsync(x => x.OccurrenceId == occurrenceId, cancellationToken)
                .ConfigureAwait(false);

            return row is null ? null : ToModel(row);
        });
    }

    /// <inheritdoc />
    Task<IReadOnlyList<JobLeaseRecord>> IJobLeaseStore.ListAsync(CancellationToken cancellationToken)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var items = await dbContext.JobLeases.AsNoTracking()
                .OrderBy(x => x.OccurrenceId)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);

            return (IReadOnlyList<JobLeaseRecord>)items.Select(ToModel).ToArray();
        });
    }

    /// <inheritdoc />
    public Task<JobLeaseRecord> TryAcquireAsync(Guid occurrenceId, string schedulerInstanceId, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        return this.ExecuteInTransactionAsync(async dbContext =>
        {
            var nowUtc = this.ResolveNowUtc();
            var row = await dbContext.JobLeases
                .SingleOrDefaultAsync(x => x.OccurrenceId == occurrenceId, cancellationToken)
                .ConfigureAwait(false);

            if (row is not null && row.ExpiresUtc > nowUtc)
            {
                return null;
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

            if (row is null)
            {
                dbContext.JobLeases.Add(ToEntity(lease));
            }
            else
            {
                Apply(row, lease);
            }

            try
            {
                await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
                return lease;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<JobLeaseRecord> RenewAsync(Guid occurrenceId, string schedulerInstanceId, string ownershipToken, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        return this.ExecuteAsync(async dbContext =>
        {
            var nowUtc = this.ResolveNowUtc();
            var row = await dbContext.JobLeases
                .SingleOrDefaultAsync(x => x.OccurrenceId == occurrenceId, cancellationToken)
                .ConfigureAwait(false);

            if (row is null
                || row.ExpiresUtc <= nowUtc
                || !string.Equals(row.SchedulerInstanceId, schedulerInstanceId, StringComparison.Ordinal)
                || !string.Equals(row.OwnershipToken, ownershipToken, StringComparison.Ordinal))
            {
                return null;
            }

            var lease = new JobLeaseRecord
            {
                OccurrenceId = row.OccurrenceId,
                SchedulerInstanceId = row.SchedulerInstanceId,
                OwnershipToken = row.OwnershipToken,
                AcquiredUtc = row.AcquiredUtc,
                RenewedUtc = nowUtc,
                ExpiresUtc = nowUtc.Add(duration),
                RenewalCount = row.RenewalCount + 1,
                CreatedDate = row.CreatedDate,
                UpdatedDate = nowUtc,
            };

            Apply(row, lease);
            await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
            return lease;
        });
    }

    /// <inheritdoc />
    public Task<bool> VerifyOwnershipAsync(Guid occurrenceId, string schedulerInstanceId, string ownershipToken, CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var nowUtc = this.ResolveNowUtc();
            var query = dbContext.JobLeases.AsNoTracking()
                .Where(x => x.OccurrenceId == occurrenceId
                    && x.SchedulerInstanceId == schedulerInstanceId
                    && x.OwnershipToken == ownershipToken);
            return IsSqlite(dbContext)
                ? (await query.ToArrayAsync(cancellationToken).ConfigureAwait(false)).Any(x => x.ExpiresUtc > nowUtc)
                : await query.AnyAsync(x => x.ExpiresUtc > nowUtc, cancellationToken).ConfigureAwait(false);
        });
    }

    /// <inheritdoc />
    public Task<bool> ReleaseAsync(Guid occurrenceId, string schedulerInstanceId, string ownershipToken, CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var row = await dbContext.JobLeases
                .SingleOrDefaultAsync(x => x.OccurrenceId == occurrenceId, cancellationToken)
                .ConfigureAwait(false);

            if (row is null
                || !string.Equals(row.SchedulerInstanceId, schedulerInstanceId, StringComparison.Ordinal)
                || !string.Equals(row.OwnershipToken, ownershipToken, StringComparison.Ordinal))
            {
                return false;
            }

            dbContext.JobLeases.Remove(row);
            await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
            return true;
        });
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobLeaseRecord>> ListExpiredAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var query = dbContext.JobLeases.AsNoTracking();
            var items = IsSqlite(dbContext)
                ? (await query.ToArrayAsync(cancellationToken).ConfigureAwait(false))
                    .Where(x => x.ExpiresUtc <= asOfUtc)
                    .OrderBy(x => x.ExpiresUtc)
                    .ThenBy(x => x.OccurrenceId)
                    .ToArray()
                : await query
                    .Where(x => x.ExpiresUtc <= asOfUtc)
                    .OrderBy(x => x.ExpiresUtc)
                    .ThenBy(x => x.OccurrenceId)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);

            return (IReadOnlyList<JobLeaseRecord>)items.Select(ToModel).ToArray();
        });
    }

    /// <inheritdoc />
    public Task UpsertAsync(JobLeaseRecord lease, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);

        return this.ExecuteAsync(async dbContext =>
        {
            var row = await dbContext.JobLeases
                .SingleOrDefaultAsync(x => x.OccurrenceId == lease.OccurrenceId, cancellationToken)
                .ConfigureAwait(false);

            if (row is null)
            {
                dbContext.JobLeases.Add(ToEntity(lease));
            }
            else
            {
                Apply(row, lease);
            }

            await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
            return 0;
        });
    }

    /// <inheritdoc />
    public Task RemoveAsync(Guid occurrenceId, CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var row = await dbContext.JobLeases
                .SingleOrDefaultAsync(x => x.OccurrenceId == occurrenceId, cancellationToken)
                .ConfigureAwait(false);

            if (row is not null)
            {
                dbContext.JobLeases.Remove(row);
                await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
            }

            return 0;
        });
    }

    /// <inheritdoc />
    public Task AppendAsync(JobExecutionHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return this.ExecuteAsync(async dbContext =>
        {
            dbContext.JobExecutionHistory.Add(ToEntity(entry, this.Options.Serializer));
            await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
            return 0;
        });
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobExecutionHistoryEntry>> ListAsync(Guid occurrenceId, CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var query = dbContext.JobExecutionHistory.AsNoTracking()
                .Where(x => x.OccurrenceId == occurrenceId);
            var items = IsSqlite(dbContext)
                ? (await query.ToArrayAsync(cancellationToken).ConfigureAwait(false))
                    .OrderBy(x => x.RecordedAt)
                    .ThenBy(x => x.HistoryId)
                    .ToArray()
                : await query
                    .OrderBy(x => x.RecordedAt)
                    .ThenBy(x => x.HistoryId)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);

            return (IReadOnlyList<JobExecutionHistoryEntry>)items.Select(x => ToModel(x, this.Options.Serializer)).ToArray();
        });
    }

    /// <inheritdoc />
    public Task<int> PurgeAsync(DateTimeOffset olderThanUtc, IReadOnlyCollection<Guid> historyIds, CancellationToken cancellationToken = default)
    {
        if (historyIds is null || historyIds.Count == 0)
        {
            return Task.FromResult(0);
        }

        return this.ExecuteAsync(async dbContext =>
        {
            var query = dbContext.JobExecutionHistory
                .Where(x => historyIds.Contains(x.HistoryId));
            var items = IsSqlite(dbContext)
                ? (await query.ToArrayAsync(cancellationToken).ConfigureAwait(false))
                    .Where(x => x.RecordedAt <= olderThanUtc)
                    .ToArray()
                : await query
                    .Where(x => x.RecordedAt <= olderThanUtc)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);

            if (items.Length == 0)
            {
                return 0;
            }

            dbContext.JobExecutionHistory.RemoveRange(items);
            await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
            return items.Length;
        });
    }

    /// <inheritdoc />
    public Task AppendAsync(JobBatchHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return this.ExecuteAsync(async dbContext =>
        {
            dbContext.JobBatchHistory.Add(ToEntity(entry, this.Options.Serializer));
            await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
            return 0;
        });
    }

    /// <inheritdoc />
    Task<IReadOnlyList<JobBatchHistoryEntry>> IJobBatchHistoryStore.ListAsync(Guid batchId, CancellationToken cancellationToken)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var query = dbContext.JobBatchHistory.AsNoTracking()
                .Where(x => x.BatchId == batchId);
            var items = IsSqlite(dbContext)
                ? (await query.ToArrayAsync(cancellationToken).ConfigureAwait(false))
                    .OrderBy(x => x.RecordedAt)
                    .ThenBy(x => x.HistoryId)
                    .ToArray()
                : await query
                    .OrderBy(x => x.RecordedAt)
                    .ThenBy(x => x.HistoryId)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);

            return (IReadOnlyList<JobBatchHistoryEntry>)items.Select(x => ToModel(x, this.Options.Serializer)).ToArray();
        });
    }

    /// <inheritdoc />
    Task<int> IJobBatchHistoryStore.PurgeAsync(DateTimeOffset olderThanUtc, IReadOnlyCollection<Guid> historyIds, CancellationToken cancellationToken)
    {
        if (historyIds is null || historyIds.Count == 0)
        {
            return Task.FromResult(0);
        }

        return this.ExecuteAsync(async dbContext =>
        {
            var query = dbContext.JobBatchHistory
                .Where(x => historyIds.Contains(x.HistoryId));
            var items = IsSqlite(dbContext)
                ? (await query.ToArrayAsync(cancellationToken).ConfigureAwait(false))
                    .Where(x => x.RecordedAt <= olderThanUtc)
                    .ToArray()
                : await query
                    .Where(x => x.RecordedAt <= olderThanUtc)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);

            if (items.Length == 0)
            {
                return 0;
            }

            dbContext.JobBatchHistory.RemoveRange(items);
            await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
            return items.Length;
        });
    }

    /// <inheritdoc />
    public Task<bool> TryAcceptAsync(JobAcceptedEvent acceptedEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(acceptedEvent);

        return this.ExecuteAsync(async dbContext =>
        {
            if (await dbContext.JobAcceptedEvents.AsNoTracking().AnyAsync(x => x.Source == acceptedEvent.Source && x.IdempotencyKey == acceptedEvent.IdempotencyKey, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            dbContext.JobAcceptedEvents.Add(ToEntity(acceptedEvent, this.Options.Serializer));
            try
            {
                await SaveChangesAsync(dbContext, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (DbUpdateException)
            {
                if (await dbContext.JobAcceptedEvents.AsNoTracking().AnyAsync(x => x.Source == acceptedEvent.Source && x.IdempotencyKey == acceptedEvent.IdempotencyKey, cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }

                throw;
            }
        });
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
        return this.ExecuteAsync(async dbContext =>
        {
            var normalizedTake = Math.Max(1, take);
            var dataTypeName = eventDataType?.AssemblyQualifiedName;

            var query = dbContext.JobAcceptedEvents.AsNoTracking()
                .Where(x => x.Source == source);

            if (!string.IsNullOrWhiteSpace(dataTypeName))
            {
                query = query.Where(x => x.DataType == dataTypeName);
            }

            JobAcceptedEventEntity[] items;
            if (IsSqlite(dbContext))
            {
                var rows = await query.ToArrayAsync(cancellationToken).ConfigureAwait(false);
                var filtered = rows.AsEnumerable();
                if (afterAcceptedUtc.HasValue)
                {
                    var acceptedUtc = afterAcceptedUtc.Value;
                    filtered = afterAcceptedEventId.HasValue
                        ? filtered.Where(x => x.AcceptedUtc > acceptedUtc || (x.AcceptedUtc == acceptedUtc && x.AcceptedEventId.CompareTo(afterAcceptedEventId.Value) > 0))
                        : filtered.Where(x => x.AcceptedUtc > acceptedUtc);
                }

                items = filtered
                    .OrderBy(x => x.AcceptedUtc)
                    .ThenBy(x => x.AcceptedEventId)
                    .Take(normalizedTake)
                    .ToArray();
            }
            else
            {
                if (afterAcceptedUtc.HasValue)
                {
                    var acceptedUtc = afterAcceptedUtc.Value;
                    if (afterAcceptedEventId.HasValue)
                    {
                        var acceptedEventId = afterAcceptedEventId.Value;
                        query = query.Where(x => x.AcceptedUtc > acceptedUtc || (x.AcceptedUtc == acceptedUtc && x.AcceptedEventId.CompareTo(acceptedEventId) > 0));
                    }
                    else
                    {
                        query = query.Where(x => x.AcceptedUtc > acceptedUtc);
                    }
                }

                items = await query
                    .OrderBy(x => x.AcceptedUtc)
                    .ThenBy(x => x.AcceptedEventId)
                    .Take(normalizedTake)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            return (IReadOnlyList<JobAcceptedEvent>)items.Select(x => ToModel(x, this.Options.Serializer)).ToArray();
        });
    }

    /// <inheritdoc />
    public Task<JobExecution> GetPreviousExecutionAsync(Guid occurrenceId, Guid currentExecutionId, CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var query = dbContext.JobExecutions.AsNoTracking()
                .Where(x => x.OccurrenceId == occurrenceId && x.ExecutionId != currentExecutionId);
            var row = IsSqlite(dbContext)
                ? (await query.ToArrayAsync(cancellationToken).ConfigureAwait(false))
                    .OrderByDescending(x => x.AttemptNumber)
                    .ThenByDescending(x => x.StartedUtc)
                    .FirstOrDefault()
                : await query
                    .OrderByDescending(x => x.AttemptNumber)
                    .ThenByDescending(x => x.StartedUtc)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

            return row is null ? null : ToModel(row);
        });
    }

    /// <inheritdoc />
    public Task<JobExecution> GetPreviousSuccessfulExecutionAsync(string jobName, string triggerName, DateTimeOffset beforeUtc, CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var query = dbContext.JobExecutions.AsNoTracking()
                .Where(x => x.JobName == jobName
                    && x.TriggerName == triggerName
                    && x.Status == JobExecutionStatus.Completed
                    && x.CompletedUtc.HasValue);
            var row = IsSqlite(dbContext)
                ? (await query.ToArrayAsync(cancellationToken).ConfigureAwait(false))
                    .Where(x => x.CompletedUtc.Value < beforeUtc)
                    .OrderByDescending(x => x.CompletedUtc)
                    .ThenByDescending(x => x.AttemptNumber)
                    .FirstOrDefault()
                : await query
                    .Where(x => x.CompletedUtc.Value < beforeUtc)
                    .OrderByDescending(x => x.CompletedUtc)
                    .ThenByDescending(x => x.AttemptNumber)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

            return row is null ? null : ToModel(row);
        });
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobOccurrence>> ListOccurrencesAsync(CancellationToken cancellationToken = default)
        => ((IJobOccurrenceStore)this).ListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<JobExecution>> ListExecutionsAsync(CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var query = dbContext.JobExecutions.AsNoTracking();
            var items = IsSqlite(dbContext)
                ? (await query.ToArrayAsync(cancellationToken).ConfigureAwait(false))
                    .OrderBy(x => x.StartedUtc)
                    .ThenBy(x => x.AttemptNumber)
                    .ToArray()
                : await query
                    .OrderBy(x => x.StartedUtc)
                    .ThenBy(x => x.AttemptNumber)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);

            return (IReadOnlyList<JobExecution>)items.Select(ToModel).ToArray();
        });
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobExecutionHistoryEntry>> ListExecutionHistoryAsync(CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var query = dbContext.JobExecutionHistory.AsNoTracking();
            var items = IsSqlite(dbContext)
                ? (await query.ToArrayAsync(cancellationToken).ConfigureAwait(false))
                    .OrderBy(x => x.RecordedAt)
                    .ThenBy(x => x.HistoryId)
                    .ToArray()
                : await query
                    .OrderBy(x => x.RecordedAt)
                    .ThenBy(x => x.HistoryId)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);

            return (IReadOnlyList<JobExecutionHistoryEntry>)items.Select(x => ToModel(x, this.Options.Serializer)).ToArray();
        });
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobOccurrenceDependency>> ListDependenciesAsync(CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var query = dbContext.JobOccurrenceDependencies.AsNoTracking();
            var items = IsSqlite(dbContext)
                ? (await query.ToArrayAsync(cancellationToken).ConfigureAwait(false))
                    .OrderBy(x => x.CreatedDate)
                    .ThenBy(x => x.DependencyId)
                    .ToArray()
                : await query
                    .OrderBy(x => x.CreatedDate)
                    .ThenBy(x => x.DependencyId)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);

            return (IReadOnlyList<JobOccurrenceDependency>)items.Select(x => ToModel(x, this.Options.Serializer)).ToArray();
        });
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobBatch>> ListBatchesAsync(CancellationToken cancellationToken = default)
        => ((IJobBatchStore)this).ListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<JobBatchHistoryEntry>> ListBatchHistoryAsync(CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var query = dbContext.JobBatchHistory.AsNoTracking();
            var items = IsSqlite(dbContext)
                ? (await query.ToArrayAsync(cancellationToken).ConfigureAwait(false))
                    .OrderBy(x => x.RecordedAt)
                    .ThenBy(x => x.HistoryId)
                    .ToArray()
                : await query
                    .OrderBy(x => x.RecordedAt)
                    .ThenBy(x => x.HistoryId)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);

            return (IReadOnlyList<JobBatchHistoryEntry>)items.Select(x => ToModel(x, this.Options.Serializer)).ToArray();
        });
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobBatchOccurrence>> ListBatchOccurrencesAsync(CancellationToken cancellationToken = default)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var items = await dbContext.JobBatchOccurrences.AsNoTracking()
                .OrderBy(x => x.BatchId)
                .ThenBy(x => x.Sequence ?? int.MaxValue)
                .ThenBy(x => x.OccurrenceId)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);

            return (IReadOnlyList<JobBatchOccurrence>)items.Select(ToModel).ToArray();
        });
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JobLeaseRecord>> ListLeasesAsync(CancellationToken cancellationToken = default)
        => ((IJobLeaseStore)this).ListAsync(cancellationToken);

    private Task<JobOccurrence> GetOccurrenceInternalAsync(Guid occurrenceId, CancellationToken cancellationToken)
    {
        return this.ExecuteAsync(async dbContext =>
        {
            var row = await dbContext.JobOccurrences.AsNoTracking()
                .SingleOrDefaultAsync(x => x.OccurrenceId == occurrenceId, cancellationToken)
                .ConfigureAwait(false);

            return row is null ? null : ToModel(row, this.Options.Serializer);
        });
    }

    private async Task<TResult> ExecuteAsync<TResult>(Func<TContext, Task<TResult>> action)
    {
        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        return await action(dbContext).ConfigureAwait(false);
    }

    private async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<TContext, Task<TResult>> action, CancellationToken cancellationToken)
    {
        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;

        try
        {
            var result = await action(dbContext).ConfigureAwait(false);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task SaveChangesAsync(TContext dbContext, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new InvalidOperationException("A concurrent jobs persistence update was detected.", exception);
        }
    }

    private static bool IsSqlite(DbContext dbContext)
        => dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsBatchOccurrenceDuplicateKeyViolation(DbUpdateException exception)
    {
        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("__Jobs_BatchOccurrences", StringComparison.OrdinalIgnoreCase)
            && (message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                || message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase)
                || message.Contains("PRIMARY KEY constraint", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsJobsPersistenceConcurrencyViolation(InvalidOperationException exception)
        => exception.InnerException is DbUpdateConcurrencyException;

    private static async Task<bool> ValidateChildOccurrencesAsync(TContext dbContext, IReadOnlyList<JobOccurrence> childOccurrences, CancellationToken cancellationToken)
    {
        foreach (var occurrence in childOccurrences ?? [])
        {
            var existingByKey = await dbContext.JobOccurrences.AsNoTracking()
                .SingleOrDefaultAsync(x => x.OccurrenceKey == occurrence.OccurrenceKey, cancellationToken)
                .ConfigureAwait(false);
            if (existingByKey is not null && existingByKey.OccurrenceId != occurrence.OccurrenceId)
            {
                return false;
            }

            var existingById = await dbContext.JobOccurrences.AsNoTracking()
                .SingleOrDefaultAsync(x => x.OccurrenceId == occurrence.OccurrenceId, cancellationToken)
                .ConfigureAwait(false);
            if (existingById is not null && !string.Equals(existingById.OccurrenceKey, occurrence.OccurrenceKey, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private async Task AddMissingChildOccurrencesAsync(TContext dbContext, IReadOnlyList<JobOccurrence> childOccurrences, CancellationToken cancellationToken)
    {
        foreach (var occurrence in childOccurrences ?? [])
        {
            var exists = await dbContext.JobOccurrences.AsNoTracking()
                .AnyAsync(x => x.OccurrenceId == occurrence.OccurrenceId || x.OccurrenceKey == occurrence.OccurrenceKey, cancellationToken)
                .ConfigureAwait(false);
            if (!exists)
            {
                dbContext.JobOccurrences.Add(ToEntity(occurrence, this.Options.Serializer));
            }
        }
    }

    private static IReadOnlyList<JobBatchOccurrence> DeduplicateMemberships(IReadOnlyList<JobBatchOccurrence> memberships)
    {
        return memberships?
            .GroupBy(x => new { x.BatchId, x.OccurrenceId })
            .Select(x => x.OrderBy(y => y.Sequence ?? int.MaxValue).First())
            .ToArray() ?? [];
    }

    private DateTimeOffset ResolveNowUtc() => this.timeProvider.GetUtcNow();

    private static JobRuntimeState ToModel(JobRuntimeStateEntity row)
        => new()
        {
            JobName = row.JobName,
            Enabled = row.Enabled,
            Paused = row.Paused,
            CreatedDate = row.CreatedDate,
            UpdatedDate = row.UpdatedDate,
        };

    private static JobRuntimeStateEntity ToEntity(JobRuntimeState model)
        => new()
        {
            JobName = model.JobName,
            Enabled = model.Enabled,
            Paused = model.Paused,
            CreatedDate = model.CreatedDate,
            UpdatedDate = model.UpdatedDate,
        };

    private static JobTriggerRuntimeState ToModel(JobTriggerRuntimeStateEntity row)
        => new(
            row.ActivatedUtc,
            row.DueUtc,
            row.LastMaterializedScheduledUtc,
            row.HasMaterializedOccurrence,
            row.Enabled,
            row.Paused,
            row.CreatedDate,
            row.UpdatedDate,
            row.LastAcceptedEventUtc,
            row.LastAcceptedEventId);

    private static JobTriggerRuntimeStateEntity ToEntity(string jobName, string triggerName, JobTriggerRuntimeState model)
        => new()
        {
            JobName = jobName,
            TriggerName = triggerName,
            ActivatedUtc = model.ActivatedUtc,
            DueUtc = model.DueUtc,
            LastMaterializedScheduledUtc = model.LastMaterializedScheduledUtc,
            HasMaterializedOccurrence = model.HasMaterializedOccurrence,
            Enabled = model.Enabled,
            Paused = model.Paused,
            CreatedDate = model.CreatedDate,
            UpdatedDate = model.UpdatedDate,
            LastAcceptedEventUtc = model.LastAcceptedEventUtc,
            LastAcceptedEventId = model.LastAcceptedEventId,
        };

    private static JobOccurrenceEntity ToEntity(JobOccurrence model, ISerializer serializer)
        => new()
        {
            OccurrenceId = model.OccurrenceId,
            OccurrenceKey = model.OccurrenceKey,
            JobName = model.JobName,
            TriggerName = model.TriggerName,
            TriggerType = model.TriggerType,
            Status = model.Status,
            DueUtc = model.DueUtc,
            ScheduledUtc = model.ScheduledUtc,
            SerializedData = Serialize(serializer, model.Data),
            DataType = model.DataType?.AssemblyQualifiedName ?? typeof(Unit).AssemblyQualifiedName,
            SerializedProperties = Serialize(serializer, model.Properties),
            CorrelationId = model.CorrelationId,
            CausationId = model.CausationId,
            IdempotencyKey = model.IdempotencyKey,
            ResumeStatus = model.ResumeStatus,
            BlockedReason = model.BlockedReason,
            CreatedDate = model.CreatedDate,
            UpdatedDate = model.UpdatedDate,
        };

    private static JobOccurrence ToModel(JobOccurrenceEntity row, ISerializer serializer)
    {
        var dataType = row.DataType is null ? typeof(Unit) : Type.GetType(row.DataType, throwOnError: true);
        return new JobOccurrence
        {
            OccurrenceId = row.OccurrenceId,
            OccurrenceKey = row.OccurrenceKey,
            JobName = row.JobName,
            TriggerName = row.TriggerName,
            TriggerType = row.TriggerType,
            Status = row.Status,
            DueUtc = row.DueUtc,
            ScheduledUtc = row.ScheduledUtc,
            Data = Deserialize(serializer, row.SerializedData, dataType) ?? Unit.Value,
            DataType = dataType,
            Properties = Deserialize(serializer, row.SerializedProperties, typeof(PropertyBag)) as PropertyBag ?? new PropertyBag(),
            CorrelationId = row.CorrelationId,
            CausationId = row.CausationId,
            IdempotencyKey = row.IdempotencyKey,
            ResumeStatus = row.ResumeStatus,
            BlockedReason = row.BlockedReason,
            CreatedDate = row.CreatedDate,
            UpdatedDate = row.UpdatedDate,
        };
    }

    private static void Apply(JobOccurrenceEntity row, JobOccurrence model, ISerializer serializer)
    {
        row.OccurrenceKey = model.OccurrenceKey;
        row.JobName = model.JobName;
        row.TriggerName = model.TriggerName;
        row.TriggerType = model.TriggerType;
        row.Status = model.Status;
        row.DueUtc = model.DueUtc;
        row.ScheduledUtc = model.ScheduledUtc;
        row.SerializedData = Serialize(serializer, model.Data);
        row.DataType = model.DataType?.AssemblyQualifiedName ?? typeof(Unit).AssemblyQualifiedName;
        row.SerializedProperties = Serialize(serializer, model.Properties);
        row.CorrelationId = model.CorrelationId;
        row.CausationId = model.CausationId;
        row.IdempotencyKey = model.IdempotencyKey;
        row.ResumeStatus = model.ResumeStatus;
        row.BlockedReason = model.BlockedReason;
        row.CreatedDate = model.CreatedDate;
        row.UpdatedDate = model.UpdatedDate;
        row.AdvanceConcurrencyVersion();
    }

    private static JobExecutionEntity ToEntity(JobExecution model)
        => new()
        {
            ExecutionId = model.ExecutionId,
            OccurrenceId = model.OccurrenceId,
            JobName = model.JobName,
            TriggerName = model.TriggerName,
            AttemptNumber = model.AttemptNumber,
            Status = model.Status,
            SchedulerInstanceId = model.SchedulerInstanceId,
            StartedUtc = model.StartedUtc,
            CompletedUtc = model.CompletedUtc,
            Message = model.Message,
            CreatedDate = model.CreatedDate,
            UpdatedDate = model.UpdatedDate,
        };

    private static JobExecution ToModel(JobExecutionEntity row)
        => new()
        {
            ExecutionId = row.ExecutionId,
            OccurrenceId = row.OccurrenceId,
            JobName = row.JobName,
            TriggerName = row.TriggerName,
            AttemptNumber = row.AttemptNumber,
            Status = row.Status,
            SchedulerInstanceId = row.SchedulerInstanceId,
            StartedUtc = row.StartedUtc,
            CompletedUtc = row.CompletedUtc,
            Message = row.Message,
            CreatedDate = row.CreatedDate,
            UpdatedDate = row.UpdatedDate,
        };

    private static void Apply(JobExecutionEntity row, JobExecution model)
    {
        row.OccurrenceId = model.OccurrenceId;
        row.JobName = model.JobName;
        row.TriggerName = model.TriggerName;
        row.AttemptNumber = model.AttemptNumber;
        row.Status = model.Status;
        row.SchedulerInstanceId = model.SchedulerInstanceId;
        row.StartedUtc = model.StartedUtc;
        row.CompletedUtc = model.CompletedUtc;
        row.Message = model.Message;
        row.CreatedDate = model.CreatedDate;
        row.UpdatedDate = model.UpdatedDate;
        row.AdvanceConcurrencyVersion();
    }

    private static JobOccurrenceDependencyEntity ToEntity(JobOccurrenceDependency model, ISerializer serializer)
        => new()
        {
            DependencyId = model.DependencyId,
            DependentOccurrenceId = model.DependentOccurrenceId,
            PrerequisiteOccurrenceId = model.PrerequisiteOccurrenceId,
            RequiredStatuses = SerializeStatuses(model.RequiredStatuses),
            Status = model.Status,
            FailurePolicy = model.FailurePolicy,
            Reason = model.Reason,
            SerializedProperties = Serialize(serializer, model.Properties),
            CreatedDate = model.CreatedDate,
            UpdatedDate = model.UpdatedDate,
        };

    private static JobOccurrenceDependency ToModel(JobOccurrenceDependencyEntity row, ISerializer serializer)
        => new()
        {
            DependencyId = row.DependencyId,
            DependentOccurrenceId = row.DependentOccurrenceId,
            PrerequisiteOccurrenceId = row.PrerequisiteOccurrenceId,
            RequiredStatuses = DeserializeStatuses(row.RequiredStatuses),
            Status = row.Status,
            FailurePolicy = row.FailurePolicy,
            Reason = row.Reason,
            Properties = Deserialize(serializer, row.SerializedProperties, typeof(PropertyBag)) as PropertyBag ?? new PropertyBag(),
            CreatedDate = row.CreatedDate,
            UpdatedDate = row.UpdatedDate,
        };

    private static void Apply(JobOccurrenceDependencyEntity row, JobOccurrenceDependency model, ISerializer serializer)
    {
        row.DependentOccurrenceId = model.DependentOccurrenceId;
        row.PrerequisiteOccurrenceId = model.PrerequisiteOccurrenceId;
        row.RequiredStatuses = SerializeStatuses(model.RequiredStatuses);
        row.Status = model.Status;
        row.FailurePolicy = model.FailurePolicy;
        row.Reason = model.Reason;
        row.SerializedProperties = Serialize(serializer, model.Properties);
        row.CreatedDate = model.CreatedDate;
        row.UpdatedDate = model.UpdatedDate;
        row.AdvanceConcurrencyVersion();
    }

    private static JobBatchEntity ToEntity(JobBatch model, ISerializer serializer)
        => new()
        {
            BatchId = model.BatchId,
            ExternalBatchId = model.ExternalBatchId,
            Description = model.Description,
            Status = model.Status,
            CompletionPolicy = model.CompletionPolicy,
            SerializedProperties = Serialize(serializer, model.Properties),
            CorrelationId = model.CorrelationId,
            CausationId = model.CausationId,
            IdempotencyKey = model.IdempotencyKey,
            AcceptedCount = model.AcceptedCount,
            SucceededCount = model.SucceededCount,
            FailedCount = model.FailedCount,
            CancelledCount = model.CancelledCount,
            ArchivedCount = model.ArchivedCount,
            CancellationRequestedDate = model.CancellationRequestedDate,
            ArchivedDate = model.ArchivedDate,
            CompletedDate = model.CompletedDate,
            CreatedDate = model.CreatedDate,
            UpdatedDate = model.UpdatedDate,
        };

    private static JobBatch ToModel(JobBatchEntity row, ISerializer serializer)
        => new()
        {
            BatchId = row.BatchId,
            ExternalBatchId = row.ExternalBatchId,
            Description = row.Description,
            Status = row.Status,
            CompletionPolicy = row.CompletionPolicy,
            Properties = Deserialize(serializer, row.SerializedProperties, typeof(PropertyBag)) as PropertyBag ?? new PropertyBag(),
            CorrelationId = row.CorrelationId,
            CausationId = row.CausationId,
            IdempotencyKey = row.IdempotencyKey,
            AcceptedCount = row.AcceptedCount,
            SucceededCount = row.SucceededCount,
            FailedCount = row.FailedCount,
            CancelledCount = row.CancelledCount,
            ArchivedCount = row.ArchivedCount,
            CancellationRequestedDate = row.CancellationRequestedDate,
            ArchivedDate = row.ArchivedDate,
            CompletedDate = row.CompletedDate,
            CreatedDate = row.CreatedDate,
            UpdatedDate = row.UpdatedDate,
        };

    private static void Apply(JobBatchEntity row, JobBatch model, ISerializer serializer)
    {
        row.ExternalBatchId = model.ExternalBatchId;
        row.Description = model.Description;
        row.Status = model.Status;
        row.CompletionPolicy = model.CompletionPolicy;
        row.SerializedProperties = Serialize(serializer, model.Properties);
        row.CorrelationId = model.CorrelationId;
        row.CausationId = model.CausationId;
        row.IdempotencyKey = model.IdempotencyKey;
        row.AcceptedCount = model.AcceptedCount;
        row.SucceededCount = model.SucceededCount;
        row.FailedCount = model.FailedCount;
        row.CancelledCount = model.CancelledCount;
        row.ArchivedCount = model.ArchivedCount;
        row.CancellationRequestedDate = model.CancellationRequestedDate;
        row.ArchivedDate = model.ArchivedDate;
        row.CompletedDate = model.CompletedDate;
        row.CreatedDate = model.CreatedDate;
        row.UpdatedDate = model.UpdatedDate;
        row.AdvanceConcurrencyVersion();
    }

    private static JobBatchOccurrenceEntity ToEntity(JobBatchOccurrence model)
        => new()
        {
            BatchId = model.BatchId,
            OccurrenceId = model.OccurrenceId,
            ChildStatus = model.ChildStatus,
            Sequence = model.Sequence,
            CreatedDate = model.CreatedDate,
            UpdatedDate = model.UpdatedDate,
        };

    private static void Apply(JobBatchOccurrenceEntity row, JobBatchOccurrence model)
    {
        row.ChildStatus = model.ChildStatus;
        row.Sequence = model.Sequence;
        row.CreatedDate = model.CreatedDate;
        row.UpdatedDate = model.UpdatedDate;
        row.AdvanceConcurrencyVersion();
    }

    private static JobBatchOccurrence ToModel(JobBatchOccurrenceEntity row)
        => new()
        {
            BatchId = row.BatchId,
            OccurrenceId = row.OccurrenceId,
            ChildStatus = row.ChildStatus,
            Sequence = row.Sequence,
            CreatedDate = row.CreatedDate,
            UpdatedDate = row.UpdatedDate,
        };

    private static JobLeaseEntity ToEntity(JobLeaseRecord model)
        => new()
        {
            OccurrenceId = model.OccurrenceId,
            SchedulerInstanceId = model.SchedulerInstanceId,
            OwnershipToken = model.OwnershipToken,
            AcquiredUtc = model.AcquiredUtc,
            RenewedUtc = model.RenewedUtc,
            ExpiresUtc = model.ExpiresUtc,
            RenewalCount = model.RenewalCount,
            CreatedDate = model.CreatedDate,
            UpdatedDate = model.UpdatedDate,
        };

    private static JobLeaseRecord ToModel(JobLeaseEntity row)
        => new()
        {
            OccurrenceId = row.OccurrenceId,
            SchedulerInstanceId = row.SchedulerInstanceId,
            OwnershipToken = row.OwnershipToken,
            AcquiredUtc = row.AcquiredUtc,
            RenewedUtc = row.RenewedUtc,
            ExpiresUtc = row.ExpiresUtc,
            RenewalCount = row.RenewalCount,
            CreatedDate = row.CreatedDate,
            UpdatedDate = row.UpdatedDate,
        };

    private static void Apply(JobLeaseEntity row, JobLeaseRecord model)
    {
        row.SchedulerInstanceId = model.SchedulerInstanceId;
        row.OwnershipToken = model.OwnershipToken;
        row.AcquiredUtc = model.AcquiredUtc;
        row.RenewedUtc = model.RenewedUtc;
        row.ExpiresUtc = model.ExpiresUtc;
        row.RenewalCount = model.RenewalCount;
        row.CreatedDate = model.CreatedDate;
        row.UpdatedDate = model.UpdatedDate;
        row.AdvanceConcurrencyVersion();
    }

    private static JobExecutionHistoryEntity ToEntity(JobExecutionHistoryEntry model, ISerializer serializer)
        => new()
        {
            HistoryId = model.HistoryId,
            OccurrenceId = model.OccurrenceId,
            ExecutionId = model.ExecutionId,
            JobName = model.JobName,
            TriggerName = model.TriggerName,
            SchedulerInstanceId = model.SchedulerInstanceId,
            EventName = model.EventName,
            OccurrenceStatus = model.OccurrenceStatus,
            ExecutionStatus = model.ExecutionStatus,
            Message = model.Message,
            RecordedAt = model.RecordedAt,
            RecordedBy = model.RecordedBy,
            SerializedProperties = Serialize(serializer, model.Properties),
        };

    private static JobExecutionHistoryEntry ToModel(JobExecutionHistoryEntity row, ISerializer serializer)
        => new()
        {
            HistoryId = row.HistoryId,
            OccurrenceId = row.OccurrenceId,
            ExecutionId = row.ExecutionId,
            JobName = row.JobName,
            TriggerName = row.TriggerName,
            SchedulerInstanceId = row.SchedulerInstanceId,
            EventName = row.EventName,
            OccurrenceStatus = row.OccurrenceStatus,
            ExecutionStatus = row.ExecutionStatus,
            Message = row.Message,
            RecordedAt = row.RecordedAt,
            RecordedBy = row.RecordedBy,
            Properties = Deserialize(serializer, row.SerializedProperties, typeof(PropertyBag)) as PropertyBag ?? new PropertyBag(),
        };

    private static JobBatchHistoryEntity ToEntity(JobBatchHistoryEntry model, ISerializer serializer)
        => new()
        {
            HistoryId = model.HistoryId,
            BatchId = model.BatchId,
            ExternalBatchId = model.ExternalBatchId,
            EventName = model.EventName,
            BatchStatus = model.BatchStatus,
            Message = model.Message,
            SchedulerInstanceId = model.SchedulerInstanceId,
            SerializedProperties = Serialize(serializer, model.Properties),
            RecordedAt = model.RecordedAt,
        };

    private static JobBatchHistoryEntry ToModel(JobBatchHistoryEntity row, ISerializer serializer)
        => new()
        {
            HistoryId = row.HistoryId,
            BatchId = row.BatchId,
            ExternalBatchId = row.ExternalBatchId,
            EventName = row.EventName,
            BatchStatus = row.BatchStatus,
            Message = row.Message,
            SchedulerInstanceId = row.SchedulerInstanceId,
            Properties = Deserialize(serializer, row.SerializedProperties, typeof(PropertyBag)) as PropertyBag ?? new PropertyBag(),
            RecordedAt = row.RecordedAt,
        };

    private static JobAcceptedEventEntity ToEntity(JobAcceptedEvent model, ISerializer serializer)
        => new()
        {
            AcceptedEventId = model.AcceptedEventId,
            Source = model.Source,
            SerializedData = Serialize(serializer, model.Data),
            DataType = model.DataType?.AssemblyQualifiedName ?? typeof(object).AssemblyQualifiedName,
            IdempotencyKey = model.IdempotencyKey,
            SourceId = model.SourceId,
            CorrelationId = model.CorrelationId,
            SerializedProperties = Serialize(serializer, model.Properties),
            AcceptedUtc = model.AcceptedUtc,
        };

    private static JobAcceptedEvent ToModel(JobAcceptedEventEntity row, ISerializer serializer)
    {
        var dataType = row.DataType is null ? typeof(object) : Type.GetType(row.DataType, throwOnError: true);
        return new JobAcceptedEvent
        {
            AcceptedEventId = row.AcceptedEventId,
            Source = row.Source,
            Data = Deserialize(serializer, row.SerializedData, dataType),
            DataType = dataType,
            IdempotencyKey = row.IdempotencyKey,
            SourceId = row.SourceId,
            CorrelationId = row.CorrelationId,
            Properties = Deserialize(serializer, row.SerializedProperties, typeof(PropertyBag)) as PropertyBag ?? new PropertyBag(),
            AcceptedUtc = row.AcceptedUtc,
        };
    }

    private static string Serialize(ISerializer serializer, object value)
    {
        if (value is null)
        {
            return null;
        }

        using var stream = new MemoryStream();
        serializer.Serialize(value, stream);
        return Convert.ToBase64String(stream.ToArray());
    }

    private static object Deserialize(ISerializer serializer, string payload, Type type)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        using var stream = new MemoryStream(Convert.FromBase64String(payload), writable: false);
        return serializer.Deserialize(stream, type);
    }

    private static string SerializeStatuses(IReadOnlyList<JobOccurrenceStatus> statuses)
        => statuses is null || statuses.Count == 0 ? string.Empty : string.Join(',', statuses);

    private static IReadOnlyList<JobOccurrenceStatus> DeserializeStatuses(string statuses)
        => string.IsNullOrWhiteSpace(statuses)
            ? []
            : statuses.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Enum.Parse<JobOccurrenceStatus>)
                .ToArray();
}
