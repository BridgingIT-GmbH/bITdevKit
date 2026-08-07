// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Application.Entities;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Queries persisted ChangeHistory rows from an EF Core context.
/// </summary>
/// <typeparam name="TContext">The EF Core context type.</typeparam>
/// <example>
/// <code>
/// var service = new ChangeHistoryQueryService&lt;AppDbContext&gt;(dbContext);
/// var result = await service.FindAllAsync(new ChangeHistoryFindAllQuery { EntityType = "Customer" });
/// </code>
/// </example>
public class ChangeHistoryQueryService<TContext>
    where TContext : DbContext
{
    private readonly TContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeHistoryQueryService{TContext}" /> class.
    /// </summary>
    /// <param name="context">The EF Core context.</param>
    public ChangeHistoryQueryService(TContext context)
    {
        EnsureArg.IsNotNull(context, nameof(context));

        this.context = context;
    }

    /// <summary>
    /// Finds ChangeHistory rows matching the supplied filters.
    /// </summary>
    /// <param name="query">The query filters and paging.</param>
    /// <param name="cancellationToken">A token to observe while querying.</param>
    /// <returns>A paged result of safe ChangeHistory records.</returns>
    public async Task<ResultPaged<ChangeHistoryRecord>> FindAllAsync(
        ChangeHistoryFindAllQuery query,
        CancellationToken cancellationToken = default)
    {
        query ??= new ChangeHistoryFindAllQuery();
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : query.PageSize;

        var entries = this.context.Set<ChangeHistoryEntry>().AsNoTracking();
        entries = ApplyFilters(entries, query);

        var totalCount = await entries.LongCountAsync(cancellationToken).AnyContext();
        entries = query.OrderAscending
            ? entries.OrderBy(e => e.ChangedDate).ThenBy(e => e.ChangeSetSequence).ThenBy(e => e.Id)
            : entries.OrderByDescending(e => e.ChangedDate).ThenBy(e => e.ChangeSetSequence).ThenBy(e => e.Id);

        var values = await entries
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => ToRecord(e, query.IncludeValues))
            .ToListAsync(cancellationToken).AnyContext();

        return ResultPaged<ChangeHistoryRecord>.Success(values, totalCount, page, pageSize);
    }

    /// <summary>
    /// Finds ChangeHistory rows grouped by change set.
    /// </summary>
    /// <param name="query">The query filters and paging.</param>
    /// <param name="cancellationToken">A token to observe while querying.</param>
    /// <returns>A paged result of grouped ChangeHistory records.</returns>
    public async Task<ResultPaged<ChangeHistoryChangeSetRecord>> FindAllChangeSetsAsync(
        ChangeHistoryFindAllQuery query,
        CancellationToken cancellationToken = default)
    {
        query ??= new ChangeHistoryFindAllQuery();
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : query.PageSize;

        var entries = ApplyFilters(this.context.Set<ChangeHistoryEntry>().AsNoTracking(), query);
        var changeSetKeys = entries
            .Select(e => new { e.ChangeSetId, e.EntityType, e.EntityId })
            .Distinct();

        var totalCount = await changeSetKeys.LongCountAsync(cancellationToken).AnyContext();
        var orderedKeys = query.OrderAscending
            ? changeSetKeys
                .OrderBy(key => entries
                    .Where(e => e.ChangeSetId == key.ChangeSetId && e.EntityType == key.EntityType && e.EntityId == key.EntityId)
                    .OrderBy(e => e.ChangedDateTicks)
                    .Select(e => e.ChangedDateTicks)
                    .FirstOrDefault())
                .ThenBy(e => e.ChangeSetId)
            : changeSetKeys
                .OrderByDescending(key => entries
                    .Where(e => e.ChangeSetId == key.ChangeSetId && e.EntityType == key.EntityType && e.EntityId == key.EntityId)
                    .OrderByDescending(e => e.ChangedDateTicks)
                    .Select(e => e.ChangedDateTicks)
                    .FirstOrDefault())
                .ThenBy(e => e.ChangeSetId);

        var keys = await orderedKeys
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken).AnyContext();
        var keyIds = keys.Select(k => k.ChangeSetId).ToArray();
        var rows = await entries
            .Where(e => keyIds.Contains(e.ChangeSetId))
            .OrderBy(e => e.ChangeSetId)
            .ThenBy(e => e.ChangeSetSequence)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken).AnyContext();

        var grouped = keys.Select(key => ToChangeSetRecord(
            key.ChangeSetId,
            rows.Where(row => row.ChangeSetId == key.ChangeSetId && row.EntityType == key.EntityType && row.EntityId == key.EntityId).ToArray(),
            query.IncludeValues)).ToArray();

        return ResultPaged<ChangeHistoryChangeSetRecord>.Success(grouped, totalCount, page, pageSize);
    }

    /// <summary>
    /// Finds one ChangeHistory change set by id.
    /// </summary>
    /// <param name="query">The change-set query.</param>
    /// <param name="cancellationToken">A token to observe while querying.</param>
    /// <returns>The grouped change set when found.</returns>
    public async Task<Result<ChangeHistoryChangeSetRecord>> FindOneChangeSetAsync(
        ChangeHistoryFindOneChangeSetQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query is null || query.ChangeSetId == Guid.Empty)
        {
            return Result<ChangeHistoryChangeSetRecord>.Failure(new ValidationError("A valid change set id is required."));
        }

        var entries = this.context.Set<ChangeHistoryEntry>()
            .AsNoTracking()
            .Where(e => e.ChangeSetId == query.ChangeSetId);
        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            entries = entries.Where(e => e.EntityType == query.EntityType);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            entries = entries.Where(e => e.EntityId == query.EntityId);
        }

        var rows = await entries
            .OrderBy(e => e.ChangeSetSequence)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken).AnyContext();
        if (rows.Count == 0)
        {
            return Result<ChangeHistoryChangeSetRecord>.Failure(new NotFoundError($"Change set {query.ChangeSetId} was not found."));
        }

        return Result<ChangeHistoryChangeSetRecord>.Success(ToChangeSetRecord(query.ChangeSetId, rows, query.IncludeValues));
    }

    private static ChangeHistoryChangeSetRecord ToChangeSetRecord(
        Guid changeSetId,
        IReadOnlyList<ChangeHistoryEntry> rows,
        bool includeValues)
    {
        var first = rows.OrderBy(e => e.ChangeSetSequence).ThenBy(e => e.Id).First();

        return new ChangeHistoryChangeSetRecord(
            changeSetId,
            first.EntityType,
            first.EntityId,
            rows.Max(e => e.ChangedDate),
            first.ChangedByUserId,
            first.ChangedByUserName,
            first.ChangedByEmail,
            first.Operation,
            first.CaptureSource,
            first.BulkOperationId,
            rows.Select(row => ToRecord(row, includeValues)).ToArray());
    }

    private static ChangeHistoryRecord ToRecord(ChangeHistoryEntry e, bool includeValues)
        => new(
            e.Id,
            e.ChangeSetId,
            e.ChangeSetSequence,
            e.EntityType,
            e.EntityClrType,
            e.EntityId,
            e.EntityIdType,
            e.PropertyName,
            e.PropertyPath,
            e.PathKind,
            e.CollectionAction,
            e.CollectionItemId,
            e.ValueClrType,
            includeValues ? e.OldValue : null,
            includeValues ? e.NewValue : null,
            e.OldValueHash,
            e.NewValueHash,
            e.Operation,
            e.CaptureStrategy,
            e.CaptureSource,
            e.CaptureStatus,
            e.CaptureMessage,
            e.BulkOperationId,
            e.AffectedEntityCount,
            e.IsRestoreable,
            e.RestorePlanName,
            e.RestoreExecutionMode,
            e.DomainRestoreHandlerName,
            e.ChangedByUserId,
            e.ChangedByUserName,
            e.ChangedByEmail,
            e.ChangedDate,
            e.ChangedDateTicks,
            e.Reason,
            e.CorrelationId,
            e.FlowId,
            e.ModuleName,
            e.ActivityParentId,
            e.Properties);

    private static IQueryable<ChangeHistoryEntry> ApplyFilters(
        IQueryable<ChangeHistoryEntry> entries,
        ChangeHistoryFindAllQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            entries = entries.Where(e => e.EntityType == query.EntityType);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            entries = entries.Where(e => e.EntityId == query.EntityId);
        }

        if (!string.IsNullOrWhiteSpace(query.PropertyName))
        {
            entries = entries.Where(e => e.PropertyName == query.PropertyName || e.PropertyPath == query.PropertyName);
        }

        if (query.ChangeSetId.HasValue)
        {
            entries = entries.Where(e => e.ChangeSetId == query.ChangeSetId.Value);
        }

        if (query.BulkOperationId.HasValue)
        {
            entries = entries.Where(e => e.BulkOperationId == query.BulkOperationId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.ChangedByUserId))
        {
            entries = entries.Where(e => e.ChangedByUserId == query.ChangedByUserId);
        }

        if (query.ChangedDateFrom.HasValue)
        {
            entries = entries.Where(e => e.ChangedDate >= query.ChangedDateFrom.Value);
        }

        if (query.ChangedDateTo.HasValue)
        {
            entries = entries.Where(e => e.ChangedDate <= query.ChangedDateTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Operation))
        {
            entries = entries.Where(e => e.Operation == query.Operation);
        }

        if (!string.IsNullOrWhiteSpace(query.CaptureSource))
        {
            entries = entries.Where(e => e.CaptureSource == query.CaptureSource);
        }

        if (!string.IsNullOrWhiteSpace(query.CaptureStrategy))
        {
            entries = entries.Where(e => e.CaptureStrategy == query.CaptureStrategy);
        }

        if (!string.IsNullOrWhiteSpace(query.CaptureStatus))
        {
            entries = entries.Where(e => e.CaptureStatus == query.CaptureStatus);
        }

        return entries;
    }
}
