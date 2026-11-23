// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;

using System.Text.Json;
using Application.Storage;

public sealed partial class EntityFrameworkBlobStoreProvider<TContext>
{
    /// <inheritdoc />
    public async Task<Result<StoredBlobPage>> ListPageAsync(BlobFilterQuery query, int pageSize, CancellationToken cancellationToken)
    {
        if (!this.Capabilities.SupportsFilterModel)
            return Result<StoredBlobPage>.Failure(new BlobStoreQueryNotSupportedError("Provider does not support FilterModel queries."));
        if (query is null || pageSize < 1 || pageSize > Math.Min(this.options.MaxTake, 1000))
            return Result<StoredBlobPage>.Failure(new BlobStoreValidationError("A valid filter query and bounded page size are required."));

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await using var lease = this.CreateContextLease();
            var context = lease.Context;
            var container = query.Scope.Container;
            var containerHash = HashHelper.ComputeSha256(container);
            var owners = context.StorageBlobs.AsNoTracking()
                .Where(blob => blob.ContainerHash == containerHash && blob.Container == container);
            if (query.Scope.Prefix is { } prefix) owners = owners.Where(blob => blob.Name.StartsWith(prefix));

            var names = EntityFrameworkStorageFilterQuery.Dimensions(query.Criteria).Select(filter => filter.Predicate.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var resolved = await this.dimensionNames.ResolveForReadAsync(context, container, names, cancellationToken).ConfigureAwait(false);
            if (resolved.IsFailure) return resolved.Wrap<StoredBlobPage>();

            var rows = EntityFrameworkStorageFilterQuery.Filter(owners, query.Criteria, blob => blob.Id,
                MatchBlobKey, (candidates, dimension) => BlobDimensionQueryTranslator.BuildCandidates(context, candidates, dimension, resolved.Value));
            var slice = await EntityFrameworkStorageFilterQuery.Seek(rows, query.Order, query.Position).Take(pageSize + 1)
                .Select(blob => new
                {
                    blob.NameHash,
                    Info = new BlobInfoProjection(blob.Container, blob.Name, blob.Length, blob.ContentTypeMimeType, blob.ContentHash,
                        blob.ETag, blob.CreatedAt, blob.LastModifiedAt, blob.ExpiresAt, blob.PropertiesJson, blob.TransformMetadataJson, blob.DimensionsJson)
                }).ToListAsync(cancellationToken).ConfigureAwait(false);

            return Result<StoredBlobPage>.Success(new()
            {
                Items = slice.Take(pageSize).Select(item => ToInfo(item.Info)).ToArray(),
                ContinuationToken = slice.Count > pageSize
                    ? JsonSerializer.Serialize(new[] { slice[pageSize - 1].Info.Name, slice[pageSize - 1].NameHash }) : null
            });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return Result<StoredBlobPage>.Failure(new BlobStoreProviderError("Entity Framework blob filter query failed."));
        }
    }

    private static Expression<Func<StorageBlob, bool>> MatchBlobKey(StorageFilterCriterion<BlobDimensionFilter> leaf)
    {
        var key = leaf.Value;
        var hash = HashHelper.ComputeSha256(key);
        return (leaf.Field, leaf.Operator) switch
        {
            ("Container", FilterOperator.Equal) => blob => blob.ContainerHash == hash && blob.Container == key,
            ("Container", FilterOperator.NotEqual) => blob => blob.ContainerHash != hash || blob.Container != key,
            ("Name", FilterOperator.Equal) => blob => blob.NameHash == hash && blob.Name == key,
            ("Name", FilterOperator.NotEqual) => blob => blob.NameHash != hash || blob.Name != key,
            _ => blob => blob.Name.StartsWith(key)
        };
    }
}
