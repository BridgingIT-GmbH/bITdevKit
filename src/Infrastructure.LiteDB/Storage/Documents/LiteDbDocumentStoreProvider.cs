// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Text;
using System.Text.Json;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;

/// <summary>Stores serialized documents with native paging, exact identities, conditional writes, and expiration.</summary>
/// <param name="database">The selected native database and collection namespace.</param>
/// <param name="clock">The mutation clock.</param>
/// <example><code>services.AddDocumentStorage().WithLiteDbClient&lt;LedgerEntry&gt;();</code></example>
public sealed class LiteDbDocumentStoreProvider(LiteDbFeatureDatabase database, TimeProvider clock = null) : IDocumentStoreProvider, IDocumentStoreRetentionProvider
{
    private readonly LiteDbKeyedCollection<string, LiteDbDocumentRecord> documents = database.Collection<string, LiteDbDocumentRecord>("documents");
    private readonly TimeProvider clock = clock ?? TimeProvider.System;

    /// <inheritdoc />
    public DocumentStoreProviderCapabilities Capabilities => StorageCapabilities;

    /// <summary>Gets native document registration capabilities without opening a database.</summary>
    public static DocumentStoreProviderCapabilities StorageCapabilities { get; } = new()
    {
        FullMatch = DocumentQuerySupport.SupportedEfficiently, RowKeyPrefixMatch = DocumentQuerySupport.SupportedServerSide,
        RowKeySuffixMatch = DocumentQuerySupport.SupportedServerSide, FullScan = DocumentQuerySupport.SupportedServerSide,
        KeyListing = DocumentQuerySupport.SupportedServerSide, SupportsContinuationPaging = true, SupportsServerSideCount = true,
        SupportsKeyOnlyProjection = true, SupportsConditionalWrite = true, SupportsConditionalDelete = true,
        SupportsAtomicPropertyUpdate = true, SupportsLogicalExpiration = true, SupportsRetention = true,
        MaxStoredDocumentSize = 4 * 1024 * 1024
    };

    /// <inheritdoc />
    public Task<Result<StoredDocument>> GetAsync(DocumentTypeIdentity type, DocumentKey key, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default) => this.GuardAsync(() => this.GetCoreAsync(type, key, visibilityCutoff, cancellationToken), cancellationToken);

    private Task<Result<StoredDocument>> GetCoreAsync(DocumentTypeIdentity type, DocumentKey key, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default) => database.ExecuteAsync(() =>
        Task.FromResult(this.documents.TryGetValue(Key(type, key), out var row) && Visible(row.Document, visibilityCutoff)
            ? Result<StoredDocument>.Success(row.Document)
            : Result<StoredDocument>.Failure(new DocumentStoreNotFoundError())), cancellationToken);

    /// <inheritdoc />
    public Task<Result<StoredDocumentPage>> FindPageAsync(DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default) => this.GuardAsync(() => this.FindPageCoreAsync(type, query, visibilityCutoff, cancellationToken), cancellationToken);

    private Task<Result<StoredDocumentPage>> FindPageCoreAsync(DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default) => database.ExecuteAsync(() =>
    {
        query ??= new();
        var take = query.Take ?? 100;
        var valid = ValidatePage("find", type, query, visibilityCutoff, database.Options.MaxMaterializedRows, out var after);
        if (valid.IsFailure) return Task.FromResult(Result<StoredDocumentPage>.Failure(valid));
        var rows = database.Query<LiteDbDocumentRecord>("documents", Predicate(type, query.DocumentKey, query.Filter, visibilityCutoff, after), take + 1, "$.SortKey");
        return Task.FromResult(Result<StoredDocumentPage>.Success(new()
        {
            Items = rows.Take(take).Select(row => row.Document).ToArray(),
            ContinuationToken = rows.Count > take ? Token("find", type, query, visibilityCutoff, rows[take - 1].Document.Key) : null
        }));
    }, cancellationToken);

