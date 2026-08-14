// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Coordinates validated permalink lookup, creation, and maintenance.
/// </summary>
/// <example>
/// <code>
/// var entry = await registry.GetOrCreateAsync(location, options, cancellationToken);
/// </code>
/// </example>
public sealed class StoragePermalinkRegistry(IStoragePermalinkRegistryProvider provider, StoragePermalinkMetrics metrics = null)
    : IStoragePermalinkRegistry, IStoragePermalinkMaintenanceService
{
    /// <inheritdoc />
    public async Task<Result<StoragePermalinkEntry>> GetAsync(StoragePermalinkId id, CancellationToken cancellationToken = default)
    {
        var started = metrics?.Start() ?? 0;
        var result = await provider.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess && result.Value.Status != StoragePermalinkStatus.Active)
        {
            result = Result<StoragePermalinkEntry>.Failure(new StoragePermalinkNotFoundError());
        }

        metrics?.RecordOperation("resolve", started, result, result.IsSuccess ? result.Value.Location.Kind : null, provider.Name);
        return result;
    }

    /// <inheritdoc />
    public async Task<Result<StoragePermalinkEntry>> GetOrCreateAsync(StorageResourceLocation location, StoragePermalinkCreateOptions options = null, CancellationToken cancellationToken = default)
    {
        var started = metrics?.Start() ?? 0;
        var result = await provider.GetOrCreateAsync(location, options, cancellationToken: cancellationToken).ConfigureAwait(false);
        metrics?.RecordOperation("get_or_create", started, result, location?.Kind, provider.Name);
        return result;
    }

    async Task<Result<StoragePermalinkEntry>> IStoragePermalinkMaintenanceService.GetAsync(StoragePermalinkId id, CancellationToken cancellationToken)
    {
        var started = metrics?.Start() ?? 0;
        var result = await provider.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        metrics?.RecordOperation("maintenance_get", started, result, result.IsSuccess ? result.Value.Location.Kind : null, provider.Name);
        return result;
    }

    /// <inheritdoc />
    public async Task<Result<StoragePermalinkPage>> ListPageAsync(StoragePermalinkQuery query, CancellationToken cancellationToken = default)
    {
        var started = metrics?.Start() ?? 0;
        var result = await provider.ListPageAsync(query, cancellationToken).ConfigureAwait(false);
        metrics?.RecordOperation("maintenance_list", started, result, query?.Kind, provider.Name);
        return result;
    }

    /// <inheritdoc />
    public async Task<Result<StoragePermalinkEntry>> UpdateExpirationAsync(StoragePermalinkId id, StoragePermalinkExpirationUpdate update, CancellationToken cancellationToken = default)
    {
        var started = metrics?.Start() ?? 0;
        var result = await provider.UpdateExpirationAsync(id, update, cancellationToken).ConfigureAwait(false);
        metrics?.RecordOperation("maintenance_expiration", started, result, result.IsSuccess ? result.Value.Location.Kind : null, provider.Name);
        return result;
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(StoragePermalinkId id, StoragePermalinkDeleteOptions options = null, CancellationToken cancellationToken = default)
    {
        var started = metrics?.Start() ?? 0;
        var result = await provider.DeleteAsync(id, options, cancellationToken).ConfigureAwait(false);
        metrics?.RecordOperation("maintenance_delete", started, result, provider: provider.Name);
        return result;
    }
}
