// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using System.Runtime.CompilerServices;

/// <summary>
/// Provides provider-neutral helpers for paging through blob listing queries.
/// </summary>
/// <example>
/// <code>
/// var items = await blobs.ListAllAsync(new BlobQuery { Container = "reports", Prefix = "2026/" });
/// </code>
/// </example>
public static class BlobEnumerationExtensions
{
    /// <summary>
    /// Enumerates blob information as Result values so paging failures remain Result-native.
    /// </summary>
    /// <param name="blobClient">The blob client to enumerate.</param>
    /// <param name="query">The initial blob query.</param>
    /// <param name="options">Optional enumeration options.</param>
    /// <param name="cancellationToken">A token to cancel enumeration.</param>
    /// <returns>An async stream of blob information results.</returns>
    /// <example>
    /// <code>
    /// await foreach (var item in blobs.EnumerateAsync(query))
    /// {
    ///     if (item.IsSuccess) Console.WriteLine(item.Value.Key.Name);
    /// }
    /// </code>
    /// </example>
    public static async IAsyncEnumerable<Result<BlobInfo>> EnumerateAsync(
        this IBlobStoreClient blobClient,
        BlobQuery query,
        BlobEnumerationOptions options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (blobClient is null)
        {
            yield return Result<BlobInfo>.Failure(new ArgumentError("Blob client cannot be null."));
            yield break;
        }

        if (query is null)
        {
            yield return Result<BlobInfo>.Failure(new BlobStoreValidationError("Blob query is required."));
            yield break;
        }

        options ??= new BlobEnumerationOptions();
        if (options.MaxItems is <= 0)
        {
            yield return Result<BlobInfo>.Failure(new BlobStoreValidationError("MaxItems must be greater than zero when supplied."));
            yield break;
        }

        var nextQuery = query;
        var emitted = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pageResult = await blobClient.ListPageAsync(nextQuery, cancellationToken).ConfigureAwait(false);
            if (pageResult.IsFailure)
            {
                yield return Result<BlobInfo>.Failure(pageResult);
                yield break;
            }

            var page = pageResult.Value;
            foreach (var item in page.Items ?? [])
            {
                yield return Result<BlobInfo>.Success(item);
                emitted++;

                if (options.MaxItems is not null && emitted >= options.MaxItems.Value)
                {
                    yield break;
                }
            }

            if (!page.HasMore)
            {
                yield break;
            }

            nextQuery = ContinueQuery(query, page.ContinuationToken);
        }
    }

    /// <summary>
    /// Lists all blobs for a bounded query into memory.
    /// </summary>
    /// <param name="blobClient">The blob client to enumerate.</param>
    /// <param name="query">The initial blob query.</param>
    /// <param name="options">Optional enumeration options.</param>
    /// <param name="cancellationToken">A token to cancel enumeration.</param>
    /// <returns>A result containing the accumulated blob information.</returns>
    /// <example>
    /// <code>
    /// var result = await blobs.ListAllAsync(query, new BlobEnumerationOptions { MaxItems = 100 });
    /// </code>
    /// </example>
    public static async Task<Result<IReadOnlyList<BlobInfo>>> ListAllAsync(
        this IBlobStoreClient blobClient,
        BlobQuery query,
        BlobEnumerationOptions options = null,
        CancellationToken cancellationToken = default)
    {
        var items = new List<BlobInfo>();
        await foreach (var item in blobClient.EnumerateAsync(query, options, cancellationToken).ConfigureAwait(false))
        {
            if (item.IsFailure)
            {
                return Result<IReadOnlyList<BlobInfo>>.Failure(item);
            }

            items.Add(item.Value);
        }

        return Result<IReadOnlyList<BlobInfo>>.Success(items);
    }

    private static BlobQuery ContinueQuery(BlobQuery query, string continuationToken) =>
        new()
        {
            Container = query.Container,
            Prefix = query.Prefix,
            Take = query.Take,
            AllowFullScan = query.AllowFullScan,
            ContinuationToken = continuationToken
        };
}
