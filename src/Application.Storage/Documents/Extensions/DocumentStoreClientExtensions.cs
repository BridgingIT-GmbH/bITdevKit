// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Runtime.CompilerServices;

/// <summary>Configures a bounded asynchronous document or key enumeration over continuation pages.</summary>
/// <example><code>var options = new DocumentEnumerationOptions { MaxItems = 500 };</code></example>
public sealed record DocumentEnumerationOptions
{
    /// <summary>Gets the mandatory positive maximum number of items yielded before enumeration stops.</summary>
    /// <example><code>var options = new DocumentEnumerationOptions { MaxItems = 500 };</code></example>
    public required int MaxItems { get; init; }
}

/// <summary>Configures bounded key-only discovery and deletion of documents matching a query.</summary>
/// <example><code>var options = new DocumentDeleteByQueryOptions { MaxItems = 100, DryRun = true };</code></example>
public sealed record DocumentDeleteByQueryOptions
{
    /// <summary>Gets the mandatory positive maximum number of matching keys considered.</summary>
    /// <example><code>var options = new DocumentDeleteByQueryOptions { MaxItems = 100 };</code></example>
    public required int MaxItems { get; init; }
    /// <summary>Gets whether matching keys are reported without performing physical deletion.</summary>
    /// <example><code>var options = new DocumentDeleteByQueryOptions { MaxItems = 100, DryRun = true };</code></example>
    public bool DryRun { get; init; }
    /// <summary>Gets whether processing continues with later keys after an individual deletion failure.</summary>
    /// <example><code>var options = new DocumentDeleteByQueryOptions { MaxItems = 100, ContinueOnError = true };</code></example>
    public bool ContinueOnError { get; init; }
}

/// <summary>Configures target concurrency, property, and expiration behavior for a typed document copy or move.</summary>
/// <example><code>var options = new DocumentTransferOptions { CreateOnly = true };</code></example>
public sealed record DocumentTransferOptions
{
    /// <summary>Gets whether the target write must fail when a physical target record already exists.</summary>
    /// <example><code>var options = new DocumentTransferOptions { CreateOnly = true };</code></example>
    public bool CreateOnly { get; init; }
    /// <summary>Gets whether cloned source properties are written when <see cref="PropertiesOverride" /> is null.</summary>
    /// <example><code>var preserve = options.PreserveProperties;</code></example>
    public bool PreserveProperties { get; init; } = true;
    /// <summary>Gets whether source expiration is applied when <see cref="ExpirationOverride" /> is null.</summary>
    /// <example><code>var preserve = options.PreserveExpiration;</code></example>
    public bool PreserveExpiration { get; init; } = true;
    /// <summary>Gets explicit replacement target properties, taking precedence over <see cref="PreserveProperties" />.</summary>
    /// <example><code>var options = new DocumentTransferOptions { PropertiesOverride = properties };</code></example>
    public PropertyBag PropertiesOverride { get; init; }
    /// <summary>Gets an explicit target expiration mutation, taking precedence over <see cref="PreserveExpiration" />.</summary>
    /// <example><code>var options = new DocumentTransferOptions { ExpirationOverride = ExpirationChange.Clear };</code></example>
    public ExpirationChange ExpirationOverride { get; init; }
}

/// <summary>Reports source metadata observed during transfer and committed target metadata.</summary>
/// <example><code>var copied = result.Target;</code></example>
public sealed record DocumentTransferResult
{
    /// <summary>Gets the source metadata snapshot, including the ETag used for conditional move deletion.</summary>
    /// <example><code>var sourceEtag = result.Source.ETag;</code></example>
    public required DocumentInfo Source { get; init; }
    /// <summary>Gets target metadata after serialization, transforms, and persistence by the target client.</summary>
    /// <example><code>var targetHash = result.Target.ContentHash;</code></example>
    public required DocumentInfo Target { get; init; }
    /// <summary>Gets whether a move conditionally deleted the source; copies and same-key no-op moves return false.</summary>
    /// <example><code>if (result.SourceDeleted) { /* move completed */ }</code></example>
    public bool SourceDeleted { get; init; }
}

