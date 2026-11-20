// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;

using Application.Storage;
using Microsoft.EntityFrameworkCore.Storage;

public partial class EntityFrameworkDocumentStoreProvider<TContext> : IDocumentDimensionDiagnosticsProvider where TContext : DbContext, IDocumentStoreContext
{
    /// <inheritdoc />
    public async Task<Result<DocumentDimensionDiagnostics>> GetDimensionDiagnosticsAsync(DocumentTypeIdentity type, bool includeDictionaryCount = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!this.Capabilities.Dimensions.SupportsDimensions) return Result<DocumentDimensionDiagnostics>.Failure(new DocumentDimensionsNotSupportedError());
        var snapshot = this.dimensionNames.Capture();
        if (!includeDictionaryCount) return Result<DocumentDimensionDiagnostics>.Success(snapshot);
        try
        {
            await using var scope = this.dimensionScopes.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(type.Value));
            var count = await context.Set<StorageDocumentDimensionName>().AsNoTracking().Where(row => row.ScopeHash == hash && row.Scope == type.Value).LongCountAsync(cancellationToken).ConfigureAwait(false);
            return Result<DocumentDimensionDiagnostics>.Success(snapshot with { DictionaryEntries = count });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return Result<DocumentDimensionDiagnostics>.Failure(new DocumentStoreProviderError("Dimension dictionary inspection failed.")); }
    }

    /// <summary>Clears cached dictionary identities after external resource recreation.</summary>
    /// <remarks>Call when reusing this provider instance after recreating its database or Dimensions tables. This operation performs no database I/O.</remarks>
    /// <example><code>provider.ClearDimensionNameCache();</code></example>
    public void ClearDimensionNameCache() => this.dimensionNames.Clear();

    /// <inheritdoc />
    public async Task<Result<DocumentInfo>> UpdateDimensionsAsync(DocumentTypeIdentity type, DocumentDimensionsUpdate update, CancellationToken cancellationToken = default)
    {
        if (!this.Capabilities.Dimensions.SupportsDimensions) return Result<DocumentInfo>.Failure(new DocumentDimensionsNotSupportedError());
        if (update?.Dimensions is null || string.IsNullOrWhiteSpace(update.Key.PartitionKey) || string.IsNullOrWhiteSpace(update.Key.RowKey))
            return Result<DocumentInfo>.Failure(new DocumentStoreInvalidQueryError("Dimension update key and replacement collection are required."));
        var condition = DocumentWriteValidator.Validate(new DocumentDeleteOptions { IfMatchETag = update.IfMatchETag });
        if (condition.IsFailure) return Result<DocumentInfo>.Failure(condition);
        try
        {
            await using var scope = this.dimensionScopes.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await using var dimensions = this.dimensionNames.BeginSession();
            var entity = await Exact(context, type, update.Key).AsNoTracking().Select(row => new StorageDocument
            {
                Id = row.Id, Type = row.Type, TypeHash = row.TypeHash,
                PartitionKey = row.PartitionKey, PartitionKeyHash = row.PartitionKeyHash,
                RowKey = row.RowKey, RowKeyHash = row.RowKeyHash,
                ContentHash = row.ContentHash, DimensionsJson = row.DimensionsJson,
                PropertiesJson = row.PropertiesJson, CreatedDate = row.CreatedDate, UpdatedDate = row.UpdatedDate,
                ExpiresAtUnixMilliseconds = row.ExpiresAtUnixMilliseconds, ConcurrencyVersion = row.ConcurrencyVersion
            }).SingleOrDefaultAsync(cancellationToken);
            if (entity is null) return Result<DocumentInfo>.Failure(new DocumentStoreNotFoundError());
            if (!Matches(entity, update.IfMatchETag)) return Result<DocumentInfo>.Failure(new DocumentStoreConflictError("The document ETag changed."));
            var changed = await DocumentDimensionMutations.ApplyAsync(context, entity, update.Dimensions, this.dimensionNames, dimensions, cancellationToken);
            if (changed.IsFailure) return Result<DocumentInfo>.Failure(changed);
            var originalVersion = entity.ConcurrencyVersion;
            entity.UpdatedDate = DateTimeOffset.UtcNow;
            entity.ConcurrencyVersion = Guid.NewGuid();
            // Publish only metadata and keep the payload out of both SELECT and UPDATE statements.
            var affected = await Exact(context, type, update.Key).Where(row => row.ConcurrencyVersion == originalVersion)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.DimensionsJson, entity.DimensionsJson)
                    .SetProperty(row => row.UpdatedDate, entity.UpdatedDate)
                    .SetProperty(row => row.ConcurrencyVersion, entity.ConcurrencyVersion), cancellationToken);
            if (affected != 1) return Result<DocumentInfo>.Failure(new DocumentStoreConflictError("The document ETag changed."));
            // This operation aborts the complete owned transaction on any failure; registration contexts retain savepoints.
            var savepoints = context.Database.AutoSavepointsEnabled;
            context.Database.AutoSavepointsEnabled = false;
            try
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                context.Database.AutoSavepointsEnabled = savepoints;
            }

            var committed = await CommitDimensionsAsync(transaction, "update-dimensions", cancellationToken).ConfigureAwait(false);
            if (committed.IsFailure) return Result<DocumentInfo>.Failure(committed);
            this.dimensionNames.PublishCommitted(dimensions);
            return Result<DocumentInfo>.Success(ToInfo(entity));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) { return Result<DocumentInfo>.Failure(MapError(exception)); }
    }

    private static async Task<Result> CommitDimensionsAsync(IDbContextTransaction transaction, string operation, CancellationToken token)
    {
        try
        {
            await transaction.CommitAsync(token).ConfigureAwait(false);
            return Result.Success();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return Result.Failure(new DocumentStoreIndeterminateOperationError(operation, transaction.TransactionId.ToString("N")));
        }
    }

    private static DocumentDimensions ReadDimensions(string json)
    {
        if (json is null) return DocumentDimensions.Empty;
        var result = DocumentDimensionValues.Deserialize(json);
        return result.IsSuccess ? result.Value : throw new InvalidDataException("Stored document Dimensions snapshot is invalid.");
    }

    private sealed class DocumentDimensionResultException(IResultError error) : Exception("Document Dimensions operation failed.")
    {
        public IResultError Error { get; } = error;
    }
}
