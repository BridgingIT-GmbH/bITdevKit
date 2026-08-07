// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Entities;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;

/// <summary>
/// Describes filters and paging for querying ChangeHistory rows.
/// </summary>
/// <example>
/// <code>
/// var query = new ChangeHistoryFindAllQuery { EntityType = "Customer", PageSize = 20 };
/// </code>
/// </example>
public sealed class ChangeHistoryFindAllQuery
{
    /// <summary>
    /// Gets or sets the entity type filter.
    /// </summary>
    public string EntityType { get; set; }

    /// <summary>
    /// Gets or sets the entity id filter.
    /// </summary>
    public string EntityId { get; set; }

    /// <summary>
    /// Gets or sets the property name/path filter.
    /// </summary>
    public string PropertyName { get; set; }

    /// <summary>
    /// Gets or sets the change set id filter.
    /// </summary>
    public Guid? ChangeSetId { get; set; }

    /// <summary>
    /// Gets or sets the bulk operation id filter.
    /// </summary>
    public Guid? BulkOperationId { get; set; }

    /// <summary>
    /// Gets or sets the user id filter.
    /// </summary>
    public string ChangedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the inclusive changed-date lower bound.
    /// </summary>
    public DateTimeOffset? ChangedDateFrom { get; set; }

    /// <summary>
    /// Gets or sets the inclusive changed-date upper bound.
    /// </summary>
    public DateTimeOffset? ChangedDateTo { get; set; }

    /// <summary>
    /// Gets or sets the operation filter.
    /// </summary>
    public string Operation { get; set; }

    /// <summary>
    /// Gets or sets the capture source filter.
    /// </summary>
    public string CaptureSource { get; set; }

    /// <summary>
    /// Gets or sets the capture strategy filter.
    /// </summary>
    public string CaptureStrategy { get; set; }

    /// <summary>
    /// Gets or sets the capture status filter.
    /// </summary>
    public string CaptureStatus { get; set; }

    /// <summary>
    /// Gets or sets the page number. Values less than 1 are treated as 1.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the page size. Values less than 1 are treated as 20.
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Gets or sets a value indicating whether results should be ordered oldest first.
    /// </summary>
    public bool OrderAscending { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether serialized old/new values should be included in query DTOs.
    /// </summary>
    public bool IncludeValues { get; set; } = true;
}

/// <summary>
/// Describes filters for querying one ChangeHistory change set.
/// </summary>
/// <example>
/// <code>
/// var query = new ChangeHistoryFindOneChangeSetQuery { ChangeSetId = changeSetId };
/// </code>
/// </example>
public sealed class ChangeHistoryFindOneChangeSetQuery
{
    /// <summary>
    /// Gets or sets the change set id.
    /// </summary>
    public Guid ChangeSetId { get; set; }

    /// <summary>
    /// Gets or sets the optional entity type filter.
    /// </summary>
    public string EntityType { get; set; }

    /// <summary>
    /// Gets or sets the optional entity id filter.
    /// </summary>
    public string EntityId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether serialized old/new values should be included in query DTOs.
    /// </summary>
    public bool IncludeValues { get; set; } = true;
}

/// <summary>
/// Defines how a ChangeHistory restore request selects values to restore.
/// </summary>
/// <example>
/// <code>
/// var mode = ChangeHistoryRestoreMode.PointInTime;
/// </code>
/// </example>
public enum ChangeHistoryRestoreMode
{
    /// <summary>
    /// Restores only the values captured by the selected change set.
    /// </summary>
    ChangeSet = 0,

