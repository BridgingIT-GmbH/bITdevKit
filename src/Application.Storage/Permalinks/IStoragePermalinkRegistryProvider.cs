// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Defines persistence operations used by the Storage Permalink Registry.
/// </summary>
/// <example>
/// <code>
/// var result = await provider.GetByIdAsync(id, cancellationToken);
/// </code>
/// </example>
public interface IStoragePermalinkRegistryProvider
{
    /// <summary>
    /// Gets the provider name used by diagnostics and metrics.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets one entry by permalink identifier.
    /// </summary>
    Task<Result<StoragePermalinkEntry>> GetByIdAsync(StoragePermalinkId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active entry at an exact storage location.
    /// </summary>
    Task<Result<StoragePermalinkEntry>> GetByLocationAsync(StorageResourceLocation location, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets or creates the active entry at a location.
    /// </summary>
    Task<Result<StoragePermalinkEntry>> GetOrCreateAsync(StorageResourceLocation location, StoragePermalinkCreateOptions options = null, DateTimeOffset? occurredAt = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves an active mapping within one configured registration.
    /// </summary>
    Task<Result<StoragePermalinkEntry>> MoveAsync(StorageResourceLocation source, StorageResourceLocation target, DateTimeOffset occurredAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves every active File Storage mapping below a source prefix.
    /// </summary>
    Task<Result<long>> MovePrefixAsync(StorageResourceLocation sourcePrefix, StorageResourceLocation targetPrefix, DateTimeOffset occurredAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tombstones the active mapping at a location.
    /// </summary>
    Task<Result> DeleteByLocationAsync(StorageResourceLocation location, DateTimeOffset occurredAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tombstones every active File Storage mapping below a prefix.
    /// </summary>
    Task<Result<long>> DeletePrefixAsync(StorageResourceLocation prefix, DateTimeOffset occurredAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces or clears expiration for an existing permalink.
    /// </summary>
    Task<Result<StoragePermalinkEntry>> UpdateExpirationAsync(StoragePermalinkId id, StoragePermalinkExpirationUpdate update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tombstones a permalink without deleting its resource.
    /// </summary>
    Task<Result> DeleteAsync(StoragePermalinkId id, StoragePermalinkDeleteOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists one bounded page for maintenance and dashboard surfaces.
    /// </summary>
    Task<Result<StoragePermalinkPage>> ListPageAsync(StoragePermalinkQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides application-facing permalink lookup and creation.
/// </summary>
/// <example>
/// <code>
/// var link = await registry.GetOrCreateAsync(location, options, cancellationToken);
/// </code>
/// </example>
public interface IStoragePermalinkRegistry
{
    /// <summary>
    /// Gets one permalink by identifier.
    /// </summary>
    Task<Result<StoragePermalinkEntry>> GetAsync(StoragePermalinkId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets or creates a permalink at an existing verified location.
    /// </summary>
    Task<Result<StoragePermalinkEntry>> GetOrCreateAsync(StorageResourceLocation location, StoragePermalinkCreateOptions options = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides administrative permalink listing, expiration, and deletion operations.
/// </summary>
/// <example>
/// <code>
/// var page = await maintenance.ListPageAsync(new() { Take = 25 }, cancellationToken);
/// </code>
/// </example>
public interface IStoragePermalinkMaintenanceService
{
    /// <summary>
    /// Gets one permalink including expired entries.
    /// </summary>
    Task<Result<StoragePermalinkEntry>> GetAsync(StoragePermalinkId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists one bounded registry page.
    /// </summary>
    Task<Result<StoragePermalinkPage>> ListPageAsync(StoragePermalinkQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces or clears expiration.
    /// </summary>
    Task<Result<StoragePermalinkEntry>> UpdateExpirationAsync(StoragePermalinkId id, StoragePermalinkExpirationUpdate update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes only the permalink mapping.
    /// </summary>
    Task<Result> DeleteAsync(StoragePermalinkId id, StoragePermalinkDeleteOptions options = null, CancellationToken cancellationToken = default);
}
