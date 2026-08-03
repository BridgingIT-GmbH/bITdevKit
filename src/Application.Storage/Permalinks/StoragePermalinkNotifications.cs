// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using BridgingIT.DevKit.Common.Utilities;

/// <summary>
/// Identifies the registry mutation represented by a storage change notification.
/// </summary>
/// <example>
/// <code>
/// var kind = StorageResourceChangeKind.Upserted;
/// </code>
/// </example>
public enum StorageResourceChangeKind
{
    /// <summary>
    /// A resource was created or overwritten.
    /// </summary>
    Upserted = 0,

    /// <summary>
    /// A resource was deleted.
    /// </summary>
    Deleted = 1,

    /// <summary>
    /// A resource moved within one configured registration.
    /// </summary>
    Moved = 2,

    /// <summary>
    /// A File Storage directory prefix moved.
    /// </summary>
    PrefixMoved = 3,

    /// <summary>
    /// A File Storage directory prefix was deleted.
    /// </summary>
    PrefixDeleted = 4
}

/// <summary>
/// Reports one completed provider-neutral storage location mutation.
/// </summary>
/// <example>
/// <code>
/// await queue.EnqueueAsync(new StorageResourceChangedNotification(StorageResourceChangeKind.Upserted, location));
/// </code>
/// </example>
public sealed record StorageResourceChangedNotification : ISimpleNotification
{
    /// <summary>
    /// Initializes a storage change notification.
    /// </summary>
    public StorageResourceChangedNotification(StorageResourceChangeKind changeKind, StorageResourceLocation location, StorageResourceLocation targetLocation = null, DateTimeOffset? occurredAt = null)
    {
        this.ChangeKind = changeKind;
        this.Location = location ?? throw new ArgumentNullException(nameof(location));
        this.TargetLocation = targetLocation;
        this.OccurredAt = occurredAt ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets the unique notification identifier.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the completed mutation kind.
    /// </summary>
    public StorageResourceChangeKind ChangeKind { get; }

    /// <summary>
    /// Gets the source or affected location.
    /// </summary>
    public StorageResourceLocation Location { get; }

    /// <summary>
    /// Gets the target for move operations.
    /// </summary>
    public StorageResourceLocation TargetLocation { get; }

    /// <summary>
    /// Gets when the storage mutation completed.
    /// </summary>
    public DateTimeOffset OccurredAt { get; }
}

/// <summary>
/// Queues successful storage mutations for asynchronous registry synchronization.
/// </summary>
/// <example>
/// <code>
/// await queue.EnqueueAsync(notification, cancellationToken);
/// </code>
/// </example>
public interface IStoragePermalinkChangeQueue
{
    /// <summary>
    /// Queues one completed storage mutation.
    /// </summary>
    ValueTask<bool> EnqueueAsync(StorageResourceChangedNotification notification, CancellationToken cancellationToken = default);
}