    /// <inheritdoc />
    public Task<Result<DocumentKeyPage>> ListPageAsync(DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default) => this.GuardAsync(() => this.ListPageCoreAsync(type, query, visibilityCutoff, cancellationToken), cancellationToken);

    private async Task<Result<DocumentKeyPage>> ListPageCoreAsync(DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default)
    {
        query ??= new();
        var take = query.Take ?? 100;
        var valid = ValidatePage("list", type, query, visibilityCutoff, database.Options.MaxMaterializedRows, out var after);
        if (valid.IsFailure) return Result<DocumentKeyPage>.Failure(valid);
        var keys = await database.InspectAsync(db => db.GetCollection<BsonDocument>(database.Options.CollectionPrefix + "_documents").Query()
            .Where(Predicate(type, query.DocumentKey, query.Filter, visibilityCutoff, after))
            .Select(BsonExpression.Create("{ Partition: $.Partition, Row: $.Row }")).OrderBy("$.SortKey").Limit(take + 1).ToList().Select(row => new DocumentKey(row["Partition"].AsString, row["Row"].AsString)).ToArray(), cancellationToken).ConfigureAwait(false);
        return Result<DocumentKeyPage>.Success(new()
        {
            Items = keys.Take(take).ToArray(), ContinuationToken = keys.Length > take ? Token("list", type, query, visibilityCutoff, keys[take - 1]) : null
        });
    }

    /// <inheritdoc />
    public Task<Result<long>> CountAsync(DocumentTypeIdentity type, DocumentCountQuery query, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default) => this.GuardAsync(() => this.CountCoreAsync(type, query, visibilityCutoff, cancellationToken), cancellationToken);

    private async Task<Result<long>> CountCoreAsync(DocumentTypeIdentity type, DocumentCountQuery query, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default) =>
        Result<long>.Success(await database.InspectAsync(db => db.GetCollection<BsonDocument>(database.Options.CollectionPrefix + "_documents")
            .LongCount(Predicate(type, query?.DocumentKey, query?.Filter ?? DocumentKeyFilter.FullMatch, visibilityCutoff, null)), cancellationToken).ConfigureAwait(false));

    /// <inheritdoc />
    public Task<Result<bool>> ExistsAsync(DocumentTypeIdentity type, DocumentKey key, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default) => this.GuardAsync(() => this.ExistsCoreAsync(type, key, visibilityCutoff, cancellationToken), cancellationToken);

    private async Task<Result<bool>> ExistsCoreAsync(DocumentTypeIdentity type, DocumentKey key, DateTimeOffset visibilityCutoff, CancellationToken cancellationToken = default) =>
        Result<bool>.Success(await database.InspectAsync(db => db.GetCollection<BsonDocument>(database.Options.CollectionPrefix + "_documents")
            .Exists(Predicate(type, key, DocumentKeyFilter.FullMatch, visibilityCutoff, null)), cancellationToken).ConfigureAwait(false));

    /// <inheritdoc />
    public Task<Result<DocumentInfo>> UpsertAsync(DocumentTypeIdentity type, StoredDocumentWrite write, CancellationToken cancellationToken = default) => this.GuardAsync(() => this.UpsertCoreAsync(type, write, cancellationToken), cancellationToken);

