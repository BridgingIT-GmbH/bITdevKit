// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;

using System.Text.Json;
using Application.Storage;

public partial class EntityFrameworkDocumentStoreProvider<TContext>
{
    /// <inheritdoc />
    public async Task<Result<DocumentInfoPage>> ListPageAsync(DocumentTypeIdentity type, DocumentFilterQuery query,
        DateTimeOffset visibilityCutoff, int pageSize, CancellationToken cancellationToken)
    {
        if (!this.Capabilities.SupportsFilterModel)
            return Result<DocumentInfoPage>.Failure(new DocumentStoreQueryNotSupportedError("Provider does not support FilterModel queries."));
        if (query is null || pageSize < 1 || pageSize > Math.Min(this.options.MaxTake, 1000))
            return Result<DocumentInfoPage>.Failure(new DocumentStoreInvalidQueryError("A valid filter query and bounded page size are required."));

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await using var scope = this.dimensionScopes.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            var owners = ApplyQuery(context, type, new DocumentQuery
            {
                DocumentKey = query.Scope.PartitionKey is { } partition ? new(partition, query.Scope.RowKeyPrefix ?? string.Empty) : null,
                Filter = DocumentKeyFilter.RowKeyPrefixMatch
            }, visibilityCutoff).AsNoTracking();
            var names = EntityFrameworkStorageFilterQuery.Dimensions(query.Criteria).Select(filter => filter.Predicate.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var resolved = await this.dimensionNames.ResolveForReadAsync(context, type.Value, names, cancellationToken).ConfigureAwait(false);
            if (resolved.IsFailure) return resolved.Wrap<DocumentInfoPage>();

            var rows = EntityFrameworkStorageFilterQuery.Filter(owners, query.Criteria, document => document.Id,
                MatchDocumentKey, (candidates, dimension) => DocumentDimensionQueryTranslator.BuildCandidates(context, candidates, dimension, resolved.Value));
            var slice = await EntityFrameworkStorageFilterQuery.Seek(rows, query.Order, query.Position).Take(pageSize + 1)
                .Select(document => new StorageDocument
                {
                    PartitionKey = document.PartitionKey,
                    RowKey = document.RowKey,
                    PartitionKeyHash = document.PartitionKeyHash,
                    RowKeyHash = document.RowKeyHash,
                    ConcurrencyVersion = document.ConcurrencyVersion,
                    ContentHash = document.ContentHash,
                    CreatedDate = document.CreatedDate,
                    UpdatedDate = document.UpdatedDate,
                    ExpiresAtUnixMilliseconds = document.ExpiresAtUnixMilliseconds,
                    PropertiesJson = document.PropertiesJson,
                    DimensionsJson = document.DimensionsJson
                }).ToListAsync(cancellationToken).ConfigureAwait(false);

            string next = null;
            if (slice.Count > pageSize)
            {
                var last = slice[pageSize - 1];
                next = JsonSerializer.Serialize(query.Order.Select(term => term.Field switch
                {
                    "PartitionKey" => last.PartitionKey,
                    "RowKey" => last.RowKey,
                    "PartitionKeyHash" => last.PartitionKeyHash,
                    _ => last.RowKeyHash
                }));
            }

            return Result<DocumentInfoPage>.Success(new()
            {
                Items = slice.Take(pageSize).Select(ToInfo).ToArray(),
                ContinuationToken = next
            });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            return Result<DocumentInfoPage>.Failure(MapError(exception));
        }
    }

    private static Expression<Func<StorageDocument, bool>> MatchDocumentKey(StorageFilterCriterion<DocumentDimensionFilter> leaf)
    {
        var key = leaf.Value;
        var hash = HashHelper.ComputeSha256(key);
        return (leaf.Field, leaf.Operator) switch
        {
            ("PartitionKey", FilterOperator.Equal) => document => document.PartitionKeyHash == hash && document.PartitionKey == key,
            ("PartitionKey", FilterOperator.NotEqual) => document => document.PartitionKeyHash != hash || document.PartitionKey != key,
            ("PartitionKey", FilterOperator.StartsWith) => document => document.PartitionKey.StartsWith(key),
            ("RowKey", FilterOperator.Equal) => document => document.RowKeyHash == hash && document.RowKey == key,
            ("RowKey", FilterOperator.NotEqual) => document => document.RowKeyHash != hash || document.RowKey != key,
            _ => document => document.RowKey.StartsWith(key)
        };
    }
}
