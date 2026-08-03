// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Exposes permalink creation for one explicitly enabled storage registration.
/// </summary>
/// <example>
/// <code>
/// var result = await accessor.GetPermalinkAsync(location, options, cancellationToken);
/// </code>
/// </example>
public interface IStoragePermalinkAccessor
{
    /// <summary>
    /// Gets the enabled storage registration name.
    /// </summary>
    string RegistrationName { get; }

    /// <summary>
    /// Gets the enabled storage resource kind.
    /// </summary>
    StorageResourceKind ResourceKind { get; }

    /// <summary>
    /// Verifies a resource and gets or creates its permalink.
    /// </summary>
    Task<Result<StoragePermalinkEntry>> GetPermalinkAsync(StorageResourceLocation location, StoragePermalinkCreateOptions options = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Coordinates permalink change tracking around a copy-then-delete move.
/// </summary>
/// <example>
/// <code>
/// using var suppression = coordinator.SuppressChangeTracking();
/// </code>
/// </example>
public interface IStoragePermalinkMoveCoordinator : IStoragePermalinkAccessor
{
    /// <summary>
    /// Suppresses granular write and delete notifications for the current asynchronous control flow.
    /// </summary>
    IDisposable SuppressChangeTracking();

    /// <summary>
    /// Publishes one permalink-preserving move after storage mutation succeeds.
    /// </summary>
    Task TrackMoveAsync(StorageResourceLocation source, StorageResourceLocation target);

    /// <summary>
    /// Publishes a target upsert when a copy succeeds but source deletion does not.
    /// </summary>
    Task TrackUpsertAsync(StorageResourceLocation target);
}

/// <summary>
/// Exposes the inner client of a Blob Storage decorator.
/// </summary>
/// <example>
/// <code>
/// var inner = decorator.InnerClient;
/// </code>
/// </example>
public interface IBlobStoreClientDecorator
{
    /// <summary>
    /// Gets the decorated Blob Storage client.
    /// </summary>
    IBlobStoreClient InnerClient { get; }
}

/// <summary>
/// Exposes the inner client of a Document Storage decorator.
/// </summary>
/// <typeparam name="T">
/// The document type.
/// </typeparam>
/// <example>
/// <code>
/// var inner = decorator.InnerClient;
/// </code>
/// </example>
public interface IDocumentStoreClientDecorator<T> where T : class, new()
{
    /// <summary>
    /// Gets the decorated Document Storage client.
    /// </summary>
    IDocumentStoreClient<T> InnerClient { get; }
}

/// <summary>
/// Provides ergonomic permalink lookup for enabled storage clients and providers.
/// </summary>
/// <example>
/// <code>
/// var link = await blobs.GetPermalinkAsync(key, cancellationToken: cancellationToken);
/// </code>
/// </example>
public static class StoragePermalinkExtensions
{
    /// <summary>
    /// Gets or creates a permalink for an existing blob.
    /// </summary>
    public static Task<Result<StoragePermalinkEntry>> GetPermalinkAsync(this IBlobStoreClient client, BlobKey key, StoragePermalinkCreateOptions options = null, CancellationToken cancellationToken = default)
    {
        var accessor = FindBlobAccessor(client);
        return accessor is null
            ? Task.FromResult(Result<StoragePermalinkEntry>.Failure(new StoragePermalinkNotEnabledError("blob")))
            : accessor.GetPermalinkAsync(StorageResourceLocation.ForBlob(accessor.RegistrationName, key), options, cancellationToken);
    }

    /// <summary>
    /// Gets or creates a permalink for an existing document.
    /// </summary>
    public static Task<Result<StoragePermalinkEntry>> GetPermalinkAsync<T>(this IDocumentStoreClient<T> client, DocumentKey key, StoragePermalinkCreateOptions options = null, CancellationToken cancellationToken = default) where T : class, new()
    {
        var accessor = FindDocumentAccessor(client);
        return accessor is null
            ? Task.FromResult(Result<StoragePermalinkEntry>.Failure(new StoragePermalinkNotEnabledError(typeof(T).Name)))
            : accessor.GetPermalinkAsync(StorageResourceLocation.ForDocument(accessor.RegistrationName, key), options, cancellationToken);
    }

    /// <summary>
    /// Gets or creates a permalink for an existing file.
    /// </summary>
    public static Task<Result<StoragePermalinkEntry>> GetPermalinkAsync(this IFileStorageProvider provider, string path, StoragePermalinkCreateOptions options = null, CancellationToken cancellationToken = default)
    {
        var accessor = FindFileAccessor(provider);
        return accessor is null
            ? Task.FromResult(Result<StoragePermalinkEntry>.Failure(new StoragePermalinkNotEnabledError(provider?.LocationName ?? "file")))
            : accessor.GetPermalinkAsync(StorageResourceLocation.ForFile(accessor.RegistrationName, path), options, cancellationToken);
    }

    /// <summary>
    /// Finds the permalink accessor through a Blob Storage decorator chain.
    /// </summary>
    public static IStoragePermalinkAccessor FindBlobAccessor(IBlobStoreClient client) => client switch
    {
        IStoragePermalinkAccessor accessor => accessor,
        IBlobStoreClientDecorator decorator => FindBlobAccessor(decorator.InnerClient),
        _ => null
    };

    /// <summary>
    /// Finds the permalink accessor through a Document Storage decorator chain.
    /// </summary>
    public static IStoragePermalinkAccessor FindDocumentAccessor<T>(IDocumentStoreClient<T> client) where T : class, new() => client switch
    {
        IStoragePermalinkAccessor accessor => accessor,
        IDocumentStoreClientDecorator<T> decorator => FindDocumentAccessor(decorator.InnerClient),
        _ => null
    };

    /// <summary>
    /// Finds the permalink accessor through a File Storage behavior chain.
    /// </summary>
    public static IStoragePermalinkAccessor FindFileAccessor(IFileStorageProvider provider) => provider switch
    {
        IStoragePermalinkAccessor accessor => accessor,
        IFileStorageBehavior behavior => FindFileAccessor(behavior.InnerProvider),
        _ => null
    };

    /// <summary>
    /// Finds the permalink move coordinator through a Blob Storage decorator chain.
    /// </summary>
    public static IStoragePermalinkMoveCoordinator FindBlobMoveCoordinator(IBlobStoreClient client) => client switch
    {
        IStoragePermalinkMoveCoordinator coordinator => coordinator,
        IBlobStoreClientDecorator decorator => FindBlobMoveCoordinator(decorator.InnerClient),
        _ => null
    };

    /// <summary>
    /// Finds the permalink move coordinator through a Document Storage decorator chain.
    /// </summary>
    public static IStoragePermalinkMoveCoordinator FindDocumentMoveCoordinator<T>(IDocumentStoreClient<T> client) where T : class, new() => client switch
    {
        IStoragePermalinkMoveCoordinator coordinator => coordinator,
        IDocumentStoreClientDecorator<T> decorator => FindDocumentMoveCoordinator(decorator.InnerClient),
        _ => null
    };
}