    private Task<Result<DocumentInfo>> UpsertCoreAsync(DocumentTypeIdentity type, StoredDocumentWrite write, CancellationToken cancellationToken = default) => database.ExecuteAsync(() =>
    {
        var condition = DocumentWriteValidator.Validate(write?.Options);
        if (condition.IsFailure) return Task.FromResult(Result<DocumentInfo>.Failure(condition));
        if (write is null || write.Content.Length > this.Capabilities.MaxStoredDocumentSize) return Task.FromResult(Result<DocumentInfo>.Failure(new DocumentStoreSizeLimitError("The document exceeds its storage bound.")));
        var key = Key(type, write.Key);
        this.documents.TryGetValue(key, out var current);
        var options = write.Options ?? new();
        if (options.CreateOnly && current is not null || !string.IsNullOrWhiteSpace(options.IfMatchETag) && current?.Document.ETag != options.IfMatchETag)
            return Task.FromResult(Result<DocumentInfo>.Failure(new DocumentStoreConflictError("The document identity or ETag changed.")));
        var now = this.clock.GetUtcNow();
        var stored = new StoredDocument
        {
            Key = write.Key, Content = write.Content.ToArray(), ContentHash = write.ContentHash, StoredContentHash = write.StoredContentHash,
            ETag = Guid.NewGuid().ToString("N"), CreatedAt = current?.Document.CreatedAt ?? now, LastModifiedAt = now,
            ExpiresAt = write.PreserveExpiration ? current?.Document.ExpiresAt : write.ExpiresAt,
            Properties = write.Properties?.Clone() ?? current?.Document.Properties?.Clone() ?? new(), TransformMetadata = write.TransformMetadata?.Clone() ?? new()
        };
        this.documents[key] = new() { Type = type.Value, Document = stored };
        return Task.FromResult(Result<DocumentInfo>.Success(Info(stored)));
    }, cancellationToken);

    /// <inheritdoc />
    public Task<Result<DocumentInfo>> UpdatePropertiesAsync(DocumentTypeIdentity type, DocumentPropertiesUpdate update, DateTimeOffset? resolvedExpiresAt, bool preserveExpiration, CancellationToken cancellationToken = default) => this.GuardAsync(() => this.UpdatePropertiesCoreAsync(type, update, resolvedExpiresAt, preserveExpiration, cancellationToken), cancellationToken);

    private Task<Result<DocumentInfo>> UpdatePropertiesCoreAsync(DocumentTypeIdentity type, DocumentPropertiesUpdate update, DateTimeOffset? resolvedExpiresAt, bool preserveExpiration, CancellationToken cancellationToken = default) => database.ExecuteAsync(() =>
    {
        var condition = DocumentWriteValidator.Validate(update);
        if (condition.IsFailure) return Task.FromResult(Result<DocumentInfo>.Failure(condition));
        var key = Key(type, update.Key);
        if (!this.documents.TryGetValue(key, out var current)) return Task.FromResult(Result<DocumentInfo>.Failure(new DocumentStoreNotFoundError()));
        if (!string.IsNullOrWhiteSpace(update.IfMatchETag) && current.Document.ETag != update.IfMatchETag) return Task.FromResult(Result<DocumentInfo>.Failure(new DocumentStoreConflictError()));
        current.Document = current.Document with
        {
            ETag = Guid.NewGuid().ToString("N"), LastModifiedAt = this.clock.GetUtcNow(),
            ExpiresAt = preserveExpiration ? current.Document.ExpiresAt : resolvedExpiresAt, Properties = update.Properties?.Clone() ?? current.Document.Properties.Clone()
        };
        return Task.FromResult(Result<DocumentInfo>.Success(Info(current.Document)));
    }, cancellationToken);

    /// <inheritdoc />
    public Task<Result> DeleteAsync(DocumentTypeIdentity type, DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default) => this.GuardAsync(() => this.DeleteCoreAsync(type, key, options, cancellationToken), cancellationToken);

    private Task<Result> DeleteCoreAsync(DocumentTypeIdentity type, DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default) => database.ExecuteAsync(() =>
    {
        var condition = DocumentWriteValidator.Validate(options);
        if (condition.IsFailure) return Task.FromResult(condition);
        var id = Key(type, key);
        if (!string.IsNullOrWhiteSpace(options?.IfMatchETag) && (!this.documents.TryGetValue(id, out var current) || current.Document.ETag != options.IfMatchETag))
            return Task.FromResult(Result.Failure(new DocumentStoreConflictError()));
        this.documents.Remove(id);
        return Task.FromResult(Result.Success());
    }, cancellationToken);