/// <summary>Provides bounded enumeration, query maintenance, and cross-client typed transfer operations.</summary>
/// <remarks>
/// These operations compose the core client API and therefore retain its validation, behaviors, serialization, transforms,
/// expiration, integrity, and concurrency semantics. Enumeration and maintenance require explicit positive bounds.
/// </remarks>
/// <example><code>await foreach (var entry in client.EnumerateAsync(query, options, cancellationToken)) { }</code></example>
public static class DocumentStoreClientExtensions
{
    /// <summary>Enumerates visible documents through bounded pages up to a mandatory maximum.</summary>
    /// <typeparam name="T">The application document type.</typeparam>
    /// <param name="client">The client used to request document pages.</param>
    /// <param name="query">The initial bounded query and optional full-scan approval.</param>
    /// <param name="options">The mandatory total enumeration bound.</param>
    /// <param name="cancellationToken">The token used to cancel paging and enumeration.</param>
    /// <returns>An asynchronous sequence containing at most <see cref="DocumentEnumerationOptions.MaxItems" /> entries.</returns>
    /// <example><code>await foreach (var entry in client.EnumerateAsync(query, options, cancellationToken)) { }</code></example>
    public static async IAsyncEnumerable<DocumentEntry<T>> EnumerateAsync<T>(this IDocumentStoreClient<T> client, DocumentQuery query, DocumentEnumerationOptions options, [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : class, new()
    {
        EnsureMaximum(options?.MaxItems ?? 0);
        var yielded = 0;
        var current = query ?? new DocumentQuery();
        while (yielded < options.MaxItems)
        {
            var page = await client.FindPageAsync(current, cancellationToken);
            if (page.IsFailure) throw new InvalidOperationException(string.Join("; ", page.Errors.Select(x => x.Message)));
            foreach (var item in page.Value.Items)
            {
                if (yielded++ >= options.MaxItems) yield break;
                yield return item;
            }
            if (!page.Value.HasMore || yielded >= options.MaxItems) yield break;
            current = Copy(current, page.Value.ContinuationToken);
        }
    }

    /// <summary>Enumerates visible keys through bounded key-only pages up to a mandatory maximum.</summary>
    /// <typeparam name="T">The application document type.</typeparam>
    /// <param name="client">The client used to request key pages.</param>
    /// <param name="query">The initial bounded query and optional full-scan approval.</param>
    /// <param name="options">The mandatory total enumeration bound.</param>
    /// <param name="cancellationToken">The token used to cancel paging and enumeration.</param>
    /// <returns>An asynchronous sequence containing at most <see cref="DocumentEnumerationOptions.MaxItems" /> keys.</returns>
    /// <example><code>await foreach (var key in client.EnumerateKeysAsync(query, options, cancellationToken)) { }</code></example>
    public static async IAsyncEnumerable<DocumentKey> EnumerateKeysAsync<T>(this IDocumentStoreClient<T> client, DocumentQuery query, DocumentEnumerationOptions options, [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : class, new()
    {
        EnsureMaximum(options?.MaxItems ?? 0);
        var yielded = 0;
        var current = query ?? new DocumentQuery();
        while (yielded < options.MaxItems)
        {
            var page = await client.ListPageAsync(current, cancellationToken);
            if (page.IsFailure) throw new InvalidOperationException(string.Join("; ", page.Errors.Select(x => x.Message)));
            foreach (var item in page.Value.Items)
            {
                if (yielded++ >= options.MaxItems) yield break;
                yield return item;
            }
            if (!page.Value.HasMore || yielded >= options.MaxItems) yield break;
            current = Copy(current, page.Value.ContinuationToken);
        }
    }

    /// <summary>Discovers matching keys through bounded key-only pages and optionally deletes their physical records.</summary>
    /// <typeparam name="T">The application document type.</typeparam>
    /// <param name="client">The client used for key discovery and deletion.</param>
    /// <param name="query">The bounded matching query and optional full-scan approval.</param>
    /// <param name="options">The mandatory total bound, dry-run mode, and failure policy.</param>
    /// <param name="cancellationToken">The token used to cancel paging and deletion.</param>
    /// <returns>A result reporting processed keys and the first failed key when processing stops.</returns>
    /// <example><code>var result = await client.DeleteByQueryAsync(query, options, cancellationToken);</code></example>
    public static async Task<Result<DocumentBatchResult<DocumentKey>>> DeleteByQueryAsync<T>(this IDocumentStoreClient<T> client, DocumentQuery query, DocumentDeleteByQueryOptions options, CancellationToken cancellationToken = default) where T : class, new()
    {
        if (options is null || options.MaxItems <= 0) return Result<DocumentBatchResult<DocumentKey>>.Failure(new DocumentStoreInvalidQueryError("MaxItems must be greater than zero."));
        var completed = new List<DocumentKey>();
        var failed = new List<DocumentKey>();
        await foreach (var entry in client.EnumerateAsync(query, new() { MaxItems = options.MaxItems }, cancellationToken))
        {
            var key = entry.Key;
            if (options.DryRun) { completed.Add(key); continue; }
            var result = await client.DeleteAsync(key, new() { IfMatchETag = entry.ETag }, cancellationToken);
            if (result.IsFailure)
            {
                failed.Add(key);
                if (!options.ContinueOnError)
                {
                    return Result<DocumentBatchResult<DocumentKey>>.Success(new() { Items = completed, FailedKey = key, FailedKeys = failed }).WithMessages(result.Messages);
                }
            }
            if (result.IsSuccess) completed.Add(key);
        }
        return Result<DocumentBatchResult<DocumentKey>>.Success(new() { Items = completed, FailedKey = failed.Count > 0 ? failed[0] : null, FailedKeys = failed });
    }

    /// <summary>Copies a visible typed document through the target client's serializer, transforms, and provider.</summary>
    /// <typeparam name="T">The shared application document type.</typeparam>
    /// <param name="source">The source client.</param>
    /// <param name="sourceKey">The exact visible source key.</param>
    /// <param name="target">The target client, which can use another named registration or provider.</param>
    /// <param name="targetKey">The destination key.</param>
    /// <param name="options">Optional create-only, property, and expiration behavior.</param>
    /// <param name="cancellationToken">The token used to cancel read and write operations.</param>
    /// <returns>A result containing observed source and committed target metadata.</returns>
    /// <example><code>var result = await source.CopyAsync(sourceKey, archive, targetKey, options, cancellationToken);</code></example>
    public static async Task<Result<DocumentTransferResult>> CopyAsync<T>(this IDocumentStoreClient<T> source, DocumentKey sourceKey, IDocumentStoreClient<T> target, DocumentKey targetKey, DocumentTransferOptions options = null, CancellationToken cancellationToken = default) where T : class, new()
    {
        options ??= new();
        var read = await source.GetAsync(sourceKey, cancellationToken);
        if (read.IsFailure) return read.Wrap<DocumentTransferResult>();
        var entry = read.Value;
        var expiration = options.ExpirationOverride ?? (options.PreserveExpiration && entry.ExpiresAt is not null ? ExpirationChange.At(entry.ExpiresAt.Value) : ExpirationChange.Clear);
        var write = await target.UpsertAsync(targetKey, entry.Value, new() { CreateOnly = options.CreateOnly, Properties = options.PropertiesOverride ?? (options.PreserveProperties ? entry.Properties.Clone() : new PropertyBag()), Expiration = expiration }, cancellationToken);
        return write.IsFailure ? write.Wrap<DocumentTransferResult>() : Result<DocumentTransferResult>.Success(new() { Source = entry, Target = write.Value });
    }

    /// <summary>Copies first and then conditionally deletes the source using the ETag observed by the copy.</summary>
    /// <typeparam name="T">The shared application document type.</typeparam>
    /// <param name="source">The source client.</param>
    /// <param name="sourceKey">The exact visible source key.</param>
    /// <param name="target">The target client, which can use another named registration or provider.</param>
    /// <param name="targetKey">The destination key.</param>
    /// <param name="options">Optional target create-only, property, and expiration behavior.</param>
    /// <param name="cancellationToken">The token used to cancel read, write, and conditional delete operations.</param>
    /// <returns>
    /// A result reporting source and target metadata and whether source deletion completed. If the source changes after the
    /// copy, the target remains committed and a transfer failure reports that the source was retained.
    /// </returns>
    /// <example><code>var result = await source.MoveAsync(sourceKey, archive, targetKey, options, cancellationToken);</code></example>
    public static async Task<Result<DocumentTransferResult>> MoveAsync<T>(this IDocumentStoreClient<T> source, DocumentKey sourceKey, IDocumentStoreClient<T> target, DocumentKey targetKey, DocumentTransferOptions options = null, CancellationToken cancellationToken = default) where T : class, new()
    {
        if (ReferenceEquals(source, target) && sourceKey == targetKey)
        {
            var existing = await source.GetAsync(sourceKey, cancellationToken);
            return existing.IsFailure ? existing.Wrap<DocumentTransferResult>() : Result<DocumentTransferResult>.Success(new() { Source = existing.Value, Target = existing.Value, SourceDeleted = false });
        }
        var sourceCoordinator = StoragePermalinkExtensions.FindDocumentMoveCoordinator(source);
        var targetCoordinator = StoragePermalinkExtensions.FindDocumentMoveCoordinator(target);
        var preservePermalink = sourceCoordinator is not null && targetCoordinator is not null && sourceCoordinator.RegistrationName == targetCoordinator.RegistrationName;
        using var sourceSuppression = preservePermalink ? sourceCoordinator.SuppressChangeTracking() : null;
        using var targetSuppression = preservePermalink ? targetCoordinator.SuppressChangeTracking() : null;
        var targetWritten = false;
        var moveTracked = false;
        try
        {
            var copy = await source.CopyAsync(sourceKey, target, targetKey, options, cancellationToken);
            if (copy.IsFailure) return copy;
            targetWritten = true;
            var delete = await source.DeleteAsync(sourceKey, new() { IfMatchETag = copy.Value.Source.ETag }, cancellationToken);
            if (delete.IsFailure) return Result<DocumentTransferResult>.Failure(new DocumentStoreTransferError("Target exists, but the source changed and was not deleted.")).WithMessages(delete.Messages);
            if (preservePermalink)
            {
                await sourceCoordinator.TrackMoveAsync(StorageResourceLocation.ForDocument(sourceCoordinator.RegistrationName, sourceKey), StorageResourceLocation.ForDocument(targetCoordinator.RegistrationName, targetKey));
                moveTracked = true;
            }
            return Result<DocumentTransferResult>.Success(copy.Value with { SourceDeleted = true });
        }
        finally
        {
            if (preservePermalink && targetWritten && !moveTracked)
            {
                await targetCoordinator.TrackUpsertAsync(StorageResourceLocation.ForDocument(targetCoordinator.RegistrationName, targetKey));
            }
        }
    }

    private static void EnsureMaximum(int value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), "MaxItems must be greater than zero."); }
    private static DocumentQuery Copy(DocumentQuery query, string continuationToken) => new() { DocumentKey = query.DocumentKey, Filter = query.Filter, Take = query.Take, AllowFullScan = query.AllowFullScan, ContinuationToken = continuationToken };
}