    /// <summary>
    /// Restores values captured by the selected change set plus earlier rows needed to rebuild the entity state at that point.
    /// </summary>
    PointInTime = 1
}

/// <summary>
/// Describes a ChangeHistory restore use case.
/// </summary>
/// <typeparam name="TEntity">The entity type to restore.</typeparam>
/// <example>
/// <code>
/// var command = new ChangeHistoryRestoreCommand&lt;Customer&gt;(customerId, changeSetId, "Undo");
/// </code>
/// </example>
public sealed record ChangeHistoryRestoreCommand<TEntity>(
    object EntityId,
    Guid ChangeSetId,
    string Reason = null,
    Guid? ExpectedConcurrencyVersion = null,
    ChangeHistoryRestoreMode RestoreMode = ChangeHistoryRestoreMode.ChangeSet)
    where TEntity : class, IEntity;

/// <summary>
/// Contains a ChangeHistory restore result.
/// </summary>
/// <example>
/// <code>
/// Console.WriteLine(result.RestoredChangeSetId);
/// </code>
/// </example>
public sealed record ChangeHistoryRestoreResult(Guid RestoredChangeSetId, int RestoredPropertyCount);

/// <summary>
/// Safe read model returned by ChangeHistory queries.
/// </summary>
/// <example>
/// <code>
/// foreach (var row in result.Values) Console.WriteLine(row.PropertyName);
/// </code>
/// </example>
public sealed record ChangeHistoryRecord(
    Guid Id,
    Guid ChangeSetId,
    int ChangeSetSequence,
    string EntityType,
    string EntityClrType,
    string EntityId,
    string EntityIdType,
    string PropertyName,
    string PropertyPath,
    string PathKind,
    string CollectionAction,
    string CollectionItemId,
    string ValueClrType,
    string OldValue,
    string NewValue,
    string OldValueHash,
    string NewValueHash,
    string Operation,
    string CaptureStrategy,
    string CaptureSource,
    string CaptureStatus,
    string CaptureMessage,
    Guid? BulkOperationId,
    int? AffectedEntityCount,
    bool IsRestoreable,
    string RestorePlanName,
    string RestoreExecutionMode,
    string DomainRestoreHandlerName,
    string ChangedByUserId,
    string ChangedByUserName,
    string ChangedByEmail,
    DateTimeOffset ChangedDate,
    long ChangedDateTicks,
    string Reason,
    string CorrelationId,
    string FlowId,
    string ModuleName,
    string ActivityParentId,
    string Properties);

/// <summary>
/// Safe read model representing one ChangeHistory change set and its rows.
/// </summary>
/// <example>
/// <code>
/// Console.WriteLine(changeSet.Rows.Count);
/// </code>
/// </example>
public sealed record ChangeHistoryChangeSetRecord(
    Guid ChangeSetId,
    string EntityType,
    string EntityId,
    DateTimeOffset ChangedDate,
    string ChangedByUserId,
    string ChangedByUserName,
    string ChangedByEmail,
    string Operation,
    string CaptureSource,
    Guid? BulkOperationId,
    IReadOnlyList<ChangeHistoryRecord> Rows);

/// <summary>
/// Contains a paged ChangeHistory query result.
/// </summary>
/// <example>
/// <code>
/// Console.WriteLine($"{result.TotalCount} ChangeHistory rows found");
/// </code>
/// </example>
public sealed record ChangeHistoryFindAllResult(
    IEnumerable<ChangeHistoryRecord> Values,
    long TotalCount,
    int CurrentPage,
    int PageSize,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage);

/// <summary>
/// Contains a paged grouped ChangeHistory query result.
/// </summary>
/// <example>
/// <code>
/// Console.WriteLine($"{result.TotalCount} ChangeHistory change sets found");
/// </code>
/// </example>
public sealed record ChangeHistoryFindAllChangeSetsResult(
    IEnumerable<ChangeHistoryChangeSetRecord> Values,
    long TotalCount,
    int CurrentPage,
    int PageSize,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage);

/// <summary>
/// Provides ChangeHistory read and restore use cases for one aggregate type.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TContext">The persistence context type that owns the ChangeHistory store.</typeparam>
/// <example>
/// <code>
/// var result = await service.FindAllAsync(new ChangeHistoryFindAllQuery());
/// </code>
/// </example>
public interface IChangeHistoryService<TEntity, TContext>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Finds ChangeHistory rows.
    /// </summary>
    Task<Result<ChangeHistoryFindAllResult>> FindAllAsync(ChangeHistoryFindAllQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds grouped ChangeHistory change sets.
    /// </summary>
    Task<Result<ChangeHistoryFindAllChangeSetsResult>> FindAllChangeSetsAsync(ChangeHistoryFindAllQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds one grouped ChangeHistory change set.
    /// </summary>
    Task<Result<ChangeHistoryChangeSetRecord>> FindOneChangeSetAsync(ChangeHistoryFindOneChangeSetQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores an entity from ChangeHistory rows.
    /// </summary>
    Task<Result<ChangeHistoryRestoreResult>> RestoreAsync(ChangeHistoryRestoreCommand<TEntity> command, CancellationToken cancellationToken = default);
}
