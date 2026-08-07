// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Persists pending entity change sets and configured direct mutations as EF Core ChangeHistory rows.
/// </summary>
/// <typeparam name="TEntity">The repository entity type.</typeparam>
/// <typeparam name="TContext">The EF Core context type.</typeparam>
/// <example>
/// <code>
/// services.AddEntityFrameworkRepository&lt;Customer, CustomerDbContext&gt;()
///     .WithBehavior&lt;RepositoryChangeHistoryBehavior&lt;Customer, CustomerDbContext&gt;&gt;();
/// </code>
/// </example>
public class RepositoryChangeHistoryBehavior<TEntity, TContext> : IGenericRepository<TEntity>
    where TEntity : class, IEntity
    where TContext : DbContext
{
    private const string RedactedValue = "\"***REDACTED***\"";
    private static readonly string[] SensitivePropertyNameParts = ["password", "secret", "token", "credential", "apikey", "api_key", "connectionstring"];
    private readonly ChangeHistoryOptions options;
    private readonly ICurrentUserAccessor currentUserAccessor;
    private readonly ISerializer serializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="RepositoryChangeHistoryBehavior{TEntity,TContext}" /> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="context">The EF Core context used by the decorated repository.</param>
    /// <param name="inner">The decorated repository.</param>
    /// <param name="options">The ChangeHistory options.</param>
    /// <param name="currentUserAccessor">The current-user accessor.</param>
    /// <param name="serializer">The value serializer.</param>
    public RepositoryChangeHistoryBehavior(
        ILoggerFactory loggerFactory,
        TContext context,
        IGenericRepository<TEntity> inner,
        ChangeHistoryOptions options = null,
        ICurrentUserAccessor currentUserAccessor = null,
        ISerializer serializer = null)
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(inner, nameof(inner));

        this.Logger = loggerFactory?.CreateLogger<RepositoryChangeHistoryBehavior<TEntity, TContext>>() ??
            NullLoggerFactory.Instance.CreateLogger<RepositoryChangeHistoryBehavior<TEntity, TContext>>();
        this.Context = context;
        this.Inner = inner;
        this.options = options ?? new ChangeHistoryOptions();
        this.currentUserAccessor = currentUserAccessor ?? new NullCurrentUserAccessor();
        this.serializer = serializer ?? new SystemTextJsonSerializer();
    }

    /// <summary>
    /// Gets the logger used for diagnostics.
    /// </summary>
    protected ILogger<RepositoryChangeHistoryBehavior<TEntity, TContext>> Logger { get; }

    /// <summary>
    /// Gets the EF Core context used for persistence.
    /// </summary>
    protected TContext Context { get; }

    /// <summary>
    /// Gets the decorated repository.
    /// </summary>
    protected IGenericRepository<TEntity> Inner { get; }

    /// <inheritdoc />
    public async Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var transaction = await this.BeginChangeHistoryTransactionAsync(cancellationToken).AnyContext();
        try
        {
            var result = await this.Inner.InsertAsync(entity, cancellationToken).AnyContext();
            var capture = await this.PrepareCaptureAsync(result ?? entity, false, true, cancellationToken).AnyContext();
            await this.Context.SaveChangesAsync(cancellationToken).AnyContext();
            capture.ConsumePendingEntityChanges();

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken).AnyContext();
            }

            return result;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TEntity>> InsertSetAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        var items = entities.SafeNull().Where(e => e is not null).ToList();
        if (items.Count == 0)
        {
            return [];
        }

        var transaction = await this.BeginChangeHistoryTransactionAsync(cancellationToken).AnyContext();
        try
        {
            var result = (await this.Inner.InsertSetAsync(items, cancellationToken).AnyContext())
                .SafeNull()
                .Where(e => e is not null)
                .ToList();
            var captures = new List<CapturePreparation>(result.Count);
            foreach (var entity in result)
            {
                captures.Add(await this.PrepareCaptureAsync(entity, false, true, cancellationToken).AnyContext());
            }

            await this.Context.SaveChangesAsync(cancellationToken).AnyContext();
            foreach (var capture in captures)
            {
                capture.ConsumePendingEntityChanges();
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken).AnyContext();
            }

            return result;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    /// <inheritdoc />
    public async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (ChangeHistoryCaptureScope.IsSuppressed)
        {
            return await this.Inner.UpdateAsync(entity, cancellationToken).AnyContext();
        }

        var capture = await this.PrepareCaptureAsync(entity, true, false, cancellationToken).AnyContext();
        var result = await this.Inner.UpdateAsync(entity, cancellationToken).AnyContext();
        capture.ConsumePendingEntityChanges();

        return result;
    }

    /// <inheritdoc />
    public async Task<(TEntity entity, RepositoryActionResult action)> UpsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var isCreate = await this.IsCreateAsync(entity, cancellationToken).AnyContext();
        if (isCreate)
        {
            var transaction = await this.BeginChangeHistoryTransactionAsync(cancellationToken).AnyContext();
            try
            {
                var result = await this.Inner.UpsertAsync(entity, cancellationToken).AnyContext();
                var createCapture = await this.PrepareCaptureAsync(result.entity ?? entity, false, true, cancellationToken).AnyContext();
                await this.Context.SaveChangesAsync(cancellationToken).AnyContext();
                createCapture.ConsumePendingEntityChanges();

                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken).AnyContext();
                }

                return result;
            }
            finally
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }

        var capture = await this.PrepareCaptureAsync(entity, true, false, cancellationToken).AnyContext();
        var updateResult = await this.Inner.UpsertAsync(entity, cancellationToken).AnyContext();
        capture.ConsumePendingEntityChanges();

        return updateResult;
    }

    /// <inheritdoc />
    public async Task<long> UpdateSetAsync(
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        => await this.CaptureUpdateSetAsync([], set, options, cancellationToken).AnyContext();

    /// <inheritdoc />
    public async Task<long> UpdateSetAsync(
        ISpecification<TEntity> specification,
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        => await this.CaptureUpdateSetAsync([specification], set, options, cancellationToken).AnyContext();

    /// <inheritdoc />
    public async Task<long> UpdateSetAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        => await this.CaptureUpdateSetAsync(specifications, set, options, cancellationToken).AnyContext();

    /// <inheritdoc />
    public async Task<RepositoryActionResult> DeleteAsync(object id, CancellationToken cancellationToken = default)
        => await this.Inner.DeleteAsync(id, cancellationToken).AnyContext();

    /// <inheritdoc />
    public async Task<RepositoryActionResult> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
        => await this.Inner.DeleteAsync(entity, cancellationToken).AnyContext();

    /// <inheritdoc />
    public async Task<long> DeleteSetAsync(IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
        => await this.Inner.DeleteSetAsync(options, cancellationToken).AnyContext();

    /// <inheritdoc />
    public async Task<long> DeleteSetAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        => await this.Inner.DeleteSetAsync(specification, options, cancellationToken).AnyContext();

    /// <inheritdoc />
    public async Task<long> DeleteSetAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        => await this.Inner.DeleteSetAsync(specifications, options, cancellationToken).AnyContext();

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default)
        => await this.Inner.ExistsAsync(id, cancellationToken).AnyContext();

    /// <inheritdoc />
    public async Task<IEnumerable<TEntity>> FindAllAsync(IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
        => await this.Inner.FindAllAsync(options, cancellationToken).AnyContext();

    /// <inheritdoc />
    public async Task<IEnumerable<TEntity>> FindAllAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        => await this.Inner.FindAllAsync(specification, options, cancellationToken).AnyContext();

    /// <inheritdoc />
    public async Task<IEnumerable<TEntity>> FindAllAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        => await this.Inner.FindAllAsync(specifications, options, cancellationToken).AnyContext();

    /// <inheritdoc />
    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        => await this.Inner.ProjectAllAsync(projection, options, cancellationToken).AnyContext();

    /// <inheritdoc />
    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        => await this.Inner.ProjectAllAsync(specification, projection, options, cancellationToken).AnyContext();

    /// <inheritdoc />
    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        IEnumerable<ISpecification<TEntity>> specifications,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        => await this.Inner.ProjectAllAsync(specifications, projection, options, cancellationToken).AnyContext();

    /// <inheritdoc />
    public async Task<TEntity> FindOneAsync(object id, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
        => await this.Inner.FindOneAsync(id, options, cancellationToken).AnyContext();

    /// <inheritdoc />
    public async Task<TEntity> FindOneAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        => await this.Inner.FindOneAsync(specification, options, cancellationToken).AnyContext();

    /// <inheritdoc />
    public async Task<TEntity> FindOneAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
        => await this.Inner.FindOneAsync(specifications, options, cancellationToken).AnyContext();

    /// <inheritdoc />
    public async Task<long> CountAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
        => await this.Inner.CountAsync(specification, cancellationToken).AnyContext();

    /// <inheritdoc />
    public async Task<long> CountAsync(IEnumerable<ISpecification<TEntity>> specifications, CancellationToken cancellationToken = default)
        => await this.Inner.CountAsync(specifications, cancellationToken).AnyContext();

    /// <inheritdoc />
    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
        => await this.Inner.CountAsync(cancellationToken).AnyContext();

    private async Task<CapturePreparation> PrepareCaptureAsync(
        TEntity entity,
        bool includeDirectMutations,
        bool includeCreate,
        CancellationToken cancellationToken)
    {
        if (entity is null)
        {
            return CapturePreparation.Empty;
        }

        var entityOptions = this.options.GetEntityOptions(typeof(TEntity));
        var strategy = entityOptions?.CaptureStrategy ?? this.options.DefaultCaptureStrategy;
        var pendingChangeSets = EntityChangeHistoryAccessor.GetPendingChangeSets(entity);
        var capturedPropertyNames = pendingChangeSets
            .SelectMany(s => s.PropertyChanges)
            .Select(c => c.PropertyName)
            .ToHashSet(StringComparer.Ordinal);

        var rows = new List<ChangeHistoryEntry>();
        rows.AddRange(pendingChangeSets.SelectMany(s => this.MapChangeSet(s, strategy, entityOptions)));

        if (includeCreate && entityOptions?.CaptureCreates == true)
        {
            rows.AddRange(this.CaptureCreate(entity, entityOptions, strategy));
        }

        if (includeDirectMutations && entityOptions?.CaptureDirectMutations == true && strategy != ChangeHistoryCaptureStrategy.EntityChangeOnly)
        {
            rows.AddRange(await this.CaptureDirectMutationsAsync(
                entity,
                entityOptions,
                strategy,
                capturedPropertyNames,
                cancellationToken).AnyContext());
        }

        if (rows.Count > 0)
        {
            this.Context.Set<ChangeHistoryEntry>().AddRange(rows);
        }

        return new CapturePreparation(entity, pendingChangeSets.Count > 0);
    }

    private async Task<bool> IsCreateAsync(TEntity entity, CancellationToken cancellationToken)
    {
        if (entity is null || IsDefaultId(entity.Id))
        {
            return true;
        }

        var existing = await this.Inner.FindOneAsync(
            entity.Id,
            this.CreateSnapshotFindOptions(this.options.GetEntityOptions(typeof(TEntity))),
            cancellationToken).AnyContext();

        return existing is null;
    }

    private async Task<long> CaptureUpdateSetAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options,
        CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(set, nameof(set));

        var entityOptions = this.options.GetEntityOptions(typeof(TEntity));
        if (entityOptions?.CaptureUpdateSet != true)
        {
            return await this.Inner.UpdateSetAsync(specifications, set, options, cancellationToken).AnyContext();
        }

        var updateBuilder = new EntityFrameworkEntityUpdateSet<TEntity>();
        set(updateBuilder);
        var assignments = updateBuilder.Assignments
            .Select(a => new BulkAssignment(a, GetPropertyName(a.PropertySelector)))
            .Where(a => !string.IsNullOrWhiteSpace(a.PropertyName))
            .Where(a => GetComparableProperties().Any(p => p.Name == a.PropertyName))
            .ToArray();
        if (assignments.Length == 0)
        {
            return await this.Inner.UpdateSetAsync(specifications, set, options, cancellationToken).AnyContext();
        }

        var findOptions = options is null
            ? this.CreateSnapshotFindOptions(entityOptions)
            : new FindOptions<TEntity>
            {
                NoTracking = true,
                Include = options.Include,
                Includes = options.Includes,
                Order = options.Order,
                Orders = options.Orders,
                Skip = options.Skip,
                Take = options.Take,
                Distinct = options.Distinct,
                Hierarchy = options.Hierarchy
            };
        var before = (await this.Inner.FindAllAsync(specifications, findOptions, cancellationToken).AnyContext()).ToArray();
        var maxAffectedRows = entityOptions.UpdateSetMaxAffectedRows ?? this.options.DefaultUpdateSetMaxAffectedRows;
        if (before.Length > maxAffectedRows)
        {
            var message = $"ChangeHistory bulk capture for {typeof(TEntity).Name} matched {before.Length} entities, exceeding the limit of {maxAffectedRows}.";
            if (entityOptions.UpdateSetMode == ChangeHistoryCaptureMode.Required)
            {
                throw new InvalidOperationException(message);
            }

            this.Logger.LogWarning("{LogKey} skipped change history bulk capture because matched rows exceeded limit (type={EntityType}, count={Count}, limit={Limit})", BridgingIT.DevKit.Common.Constants.LogKey, typeof(TEntity).Name, before.Length, maxAffectedRows);

            var summaryBulkOperationId = GuidGenerator.CreateSequential();
            var transaction = await this.BeginChangeHistoryTransactionAsync(cancellationToken).AnyContext();
            try
            {
                var affectedRows = await this.Inner.UpdateSetAsync(specifications, set, options, cancellationToken).AnyContext();
                this.Context.Set<ChangeHistoryEntry>().Add(this.CreateBulkSummaryEntry(summaryBulkOperationId, before.Length, message, entityOptions));
                await this.Context.SaveChangesAsync(cancellationToken).AnyContext();
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken).AnyContext();
                }

                return affectedRows;
            }
            finally
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }

        var updateTransaction = await this.BeginChangeHistoryTransactionAsync(cancellationToken).AnyContext();
        var affected = await this.Inner.UpdateSetAsync(specifications, set, options, cancellationToken).AnyContext();
        if (affected == 0 || before.Length == 0)
        {
            if (updateTransaction is not null)
            {
                await updateTransaction.CommitAsync(cancellationToken).AnyContext();
                await updateTransaction.DisposeAsync();
            }

            return affected;
        }

        var bulkOperationId = GuidGenerator.CreateSequential();
        var rows = new List<ChangeHistoryEntry>();
        foreach (var baseline in before)
        {
            var current = await this.Inner.FindOneAsync(
                baseline.Id,
                new FindOptions<TEntity> { NoTracking = true },
                cancellationToken).AnyContext();
            if (current is null)
            {
                continue;
            }

            rows.AddRange(this.CompareAssignedProperties(
                current,
                baseline,
                entityOptions,
                assignments,
                bulkOperationId,
                before.Length));
        }

        if (rows.Count > 0)
        {
            this.Context.Set<ChangeHistoryEntry>().AddRange(rows);
            await this.Context.SaveChangesAsync(cancellationToken).AnyContext();
        }

        if (updateTransaction is not null)
        {
            await updateTransaction.CommitAsync(cancellationToken).AnyContext();
            await updateTransaction.DisposeAsync();
        }

        return affected;
    }

    private async Task<IDbContextTransaction> BeginChangeHistoryTransactionAsync(CancellationToken cancellationToken)
    {
        if (this.Context.Database.CurrentTransaction is not null || this.Context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            return null;
        }

        return await this.Context.Database.BeginTransactionAsync(cancellationToken).AnyContext();
    }

    private IEnumerable<ChangeHistoryEntry> CaptureCreate(
        TEntity entity,
        ChangeHistoryEntityOptions entityOptions,
        ChangeHistoryCaptureStrategy strategy)
    {
        var changeSetId = GuidGenerator.CreateSequential();
        var sequence = 0;
        var rows = new List<ChangeHistoryEntry>();

        foreach (var property in GetComparableProperties())
        {
            var policy = this.GetValuePolicy(entityOptions, property.Name);
            if (policy == ChangeHistoryValuePolicy.Exclude)
            {
                continue;
            }

            var newValue = property.GetValue(entity);
            if (newValue is null)
            {
                continue;
            }

            rows.Add(this.CreateEntry(
                changeSetId,
                sequence++,
                entity,
                property.Name,
                null,
                newValue,
                property.PropertyType,
                strategy,
                ChangeHistoryCaptureSource.Create,
                policy,
                ChangeHistoryOperation.Create,
                isRestoreable: false));
        }

        rows.AddRange(this.CaptureConfiguredPaths(
            entity,
            null,
            entityOptions,
            changeSetId,
            rows.Count,
            strategy,
            ChangeHistoryCaptureSource.Create,
            ChangeHistoryOperation.Create,
            isRestoreable: false));

        return rows;
    }

    private async Task<IEnumerable<ChangeHistoryEntry>> CaptureDirectMutationsAsync(
        TEntity entity,
        ChangeHistoryEntityOptions entityOptions,
        ChangeHistoryCaptureStrategy strategy,
        ISet<string> alreadyCapturedProperties,
        CancellationToken cancellationToken)
    {
        try
        {
            return strategy switch
            {
                ChangeHistoryCaptureStrategy.RepositorySnapshot => await this.CaptureRepositorySnapshotAsync(
                    entity,
                    entityOptions,
                    alreadyCapturedProperties,
                    cancellationToken).AnyContext(),
                ChangeHistoryCaptureStrategy.EfChangeTracker => this.CaptureEfChangeTracker(
                    entity,
                    entityOptions,
                    alreadyCapturedProperties),
                _ => []
            };
        }
        catch (Exception exception) when (entityOptions.DirectMutationMode != ChangeHistoryCaptureMode.Required)
        {
            this.Logger.LogWarning(exception, "{LogKey} skipped change history direct mutation capture (type={EntityType}, id={EntityId}, strategy={CaptureStrategy})", BridgingIT.DevKit.Common.Constants.LogKey, typeof(TEntity).Name, entity.Id, strategy);

            return [this.CreateDiagnosticEntry(
                entity,
                strategy,
                strategy == ChangeHistoryCaptureStrategy.EfChangeTracker ? ChangeHistoryCaptureSource.EfChangeTracker : ChangeHistoryCaptureSource.RepositorySnapshot,
                ChangeHistoryCaptureStatus.Failed,
                exception.Message)];
        }
    }

    private async Task<IEnumerable<ChangeHistoryEntry>> CaptureRepositorySnapshotAsync(
        TEntity entity,
        ChangeHistoryEntityOptions entityOptions,
        ISet<string> alreadyCapturedProperties,
        CancellationToken cancellationToken)
    {
        var baseline = await this.Inner.FindOneAsync(
            entity.Id,
            this.CreateSnapshotFindOptions(entityOptions),
            cancellationToken).AnyContext();
        if (baseline is null)
        {
            var message = $"No persisted baseline exists for {typeof(TEntity).Name} ({entity.Id}).";
            if (entityOptions.DirectMutationMode == ChangeHistoryCaptureMode.Required)
            {
                throw new InvalidOperationException(message);
            }

            return [this.CreateDiagnosticEntry(
                entity,
                ChangeHistoryCaptureStrategy.RepositorySnapshot,
                ChangeHistoryCaptureSource.RepositorySnapshot,
                ChangeHistoryCaptureStatus.Skipped,
                message)];
        }

        return this.CompareScalarProperties(
            entity,
            baseline,
            entityOptions,
            ChangeHistoryCaptureStrategy.RepositorySnapshot,
            ChangeHistoryCaptureSource.RepositorySnapshot,
            alreadyCapturedProperties);
    }

    private IEnumerable<ChangeHistoryEntry> CaptureEfChangeTracker(
        TEntity entity,
        ChangeHistoryEntityOptions entityOptions,
        ISet<string> alreadyCapturedProperties)
    {
        this.Context.ChangeTracker.DetectChanges();

        var entry = this.Context.ChangeTracker.Entries<TEntity>()
            .FirstOrDefault(e => Equals(e.Entity.Id, entity.Id));
        if (entry is null)
        {
            if (entityOptions.DirectMutationMode == ChangeHistoryCaptureMode.Required)
            {
                throw new InvalidOperationException($"No tracked EF Core entry exists for {typeof(TEntity).Name} ({entity.Id}).");
            }

            return [];
        }

        var sequence = 0;
        var changeSetId = GuidGenerator.CreateSequential();
        var rows = entry.Properties
            .Where(p => p.IsModified && !alreadyCapturedProperties.Contains(p.Metadata.Name))
            .Where(p => this.GetValuePolicy(entityOptions, p.Metadata.Name) != ChangeHistoryValuePolicy.Exclude)
            .Where(p => !Equals(p.OriginalValue, p.CurrentValue))
            .Select(p => this.CreateEntry(
                changeSetId,
                sequence++,
                entity,
                p.Metadata.Name,
                p.OriginalValue,
                p.CurrentValue,
                p.Metadata.ClrType,
                ChangeHistoryCaptureStrategy.EfChangeTracker,
                ChangeHistoryCaptureSource.EfChangeTracker,
                this.GetValuePolicy(entityOptions, p.Metadata.Name)))
            .ToList();

        rows.AddRange(this.CaptureEfChangeTrackerPaths(
            entity,
            entry,
            entityOptions,
            alreadyCapturedProperties,
            changeSetId,
            ref sequence));

        return rows;
    }

    private IEnumerable<ChangeHistoryEntry> CaptureEfChangeTrackerPaths(
        TEntity entity,
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<TEntity> entry,
        ChangeHistoryEntityOptions entityOptions,
        ISet<string> alreadyCapturedProperties,
        Guid changeSetId,
        ref int sequence)
    {
        if (entityOptions?.CapturePaths.Count is not > 0)
        {
            return [];
        }

        var baseline = (TEntity)entry.OriginalValues.ToObject();

        this.PopulateTrackedPathBaselines(entity, baseline, entityOptions);

        var rows = this.CaptureConfiguredPaths(
            entity,
            baseline,
            entityOptions,
            changeSetId,
            sequence,
            ChangeHistoryCaptureStrategy.EfChangeTracker,
            ChangeHistoryCaptureSource.EfChangeTracker,
            ChangeHistoryOperation.Update,
            isRestoreable: true)
            .Where(row => !alreadyCapturedProperties.Contains(row.PropertyName))
            .ToArray();

        foreach (var row in rows)
        {
            row.ChangeSetSequence = sequence++;
        }

        return rows;
    }

    private void PopulateTrackedPathBaselines(TEntity entity, TEntity baseline, ChangeHistoryEntityOptions entityOptions)
    {
        foreach (var path in entityOptions.CapturePaths)
        {
            if (path.Kind == ChangeHistoryCapturePathKind.Owned)
            {
                var currentOwned = GetValueByPath(entity, path.Path);
                var ownedEntry = currentOwned is null
                    ? null
                    : this.Context.ChangeTracker.Entries().FirstOrDefault(e => ReferenceEquals(e.Entity, currentOwned));
                if (ownedEntry is not null && ownedEntry.State != EntityState.Added)
                {
                    SetValueByPath(baseline, path.Path, ownedEntry.OriginalValues.ToObject());
                }

                continue;
            }

            if (path.Kind != ChangeHistoryCapturePathKind.Collection || path.CollectionItemType is null)
            {
                continue;
            }

            var baselineItems = CreateList(path.CollectionItemType);
            foreach (var itemEntry in this.Context.ChangeTracker.Entries()
                         .Where(e => path.CollectionItemType.IsInstanceOfType(e.Entity))
                         .Where(e => e.State != EntityState.Added))
            {
                baselineItems.Add(itemEntry.OriginalValues.ToObject());
            }

            SetValueByPath(baseline, path.Path, baselineItems);
        }
    }

    private IEnumerable<ChangeHistoryEntry> CompareScalarProperties(
        TEntity current,
        TEntity baseline,
        ChangeHistoryEntityOptions entityOptions,
        ChangeHistoryCaptureStrategy strategy,
        ChangeHistoryCaptureSource source,
        ISet<string> alreadyCapturedProperties)
    {
        var changeSetId = GuidGenerator.CreateSequential();
        var sequence = 0;
        var rows = new List<ChangeHistoryEntry>();

        foreach (var property in GetComparableProperties())
        {
            if (alreadyCapturedProperties.Contains(property.Name))
            {
                continue;
            }

            var policy = this.GetValuePolicy(entityOptions, property.Name);
            if (policy == ChangeHistoryValuePolicy.Exclude)
            {
                continue;
            }

            var oldValue = property.GetValue(baseline);
            var newValue = property.GetValue(current);
            if (Equals(oldValue, newValue))
            {
                continue;
            }

            rows.Add(this.CreateEntry(changeSetId, sequence++, current, property.Name, oldValue, newValue, property.PropertyType, strategy, source, policy));
        }

        rows.AddRange(this.CaptureConfiguredPaths(
            current,
            baseline,
            entityOptions,
            changeSetId,
            sequence,
            strategy,
            source,
            ChangeHistoryOperation.Update,
            isRestoreable: true));

        return rows;
    }

    private IEnumerable<ChangeHistoryEntry> CaptureConfiguredPaths(
        TEntity current,
        TEntity baseline,
        ChangeHistoryEntityOptions entityOptions,
        Guid changeSetId,
        int sequenceStart,
        ChangeHistoryCaptureStrategy strategy,
        ChangeHistoryCaptureSource source,
        ChangeHistoryOperation operation,
        bool isRestoreable)
    {
        if (entityOptions?.CapturePaths.Count > 0)
        {
            var sequence = sequenceStart;
            var rows = new List<ChangeHistoryEntry>();
            foreach (var path in entityOptions.CapturePaths)
            {
                if (path.Kind == ChangeHistoryCapturePathKind.Owned)
                {
                    rows.AddRange(this.CaptureOwnedPath(current, baseline, path, entityOptions, changeSetId, ref sequence, strategy, source, operation, isRestoreable));
                }

                if (path.Kind == ChangeHistoryCapturePathKind.Collection)
                {
                    rows.AddRange(this.CaptureCollectionPath(current, baseline, path, entityOptions, changeSetId, ref sequence, strategy, source, operation, isRestoreable));
                }

                if (path.Kind == ChangeHistoryCapturePathKind.Graph)
                {
                    rows.AddRange(this.CaptureGraphPath(current, baseline, path, entityOptions, changeSetId, ref sequence, strategy, source, operation, isRestoreable));
                }
            }

            return rows;
        }

        return [];
    }

    private IEnumerable<ChangeHistoryEntry> CaptureOwnedPath(
        TEntity current,
        TEntity baseline,
        ChangeHistoryCapturePathOptions path,
        ChangeHistoryEntityOptions entityOptions,
        Guid changeSetId,
        ref int sequence,
        ChangeHistoryCaptureStrategy strategy,
        ChangeHistoryCaptureSource source,
        ChangeHistoryOperation operation,
        bool isRestoreable)
    {
        var currentValue = GetValueByPath(current, path.Path);
        var baselineValue = baseline is null ? null : GetValueByPath(baseline, path.Path);
        if (currentValue is null && baselineValue is null)
        {
            return [];
        }

        var valueType = (currentValue ?? baselineValue).GetType();
        var rows = new List<ChangeHistoryEntry>();
        foreach (var property in GetComparableProperties(valueType))
        {
            var propertyPath = $"{path.Path}.{property.Name}";
            var policy = this.GetValuePolicy(entityOptions, propertyPath);
            if (policy == ChangeHistoryValuePolicy.Exclude)
            {
                continue;
            }

            var oldValue = baselineValue is null ? null : property.GetValue(baselineValue);
            var newValue = currentValue is null ? null : property.GetValue(currentValue);
            if (baseline is not null && Equals(oldValue, newValue))
            {
                continue;
            }

            if (baseline is null && newValue is null)
            {
                continue;
            }

            rows.Add(this.CreateEntry(
                changeSetId,
                sequence++,
                current,
                propertyPath,
                oldValue,
                newValue,
                property.PropertyType,
                strategy,
                source,
                policy,
                operation,
                isRestoreable: isRestoreable,
                propertyPath: propertyPath,
                pathKind: ChangeHistoryCapturePathKind.Owned.ToString(),
                restorePlanName: path.RestorePlanName));
        }

        return rows;
    }

    private IEnumerable<ChangeHistoryEntry> CaptureGraphPath(
        TEntity current,
        TEntity baseline,
        ChangeHistoryCapturePathOptions path,
        ChangeHistoryEntityOptions entityOptions,
        Guid changeSetId,
        ref int sequence,
        ChangeHistoryCaptureStrategy strategy,
        ChangeHistoryCaptureSource source,
        ChangeHistoryOperation operation,
        bool isRestoreable)
    {
        var currentValue = GetValueByPath(current, path.Path);
        var baselineValue = baseline is null ? null : GetValueByPath(baseline, path.Path);

        return this.CaptureGraphNode(
            current,
            currentValue,
            baselineValue,
            path,
            path.Path,
            path.Path,
            entityOptions,
            changeSetId,
            ref sequence,
            strategy,
            source,
            operation,
            isRestoreable,
            depth: 0);
    }

    private IEnumerable<ChangeHistoryEntry> CaptureGraphNode(
        TEntity entity,
        object currentValue,
        object baselineValue,
        ChangeHistoryCapturePathOptions graphOptions,
        string identityPath,
        string displayPath,
        ChangeHistoryEntityOptions entityOptions,
        Guid changeSetId,
        ref int sequence,
        ChangeHistoryCaptureStrategy strategy,
        ChangeHistoryCaptureSource source,
        ChangeHistoryOperation operation,
        bool isRestoreable,
        int depth)
    {
        if (depth > 8 || currentValue is null && baselineValue is null)
        {
            return [];
        }

        if (IsCollectionNode(currentValue ?? baselineValue))
        {
            return this.CaptureGraphCollection(
                entity,
                currentValue,
                baselineValue,
                graphOptions,
                identityPath,
                displayPath,
                entityOptions,
                changeSetId,
                ref sequence,
                strategy,
                source,
                operation,
                isRestoreable,
                depth);
        }

        var nodeType = (currentValue ?? baselineValue).GetType();
        var rows = new List<ChangeHistoryEntry>();
        foreach (var property in nodeType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .Where(p => p.CanRead)
                     .Where(p => p.GetIndexParameters().Length == 0))
        {
            var currentPropertyValue = currentValue is null ? null : property.GetValue(currentValue);
            var baselinePropertyValue = baselineValue is null ? null : property.GetValue(baselineValue);
            var propertyIdentityPath = $"{identityPath}.{property.Name}";
            var propertyDisplayPath = $"{displayPath}.{property.Name}";

            if (IsScalarType(property.PropertyType))
            {
                if (baselineValue is not null && Equals(baselinePropertyValue, currentPropertyValue))
                {
                    continue;
                }

                if (baselineValue is null && currentPropertyValue is null)
                {
                    continue;
                }

                var policy = this.GetValuePolicy(entityOptions, propertyDisplayPath);
                if (policy == ChangeHistoryValuePolicy.Exclude)
                {
                    continue;
                }

                rows.Add(this.CreateEntry(
                    changeSetId,
                    sequence++,
                    entity,
                    propertyDisplayPath,
                    baselinePropertyValue,
                    currentPropertyValue,
                    property.PropertyType,
                    strategy,
                    source,
                    policy,
                    operation == ChangeHistoryOperation.Create ? ChangeHistoryOperation.Create : ChangeHistoryOperation.GraphChanged,
                    isRestoreable: isRestoreable,
                    propertyPath: propertyDisplayPath,
                    pathKind: ChangeHistoryCapturePathKind.Graph.ToString()));

                continue;
            }

            rows.AddRange(this.CaptureGraphNode(
                entity,
                currentPropertyValue,
                baselinePropertyValue,
                graphOptions,
                propertyIdentityPath,
                propertyDisplayPath,
                entityOptions,
                changeSetId,
                ref sequence,
                strategy,
                source,
                operation,
                isRestoreable,
                depth + 1));
        }

        return rows;
    }

    private IEnumerable<ChangeHistoryEntry> CaptureGraphCollection(
        TEntity entity,
        object currentValue,
        object baselineValue,
        ChangeHistoryCapturePathOptions graphOptions,
        string identityPath,
        string displayPath,
        ChangeHistoryEntityOptions entityOptions,
        Guid changeSetId,
        ref int sequence,
        ChangeHistoryCaptureStrategy strategy,
        ChangeHistoryCaptureSource source,
        ChangeHistoryOperation operation,
        bool isRestoreable,
        int depth)
    {
        if (!graphOptions.GraphIdentities.TryGetValue(identityPath, out var identityOptions) || identityOptions.Identity is null)
        {
            var sampleItem = FirstCollectionItem(currentValue) ?? FirstCollectionItem(baselineValue);
            var inferredIdentity = sampleItem is null ? null : this.CreateEfPrimaryKeyIdentityAccessor(sampleItem.GetType());
            if (inferredIdentity is null && graphOptions.RequireExplicitGraphIdentities)
            {
                throw new InvalidOperationException($"ChangeHistory graph path '{identityPath}' requires an explicit identity rule to avoid ambiguous ownership/delete behavior.");
            }

            if (inferredIdentity is null)
            {
                return [];
            }

            identityOptions = new ChangeHistoryGraphIdentityOptions(identityPath, inferredIdentity);
        }

        var currentItems = ToIdentityMap(currentValue, identityOptions.Identity);
        var baselineItems = ToIdentityMap(baselineValue, identityOptions.Identity);
        var itemIds = currentItems.Keys.Concat(baselineItems.Keys).Distinct(StringComparer.Ordinal).OrderBy(id => id).ToArray();
        var rows = new List<ChangeHistoryEntry>();
        var membershipAction = GetCollectionMembershipAction(currentItems, baselineItems, baselineValue is null, operation);

        foreach (var itemId in itemIds)
        {
            currentItems.TryGetValue(itemId, out var currentItem);
            baselineItems.TryGetValue(itemId, out var baselineItem);
            var action = baselineItem is null
                ? membershipAction == "Replaced" ? "Replaced" : "Added"
                : currentItem is null
                    ? membershipAction == "Cleared" || membershipAction == "Replaced" ? membershipAction : "Removed"
                    : null;
            var itemDisplayPath = $"{displayPath}[{itemId}]";

            rows.AddRange(this.CaptureGraphNode(
                entity,
                currentItem,
                baselineItem,
                graphOptions,
                identityPath,
                itemDisplayPath,
                entityOptions,
                changeSetId,
                ref sequence,
                strategy,
                source,
                operation,
                isRestoreable,
                depth + 1)
                .Select(row =>
                {
                    row.CollectionItemId ??= itemId;
                    row.CollectionAction ??= action;
                    row.RestorePlanName = graphOptions.RestorePlanName;
                    return row;
                }));
        }

        return rows;
    }

    private IEnumerable<ChangeHistoryEntry> CaptureCollectionPath(
        TEntity current,
        TEntity baseline,
        ChangeHistoryCapturePathOptions path,
        ChangeHistoryEntityOptions entityOptions,
        Guid changeSetId,
        ref int sequence,
        ChangeHistoryCaptureStrategy strategy,
        ChangeHistoryCaptureSource source,
        ChangeHistoryOperation operation,
        bool isRestoreable)
    {
        var identityAccessor = path.CollectionItemIdentity ?? this.CreateEfPrimaryKeyIdentityAccessor(path.CollectionItemType);
        if (identityAccessor is null)
        {
            return [];
        }

        var currentItems = ToIdentityMap(GetValueByPath(current, path.Path), identityAccessor);
        var baselineItems = baseline is null
            ? new Dictionary<string, object>(StringComparer.Ordinal)
            : ToIdentityMap(GetValueByPath(baseline, path.Path), identityAccessor);
        var itemIds = currentItems.Keys.Concat(baselineItems.Keys).Distinct(StringComparer.Ordinal).OrderBy(id => id).ToArray();
        var rows = new List<ChangeHistoryEntry>();
        var membershipAction = GetCollectionMembershipAction(currentItems, baselineItems, baseline is null, operation);

        foreach (var itemId in itemIds)
        {
            currentItems.TryGetValue(itemId, out var currentItem);
            baselineItems.TryGetValue(itemId, out var baselineItem);
            var action = baselineItem is null
                ? membershipAction == "Replaced" ? "Replaced" : "Added"
                : currentItem is null
                    ? membershipAction == "Cleared" || membershipAction == "Replaced" ? membershipAction : "Removed"
                    : null;
            var itemType = (currentItem ?? baselineItem)?.GetType() ?? path.CollectionItemType;
            if (itemType is null)
            {
                continue;
            }

            if (baselineItem is null && currentItem is not null)
            {
                rows.Add(this.CreateEntry(
                    changeSetId,
                    sequence++,
                    current,
                    $"{path.Path}[{itemId}]",
                    null,
                    currentItem,
                    itemType,
                    strategy,
                    source,
                    this.GetValuePolicy(entityOptions, path.Path),
                    operation == ChangeHistoryOperation.Create ? ChangeHistoryOperation.Create : ChangeHistoryOperation.CollectionChanged,
                    isRestoreable: false,
                    propertyPath: $"{path.Path}[{itemId}]",
                    pathKind: ChangeHistoryCapturePathKind.Collection.ToString(),
                    collectionAction: action,
                    collectionItemId: itemId,
                    restorePlanName: path.RestorePlanName));
            }

            foreach (var property in GetComparableProperties(itemType))
            {
                var propertyPath = $"{path.Path}[{itemId}].{property.Name}";
                var policy = this.GetValuePolicy(entityOptions, propertyPath);
                if (policy == ChangeHistoryValuePolicy.Exclude)
                {
                    continue;
                }

                var oldValue = baselineItem is null ? null : property.GetValue(baselineItem);
                var newValue = currentItem is null ? null : property.GetValue(currentItem);
                if (action is null && Equals(oldValue, newValue))
                {
                    continue;
                }

                rows.Add(this.CreateEntry(
                    changeSetId,
                    sequence++,
                    current,
                    propertyPath,
                    oldValue,
                    newValue,
                    property.PropertyType,
                    strategy,
                    source,
                    policy,
                    operation == ChangeHistoryOperation.Create ? ChangeHistoryOperation.Create : ChangeHistoryOperation.CollectionChanged,
                    isRestoreable: isRestoreable,
                    propertyPath: propertyPath,
                    pathKind: ChangeHistoryCapturePathKind.Collection.ToString(),
                    collectionAction: action,
                    collectionItemId: itemId,
                    restorePlanName: path.RestorePlanName));
            }
        }

        return rows;
    }

    private IEnumerable<ChangeHistoryEntry> CompareAssignedProperties(
        TEntity current,
        TEntity baseline,
        ChangeHistoryEntityOptions entityOptions,
        IEnumerable<BulkAssignment> assignments,
        Guid bulkOperationId,
        int affectedEntityCount)
    {
        var changeSetId = GuidGenerator.CreateSequential();
        var sequence = 0;
        var rows = new List<ChangeHistoryEntry>();

        foreach (var assignment in assignments)
        {
            var property = typeof(TEntity).GetProperty(assignment.PropertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property is null)
            {
                continue;
            }

            var policy = this.GetValuePolicy(entityOptions, property.Name);
            if (policy == ChangeHistoryValuePolicy.Exclude)
            {
                continue;
            }

            var oldValue = property.GetValue(baseline);
            var newValue = property.GetValue(current);
            if (Equals(oldValue, newValue))
            {
                continue;
            }

            rows.Add(this.CreateEntry(
                changeSetId,
                sequence++,
                current,
                property.Name,
                oldValue,
                newValue,
                property.PropertyType,
                entityOptions?.CaptureStrategy ?? this.options.DefaultCaptureStrategy,
                ChangeHistoryCaptureSource.UpdateSet,
                policy,
                ChangeHistoryOperation.BulkUpdate,
                bulkOperationId,
                affectedEntityCount));
        }

        return rows;
    }

    private IEnumerable<ChangeHistoryEntry> MapChangeSet(
        EntityChangeSet changeSet,
        ChangeHistoryCaptureStrategy strategy,
        ChangeHistoryEntityOptions entityOptions)
    {
        return changeSet.PropertyChanges
            .Where(c => this.GetValuePolicy(entityOptions, c.PropertyName) != ChangeHistoryValuePolicy.Exclude)
            .Select(c => this.CreateEntry(
                changeSet.ChangeSetId,
                c.Sequence,
                changeSet,
                c,
                strategy,
                this.GetValuePolicy(entityOptions, c.PropertyName)))
            .ToArray();
    }

    private ChangeHistoryEntry CreateEntry(
        Guid changeSetId,
        int sequence,
        TEntity entity,
        string propertyName,
        object oldValue,
        object newValue,
        Type valueType,
        ChangeHistoryCaptureStrategy strategy,
        ChangeHistoryCaptureSource source,
        ChangeHistoryValuePolicy valuePolicy,
        ChangeHistoryOperation operation = ChangeHistoryOperation.Update,
        Guid? bulkOperationId = null,
        int? affectedEntityCount = null,
        bool isRestoreable = true,
        string propertyPath = null,
        string pathKind = null,
        string collectionAction = null,
        string collectionItemId = null,
        string restorePlanName = null,
        ChangeHistoryCaptureStatus captureStatus = ChangeHistoryCaptureStatus.Captured,
        string captureMessage = null)
    {
        var entityType = entity.GetType();
        var entityId = entity.Id;

        return this.CreateEntry(
            changeSetId,
            sequence,
            entityType.Name,
            entityType.AssemblyQualifiedName,
            entityId?.ToString(),
            entityId?.GetType().AssemblyQualifiedName,
            propertyName,
            propertyPath,
            oldValue,
            newValue,
            valueType?.AssemblyQualifiedName,
            strategy,
            source,
            valuePolicy,
            operation,
            bulkOperationId,
            affectedEntityCount,
            isRestoreable,
            pathKind,
            collectionAction,
            collectionItemId,
            restorePlanName,
            captureStatus,
            captureMessage);
    }

    private ChangeHistoryEntry CreateEntry(
        Guid changeSetId,
        int sequence,
        EntityChangeSet changeSet,
        EntityPropertyChange propertyChange,
        ChangeHistoryCaptureStrategy strategy,
        ChangeHistoryValuePolicy valuePolicy)
        => this.CreateEntry(
            changeSetId,
            sequence,
            changeSet.EntityType,
            changeSet.EntityClrType,
            changeSet.EntityId,
            changeSet.EntityIdType,
            propertyChange.PropertyName,
            propertyChange.PropertyPath,
            propertyChange.OldValue,
            propertyChange.NewValue,
            propertyChange.ValueClrType,
            strategy,
            ChangeHistoryCaptureSource.EntityChange,
            valuePolicy);

    private ChangeHistoryEntry CreateEntry(
        Guid changeSetId,
        int sequence,
        string entityType,
        string entityClrType,
        string entityId,
        string entityIdType,
        string propertyName,
        string propertyPath,
        object oldValue,
        object newValue,
        string valueClrType,
        ChangeHistoryCaptureStrategy strategy,
        ChangeHistoryCaptureSource source,
        ChangeHistoryValuePolicy valuePolicy,
        ChangeHistoryOperation operation = ChangeHistoryOperation.Update,
        Guid? bulkOperationId = null,
        int? affectedEntityCount = null,
        bool isRestoreable = true,
        string pathKind = null,
        string collectionAction = null,
        string collectionItemId = null,
        string restorePlanName = null,
        ChangeHistoryCaptureStatus captureStatus = ChangeHistoryCaptureStatus.Captured,
        string captureMessage = null)
    {
        var oldValueCapture = this.CaptureValue(oldValue, valuePolicy);
        var newValueCapture = this.CaptureValue(newValue, valuePolicy);
        var changedDate = DateTimeOffset.UtcNow;

        return new ChangeHistoryEntry
        {
            Id = GuidGenerator.CreateSequential(),
            ChangeSetId = changeSetId,
            ChangeSetSequence = sequence,
            EntityType = entityType,
            EntityClrType = entityClrType,
            EntityId = entityId,
            EntityIdType = entityIdType,
            PropertyName = propertyName,
            PropertyPath = propertyPath,
            PathKind = pathKind ?? "Scalar",
            CollectionAction = collectionAction,
            CollectionItemId = collectionItemId,
            ValueClrType = valueClrType,
            OldValue = oldValueCapture.StoredValue,
            NewValue = newValueCapture.StoredValue,
            OldValueHash = oldValueCapture.Hash,
            NewValueHash = newValueCapture.Hash,
            Operation = operation.ToString(),
            CaptureStrategy = strategy.ToString(),
            CaptureSource = source.ToString(),
            CaptureStatus = captureStatus.ToString(),
            CaptureMessage = captureMessage,
            BulkOperationId = bulkOperationId,
            AffectedEntityCount = affectedEntityCount,
            IsRestoreable = isRestoreable && oldValueCapture.IsRestoreable && newValueCapture.IsRestoreable,
            RestorePlanName = restorePlanName,
            ChangedByUserId = this.currentUserAccessor.UserId,
            ChangedByUserName = this.currentUserAccessor.UserName,
            ChangedByEmail = this.currentUserAccessor.Email,
            ChangedDate = changedDate,
            ChangedDateTicks = changedDate.UtcTicks,
            CorrelationId = Activity.Current?.TraceId.ToString(),
            FlowId = Activity.Current?.RootId,
            ModuleName = GetCurrentModuleName(),
            ActivityParentId = GetCurrentActivityParentId(),
            Properties = this.CreateActivityPropertiesJson()
        };
    }

    private ChangeHistoryEntry CreateDiagnosticEntry(
        TEntity entity,
        ChangeHistoryCaptureStrategy strategy,
        ChangeHistoryCaptureSource source,
        ChangeHistoryCaptureStatus status,
        string message)
        => this.CreateEntry(
            GuidGenerator.CreateSequential(),
            0,
            entity,
            "__ChangeHistoryCapture",
            null,
            null,
            typeof(string),
            strategy,
            source,
            ChangeHistoryValuePolicy.Include,
            ChangeHistoryOperation.Update,
            isRestoreable: false,
            captureStatus: status,
            captureMessage: message);

    private ChangeHistoryEntry CreateBulkSummaryEntry(
        Guid bulkOperationId,
        int affectedEntityCount,
        string message,
        ChangeHistoryEntityOptions entityOptions)
        => this.CreateEntry(
            GuidGenerator.CreateSequential(),
            0,
            typeof(TEntity).Name,
            typeof(TEntity).AssemblyQualifiedName,
            "*",
            typeof(string).AssemblyQualifiedName,
            "__ChangeHistoryBulkSummary",
            null,
            null,
            null,
            typeof(string).AssemblyQualifiedName,
            entityOptions?.CaptureStrategy ?? this.options.DefaultCaptureStrategy,
            ChangeHistoryCaptureSource.UpdateSet,
            ChangeHistoryValuePolicy.Include,
            ChangeHistoryOperation.BulkUpdate,
            bulkOperationId,
            affectedEntityCount,
            isRestoreable: false,
            captureStatus: ChangeHistoryCaptureStatus.Summary,
            captureMessage: message);

    private ChangeHistoryValuePolicy GetValuePolicy(ChangeHistoryEntityOptions entityOptions, string propertyName)
    {
        if (entityOptions?.PropertyPolicies.TryGetValue(propertyName, out var policy) == true)
        {
            return policy;
        }

        if (this.options.ProtectSensitivePropertyNames && IsSensitivePropertyName(propertyName))
        {
            return this.options.SensitiveValuePolicy;
        }

        return ChangeHistoryValuePolicy.Include;
    }

    private FindOptions<TEntity> CreateSnapshotFindOptions(ChangeHistoryEntityOptions entityOptions)
    {
        var result = new FindOptions<TEntity> { NoTracking = true };
        foreach (var includePath in entityOptions?.CapturePaths.SafeNull()
                     .Select(p => p.IncludePath)
                     .Where(p => !string.IsNullOrWhiteSpace(p))
                     .Distinct(StringComparer.Ordinal) ?? [])
        {
            result.AddInclude(new IncludeOption<TEntity>(includePath));
        }

        return result;
    }

    private ValueCapture CaptureValue(object value, ChangeHistoryValuePolicy policy)
    {
        if (value is null)
        {
            return new ValueCapture(null, null, true);
        }

        var serializedValue = this.serializer.SerializeToString(value);
        var isRestoreable = policy == ChangeHistoryValuePolicy.Include;
        var storedValue = policy switch
        {
            ChangeHistoryValuePolicy.Redact => RedactedValue,
            ChangeHistoryValuePolicy.HashOnly => null,
            _ => serializedValue
        };

        var oversizedResult = this.ApplyOversizedValuePolicy(storedValue, serializedValue);

        return new ValueCapture(oversizedResult.StoredValue, HashValue(serializedValue), isRestoreable && oversizedResult.IsRestoreable);
    }

    private string CreateActivityPropertiesJson()
    {
        var activityId = GetCurrentActivityParentId();
        if (string.IsNullOrWhiteSpace(activityId))
        {
            return null;
        }

        return this.serializer.SerializeToString(new Dictionary<string, string>
        {
            [ModuleConstants.ActivityParentIdKey] = activityId
        });
    }

    private Func<object, string> CreateEfPrimaryKeyIdentityAccessor(Type itemType)
    {
        if (itemType is null)
        {
            return null;
        }

        var entityType = this.Context.Model.FindEntityType(itemType);
        var keyProperty = entityType?.FindPrimaryKey()?.Properties.SingleOrDefault() ??
            entityType?.GetKeys()
                .Where(k => !k.IsPrimaryKey())
                .Select(k => k.Properties.SingleOrDefault())
                .SingleOrDefault(p => p is not null);
        if (keyProperty is null)
        {
            return null;
        }

        var property = itemType.GetProperty(keyProperty.Name, BindingFlags.Instance | BindingFlags.Public);
        if (property is null || !property.CanRead || !IsScalarType(property.PropertyType))
        {
            return null;
        }

        return item => property.GetValue(item)?.ToString();
    }

    private static string GetCollectionMembershipAction(
        IReadOnlyDictionary<string, object> currentItems,
        IReadOnlyDictionary<string, object> baselineItems,
        bool baselineMissing,
        ChangeHistoryOperation operation)
    {
        if (baselineMissing || operation == ChangeHistoryOperation.Create)
        {
            return "Added";
        }

        if (baselineItems.Count > 0 && currentItems.Count == 0)
        {
            return "Cleared";
        }

        if (baselineItems.Count > 0 &&
            currentItems.Count > 0 &&
            !baselineItems.Keys.Intersect(currentItems.Keys, StringComparer.Ordinal).Any())
        {
            return "Replaced";
        }

        return null;
    }

    private static IList CreateList(Type itemType)
        => (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));

    private ValuePolicyResult ApplyOversizedValuePolicy(string storedValue, string originalSerializedValue)
    {
        if (storedValue is null || this.options.MaxStoredValueLength is null || storedValue.Length <= this.options.MaxStoredValueLength.Value)
        {
            return new ValuePolicyResult(storedValue, true);
        }

        return this.options.OversizedValuePolicy switch
        {
            ChangeHistoryOversizedValuePolicy.Include => new ValuePolicyResult(storedValue, true),
            ChangeHistoryOversizedValuePolicy.Truncate => new ValuePolicyResult(storedValue[..this.options.MaxStoredValueLength.Value], false),
            ChangeHistoryOversizedValuePolicy.HashOnly => new ValuePolicyResult(null, false),
            ChangeHistoryOversizedValuePolicy.Reject => throw new InvalidOperationException($"ChangeHistory value length {originalSerializedValue.Length} exceeds the configured limit of {this.options.MaxStoredValueLength.Value}."),
            _ => new ValuePolicyResult(storedValue, true)
        };
    }

    private static string HashValue(string value)
    {
        if (value is null)
        {
            return null;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        return Convert.ToHexString(bytes);
    }

    private static IEnumerable<PropertyInfo> GetComparableProperties()
        => typeof(TEntity).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanRead)
            .Where(p => p.GetIndexParameters().Length == 0)
            .Where(p => p.Name != nameof(IEntity.Id))
            .Where(p => IsScalarType(p.PropertyType));

    private static IEnumerable<PropertyInfo> GetComparableProperties(Type type)
        => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanRead)
            .Where(p => p.GetIndexParameters().Length == 0)
            .Where(p => p.Name != nameof(IEntity.Id))
            .Where(p => IsScalarType(p.PropertyType));

    private static bool IsScalarType(Type type)
        => type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(decimal);

    private static bool IsSensitivePropertyName(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        var normalized = propertyName.Replace(".", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

        return SensitivePropertyNameParts.Any(part => normalized.Contains(part, StringComparison.Ordinal));
    }

    private static bool IsCollectionNode(object value)
        => value is IEnumerable && value is not string;

    private static object FirstCollectionItem(object value)
    {
        if (value is not IEnumerable enumerable || value is string)
        {
            return null;
        }

        foreach (var item in enumerable)
        {
            if (item is not null)
            {
                return item;
            }
        }

        return null;
    }

    private static string GetCurrentModuleName()
        => Activity.Current?.GetBaggageItem(ModuleConstants.ModuleNameKey) ??
            Activity.Current?.GetBaggageItem(ActivityConstants.ModuleNameTagKey);

    private static string GetCurrentActivityParentId()
        => Activity.Current?.ParentId ?? Activity.Current?.Id;

    private static object GetValueByPath(object instance, string path)
    {
        var value = instance;
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (value is null)
            {
                return null;
            }

            value = value.GetType().GetProperty(part, BindingFlags.Instance | BindingFlags.Public)?.GetValue(value);
        }

        return value;
    }

    private static void SetValueByPath(object instance, string path, object value)
    {
        if (instance is null || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var current = instance;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            current = current?.GetType().GetProperty(parts[i], BindingFlags.Instance | BindingFlags.Public)?.GetValue(current);
            if (current is null)
            {
                return;
            }
        }

        var property = current.GetType().GetProperty(parts[^1], BindingFlags.Instance | BindingFlags.Public);
        if (property?.CanWrite == true)
        {
            property.SetValue(current, value);
        }
    }

    private static Dictionary<string, object> ToIdentityMap(object value, Func<object, string> identityAccessor)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        if (value is not IEnumerable enumerable)
        {
            return result;
        }

        foreach (var item in enumerable)
        {
            var id = item is null ? null : identityAccessor(item);
            if (!string.IsNullOrWhiteSpace(id))
            {
                result[id] = item;
            }
        }

        return result;
    }

    private static string GetPropertyName(LambdaExpression property)
    {
        if (property?.Body is MemberExpression memberExpression)
        {
            return memberExpression.Member.Name;
        }

        if (property?.Body is UnaryExpression { Operand: MemberExpression unaryMemberExpression })
        {
            return unaryMemberExpression.Member.Name;
        }

        return null;
    }

    private static bool IsDefaultId(object id)
    {
        if (id is null)
        {
            return true;
        }

        var type = id.GetType();
        return type.IsValueType && Equals(id, Activator.CreateInstance(type));
    }

    private sealed record BulkAssignment(EntityFrameworkEntityUpdateSet<TEntity>.Assignment Assignment, string PropertyName);

    private sealed record ValueCapture(string StoredValue, string Hash, bool IsRestoreable);

    private sealed record ValuePolicyResult(string StoredValue, bool IsRestoreable);

    private sealed class CapturePreparation(TEntity entity, bool hasPendingEntityChanges)
    {
        public static readonly CapturePreparation Empty = new(null, false);

        public void ConsumePendingEntityChanges()
        {
            if (hasPendingEntityChanges)
            {
                EntityChangeHistoryAccessor.ConsumePendingChangeSets(entity);
            }
        }
    }
}