    /// <inheritdoc />
    public Task<Result<DocumentRetentionSweepResult>> SweepExpiredAsync(DocumentRetentionSweepRequest request, CancellationToken cancellationToken = default) => this.GuardAsync(() => this.SweepExpiredCoreAsync(request, cancellationToken), cancellationToken);

    private async Task<Result<DocumentRetentionSweepResult>> SweepExpiredCoreAsync(DocumentRetentionSweepRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var deleted = new List<DocumentKey>();
        var batches = 0;
        var more = false;
        for (; batches < request.MaxBatches;)
        {
            var keys = await database.ExecuteAsync(() =>
            {
                var rows = database.Query<LiteDbDocumentRecord>("documents", BsonExpression.Create("$.TypeKey = @0 AND $.ExpiresTicks <= @1", Hash(request.DocumentType.Value), request.VisibilityCutoff.UtcTicks),
                    Math.Min(request.BatchSize, database.Options.MaxMaterializedRows), "$.ExpiresTicks");
                foreach (var row in rows) this.documents.Remove(Key(request.DocumentType, row.Document.Key));
                return Task.FromResult(rows.Select(row => row.Document.Key).ToArray());
            }, cancellationToken).ConfigureAwait(false);
            batches++;
            deleted.AddRange(keys);
            more = keys.Length == Math.Min(request.BatchSize, database.Options.MaxMaterializedRows);
            if (!more) break;
            if (request.BatchDelay > TimeSpan.Zero) await Task.Delay(request.BatchDelay, cancellationToken).ConfigureAwait(false);
        }

        return Result<DocumentRetentionSweepResult>.Success(new() { DocumentType = request.DocumentType, DeletedCount = deleted.Count, DeletedKeys = deleted, BatchCount = batches, HasMore = more });
    }

    private async Task<Result<T>> GuardAsync<T>(Func<Task<Result<T>>> action, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try { return await action().ConfigureAwait(false); }
        catch (OperationCanceledException) { throw; }
        catch (ArgumentException) { return Result<T>.Failure(new DocumentStoreInvalidQueryError()); }
        catch (Exception) { return Result<T>.Failure(new DocumentStoreProviderError("The native document operation failed.")); }
    }

    private async Task<Result> GuardAsync(Func<Task<Result>> action, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try { return await action().ConfigureAwait(false); }
        catch (OperationCanceledException) { throw; }
        catch (ArgumentException) { return Result.Failure(new DocumentStoreInvalidQueryError()); }
        catch (Exception) { return Result.Failure(new DocumentStoreProviderError("The native document operation failed.")); }
    }

    private static string Key(DocumentTypeIdentity type, DocumentKey key) => JsonSerializer.Serialize(new[] { type.Value, key.PartitionKey, key.RowKey });

    private static bool Visible(StoredDocument value, DateTimeOffset cutoff) => value.ExpiresAt is null || value.ExpiresAt > cutoff;

    private static string Hash(string value) => HashHelper.ComputeSha256(Encoding.UTF8.GetBytes(value));

    private static string Sort(DocumentKey key) => LiteDbDocumentRecord.EncodeOrdinal(key.PartitionKey) + "!" + LiteDbDocumentRecord.EncodeOrdinal(key.RowKey);

    private static Result ValidatePage(string operation, DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset cutoff, int maximum, out DocumentKey? after)
    {
        after = null;
        if ((query.Take ?? 100) <= 0 || (query.Take ?? 100) >= maximum) return Result.Failure(new DocumentStoreInvalidQueryError("The page exceeds the native materialization bound."));
        if (string.IsNullOrWhiteSpace(query.ContinuationToken)) return Result.Success();
        var token = DocumentContinuationTokenSerializer.Deserialize(query.ContinuationToken);
        if (token.IsFailure) return Result.Failure().WithErrors(token.Errors);
        if (token.Value.Provider != type.Value || token.Value.QueryHash != DocumentQueryHash.Compute(operation, type, query, query.Take ?? 100) || token.Value.VisibilityCutoff != cutoff)
            return Result.Failure(new DocumentStoreInvalidQueryError("The continuation does not match this selection."));
        try { after = JsonSerializer.Deserialize<DocumentKey>(token.Value.NativeToken); return Result.Success(); }
        catch (Exception error) when (error is JsonException or ArgumentException) { return Result.Failure(new DocumentStoreInvalidQueryError("Invalid native continuation.")); }
    }

    private static BsonExpression Predicate(DocumentTypeIdentity type, DocumentKey? key, DocumentKeyFilter filter, DateTimeOffset cutoff, DocumentKey? after)
    {
        var clauses = new List<string> { "$.TypeKey = @0", "$.ExpiresTicks > @1" };
        var values = new List<BsonValue> { Hash(type.Value), cutoff.UtcTicks };
        if (key.HasValue)
        {
            Add("PartitionKey", "=", Hash(key.Value.PartitionKey));
            var row = LiteDbDocumentRecord.EncodeOrdinal(key.Value.RowKey);
            switch (filter)
            {
                case DocumentKeyFilter.FullMatch: Add("RowKey", "=", row); break;
                case DocumentKeyFilter.RowKeyPrefixMatch: Add("RowKey", "LIKE", row + "%"); break;
                case DocumentKeyFilter.RowKeySuffixMatch: Add("RowKey", "LIKE", "%" + row); break;
                default: throw new ArgumentException("Unsupported document key filter.");
            }
        }

        if (after.HasValue) Add("SortKey", ">", Sort(after.Value));
        return BsonExpression.Create(string.Join(" AND ", clauses), values.ToArray());
        void Add(string field, string op, BsonValue value) { clauses.Add($"$.{field} {op} @{values.Count}"); values.Add(value); }
    }

    private static string Token(string operation, DocumentTypeIdentity type, DocumentQuery query, DateTimeOffset cutoff, DocumentKey key) => DocumentContinuationTokenSerializer.Serialize(new()
    {
        Provider = type.Value, QueryHash = DocumentQueryHash.Compute(operation, type, query, query.Take ?? 100), VisibilityCutoff = cutoff, NativeToken = JsonSerializer.Serialize(key)
    }).Value;

    private static DocumentInfo Info(StoredDocument value) => new()
    {
        Key = value.Key, ETag = value.ETag, ContentHash = value.ContentHash, ContentLength = value.Content.Length, CreatedAt = value.CreatedAt,
        LastModifiedAt = value.LastModifiedAt, ExpiresAt = value.ExpiresAt, Properties = value.Properties.Clone()
    };
}

/// <summary>Stores one original document and case-sensitive native query metadata.</summary>
public sealed class LiteDbDocumentRecord : ILiteDbIndexedRecord
{
    /// <summary>Gets or sets the document type namespace.</summary>
    public string Type { get; set; }

    /// <summary>Gets or sets the complete serialized document.</summary>
    public StoredDocument Document { get; set; }

    /// <inheritdoc />
    public void WriteIndexes(BsonDocument document)
    {
        document["TypeKey"] = HashHelper.ComputeSha256(Encoding.UTF8.GetBytes(this.Type));
        document["PartitionKey"] = HashHelper.ComputeSha256(Encoding.UTF8.GetBytes(this.Document.Key.PartitionKey));
        document["RowKey"] = EncodeOrdinal(this.Document.Key.RowKey);
        document["Partition"] = this.Document.Key.PartitionKey;
        document["Row"] = this.Document.Key.RowKey;
        document["SortKey"] = EncodeOrdinal(this.Document.Key.PartitionKey) + "!" + EncodeOrdinal(this.Document.Key.RowKey);
        document["ExpiresTicks"] = this.Document.ExpiresAt?.UtcTicks ?? long.MaxValue;
    }

    /// <summary>Encodes UTF-16 code units so native lexical sorting preserves ordinal key ordering.</summary>
    /// <param name="value">The logical key or an empty prefix.</param>
    /// <returns>A hexadecimal key independent of native string collation.</returns>
    public static string EncodeOrdinal(string value) => Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(value ?? ""));
}
