// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using BridgingIT.DevKit.Common;

/// <summary>Validates, serializes, hashes, and coordinates typed document operations.</summary>
/// <typeparam name="T">The document type.</typeparam>
/// <example><code>var client = new DocumentStoreClient&lt;Person&gt;(provider);</code></example>
public class DocumentStoreClient<T> : IDocumentStoreClient<T>, IDocumentStoreProviderAccessor, IDocumentStoreClientIdentity where T : class, new()
{
    private readonly DocumentTypeIdentity type = DocumentTypeIdentity.For<T>();
    private readonly ISerializer serializer;
    private readonly DocumentStoreOptions options;
    private readonly TimeProvider timeProvider;
    private readonly IReadOnlyList<IDocumentPayloadTransform> transforms;
    private readonly IReadOnlyDictionary<string, IDocumentPayloadTransform> transformsById;

    /// <summary>Initializes a document client.</summary>
    /// <param name="provider">The serialized persistence provider.</param>
    /// <param name="serializer">The logical document serializer.</param>
    /// <param name="options">Document safety options.</param>
    /// <param name="timeProvider">The operation clock.</param>
    /// <example><code>var client = new DocumentStoreClient&lt;Person&gt;(provider);</code></example>
    public DocumentStoreClient(
        IDocumentStoreProvider provider,
        ISerializer serializer = null,
        DocumentStoreOptions options = null,
        TimeProvider timeProvider = null,
        IEnumerable<IDocumentPayloadTransform> transforms = null,
        string clientName = "default")
    {
        EnsureArg.IsNotNull(provider, nameof(provider));
        this.Provider = provider;
        this.serializer = serializer ?? new SystemTextJsonSerializer();
        this.options = options ?? new DocumentStoreOptions();
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.ClientName = string.IsNullOrWhiteSpace(clientName) ? "default" : clientName.Trim().ToLowerInvariant();
        this.transforms = transforms?.ToArray() ?? [];
        this.transformsById = this.transforms.ToDictionary(x => x.Identifier, StringComparer.Ordinal);
    }

    /// <summary>Gets the provider used by this client.</summary>
    protected IDocumentStoreProvider Provider { get; }

    /// <inheritdoc />
    public string ClientName { get; }

    IDocumentStoreProvider IDocumentStoreProviderAccessor.Provider => this.Provider;

    /// <inheritdoc />
    public async Task<Result<DocumentEntry<T>>> GetAsync(DocumentKey key, CancellationToken cancellationToken = default)
    {
        var validation = ValidateKey(key);
        if (validation.IsFailure)
        {
            return Result<DocumentEntry<T>>.Failure(validation);
        }

        var result = await this.Provider.GetAsync(this.type, key, this.timeProvider.GetUtcNow(), cancellationToken);
        return result.IsFailure ? result.Wrap<DocumentEntry<T>>() : await this.DeserializeAsync(result.Value, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<DocumentPage<T>>> FindPageAsync(DocumentQuery query, CancellationToken cancellationToken = default)
    {
        var validation = DocumentQueryValidator.ValidatePage<T>("find", this.type.Value, query, this.Provider.Capabilities, this.options);
        if (validation.IsFailure)
        {
            return Result<DocumentPage<T>>.Failure(validation);
        }

        var visibilityCutoff = validation.Value.ContinuationToken?.VisibilityCutoff ?? this.timeProvider.GetUtcNow();
        var result = await this.Provider.FindPageAsync(this.type, query, visibilityCutoff, cancellationToken);
        if (result.IsFailure)
        {
            return result.Wrap<DocumentPage<T>>();
        }

        var items = new List<DocumentEntry<T>>(result.Value.Items.Count);
        foreach (var stored in result.Value.Items)
        {
            var item = await this.DeserializeAsync(stored, cancellationToken);
            if (item.IsFailure)
            {
                return item.Wrap<DocumentPage<T>>();
            }

            items.Add(item.Value);
        }

        return Result<DocumentPage<T>>.Success(new DocumentPage<T>
        {
            Items = items,
            ContinuationToken = result.Value.ContinuationToken
        });
    }

    /// <inheritdoc />
    public Task<Result<DocumentKeyPage>> ListPageAsync(DocumentQuery query, CancellationToken cancellationToken = default)
    {
        var validation = DocumentQueryValidator.ValidatePage<T>("list", this.type.Value, query, this.Provider.Capabilities, this.options);
        var visibilityCutoff = validation.IsSuccess
            ? validation.Value.ContinuationToken?.VisibilityCutoff ?? this.timeProvider.GetUtcNow()
            : default;
        return validation.IsFailure
            ? Task.FromResult(Result<DocumentKeyPage>.Failure(validation))
            : this.Provider.ListPageAsync(this.type, query, visibilityCutoff, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<long>> CountAsync(DocumentCountQuery query, CancellationToken cancellationToken = default)
    {
        var validation = DocumentQueryValidator.ValidateCount<T>("count", query, this.Provider.Capabilities, this.options);
        return validation.IsFailure
            ? Task.FromResult(Result<long>.Failure(validation))
            : this.Provider.CountAsync(this.type, query, this.timeProvider.GetUtcNow(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<bool>> ExistsAsync(DocumentKey key, CancellationToken cancellationToken = default)
    {
        var validation = ValidateKey(key);
        return validation.IsFailure
            ? Task.FromResult(Result<bool>.Failure((IResult)validation))
            : this.Provider.ExistsAsync(this.type, key, this.timeProvider.GetUtcNow(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<DocumentInfo>> UpsertAsync(
        DocumentKey key,
        T value,
        DocumentWriteOptions options = null,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateKey(key);
        if (validation.IsFailure)
        {
            return Result<DocumentInfo>.Failure(validation);
        }

        if (value is null)
        {
            return Result<DocumentInfo>.Failure(new DocumentStoreInvalidQueryError("Document value must not be null."));
        }

        options ??= new DocumentWriteOptions();
        try
        {
            using var stream = new MemoryStream();
            this.serializer.Serialize(value, stream);
            var content = stream.ToArray();
            if (content.LongLength > this.options.MaxDocumentSize)
            {
                return Result<DocumentInfo>.Failure(new DocumentStoreSizeLimitError($"Document size {content.LongLength} exceeds {this.options.MaxDocumentSize} bytes."));
            }

            var hash = ContentHashHelper.ComputeSha256(content);
            if (!string.IsNullOrWhiteSpace(options.ExpectedContentHash) &&
                !string.Equals(hash, options.ExpectedContentHash, StringComparison.OrdinalIgnoreCase))
            {
                return Result<DocumentInfo>.Failure(new DocumentStoreIntegrityError("Serialized content does not match ExpectedContentHash."));
            }

            var storedContent = content;
            var transformDescriptors = new List<ContentTransformDescriptor>(this.transforms.Count);
            foreach (var transform in this.transforms)
            {
                var metadata = new PropertyBag();
                storedContent = await transform.WriteAsync(storedContent, metadata, cancellationToken);
                transformDescriptors.Add(new() { Id = transform.Identifier, Properties = metadata.Clone() });
            }

            var storedHash = ContentHashHelper.ComputeSha256(storedContent);
            var transformMetadata = new PropertyBag();
            transformMetadata.Set("bdk_transform_envelope", ContentTransformEnvelopeCodec.Encode(new()
            {
                LogicalLength = content.LongLength,
                LogicalContentHash = hash,
                StoredLength = storedContent.LongLength,
                StoredContentHash = storedHash,
                Transforms = transformDescriptors
            }));
            var expiration = ExpirationHelper.Resolve(options.Expiration, null, this.timeProvider);
            return await this.Provider.UpsertAsync(this.type, new StoredDocumentWrite
            {
                Key = key,
                Content = storedContent,
                ContentHash = hash,
                StoredContentHash = storedHash,
                TransformMetadata = transformMetadata,
                Properties = options.Properties?.Clone(),
                ExpiresAt = expiration,
                PreserveExpiration = options.Expiration.Mode == ExpirationChangeMode.Preserve,
                Options = options
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<DocumentInfo>.Failure(new DocumentStoreSerializationError("Document serialization failed.", ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result<DocumentBatchResult<DocumentInfo>>> UpsertManyAsync(
        IReadOnlyCollection<DocumentWrite<T>> writes,
        CancellationToken cancellationToken = default)
    {
        if (writes is null)
        {
            return Result<DocumentBatchResult<DocumentInfo>>.Failure(new DocumentStoreInvalidQueryError("Writes must not be null."));
        }

        var materialized = writes.ToArray();
        if (materialized.Any(x => x is null || ValidateKey(x.Key).IsFailure || x.Value is null))
        {
            return Result<DocumentBatchResult<DocumentInfo>>.Failure(new DocumentStoreInvalidQueryError("Every write must contain a valid key and value."));
        }

        var completed = new List<DocumentInfo>(materialized.Length);
        foreach (var write in materialized)
        {
            var result = await this.UpsertAsync(write.Key, write.Value, write.Options, cancellationToken);
            if (result.IsFailure)
            {
                return Result<DocumentBatchResult<DocumentInfo>>.Success(new()
                {
                    Items = completed,
                    FailedKey = write.Key,
                    FailedKeys = [write.Key]
                }).WithMessages(result.Messages);
            }

            completed.Add(result.Value);
        }

        return Result<DocumentBatchResult<DocumentInfo>>.Success(new() { Items = completed });
    }

    /// <inheritdoc />
    public Task<Result<DocumentInfo>> UpdatePropertiesAsync(DocumentPropertiesUpdate update, CancellationToken cancellationToken = default)
    {
        if (update is null || ValidateKey(update.Key).IsFailure)
        {
            return Task.FromResult(Result<DocumentInfo>.Failure(new DocumentStoreInvalidQueryError("A valid properties update is required.")));
        }

        var expiresAt = ExpirationHelper.Resolve(update.Expiration, null, this.timeProvider);
        return this.Provider.UpdatePropertiesAsync(
            this.type,
            update with { Properties = update.Properties?.Clone() },
            expiresAt,
            update.Expiration.Mode == ExpirationChangeMode.Preserve,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> DeleteAsync(DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default)
    {
        var validation = ValidateKey(key);
        return validation.IsFailure
            ? Task.FromResult(validation)
            : this.Provider.DeleteAsync(this.type, key, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<DocumentBatchResult<DocumentKey>>> DeleteManyAsync(
        IReadOnlyCollection<DocumentDelete> deletes,
        CancellationToken cancellationToken = default)
    {
        if (deletes is null || deletes.Any(x => x is null || ValidateKey(x.Key).IsFailure))
        {
            return Result<DocumentBatchResult<DocumentKey>>.Failure(new DocumentStoreInvalidQueryError("Every delete must contain a valid key."));
        }

        var completed = new List<DocumentKey>(deletes.Count);
        foreach (var delete in deletes)
        {
            var result = await this.DeleteAsync(delete.Key, delete.Options, cancellationToken);
            if (result.IsFailure)
            {
                return Result<DocumentBatchResult<DocumentKey>>.Success(new()
                {
                    Items = completed,
                    FailedKey = delete.Key,
                    FailedKeys = [delete.Key]
                }).WithMessages(result.Messages);
            }

            completed.Add(delete.Key);
        }

        return Result<DocumentBatchResult<DocumentKey>>.Success(new() { Items = completed });
    }

    private async Task<Result<DocumentEntry<T>>> DeserializeAsync(StoredDocument stored, CancellationToken cancellationToken)
    {
        try
        {
            var storedHash = ContentHashHelper.ComputeSha256(stored.Content);
            if (!string.Equals(storedHash, stored.StoredContentHash, StringComparison.OrdinalIgnoreCase))
            {
                return Result<DocumentEntry<T>>.Failure(new DocumentStoreIntegrityError());
            }

            var encodedEnvelope = stored.TransformMetadata?.Get<string>("bdk_transform_envelope");
            if (string.IsNullOrWhiteSpace(encodedEnvelope))
            {
                return Result<DocumentEntry<T>>.Failure(new DocumentStoreSerializationError("Document transform envelope is missing."));
            }

            var envelope = ContentTransformEnvelopeCodec.Decode(encodedEnvelope);
            if (envelope.StoredLength != stored.Content.LongLength ||
                !string.Equals(envelope.StoredContentHash, stored.StoredContentHash, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(envelope.LogicalContentHash, stored.ContentHash, StringComparison.OrdinalIgnoreCase))
            {
                return Result<DocumentEntry<T>>.Failure(new DocumentStoreIntegrityError());
            }

            var logicalContent = stored.Content.ToArray();
            for (var index = envelope.Transforms.Count - 1; index >= 0; index--)
            {
                var descriptor = envelope.Transforms[index];
                if (!this.transformsById.TryGetValue(descriptor.Id, out var transform))
                {
                    return Result<DocumentEntry<T>>.Failure(new DocumentStoreSerializationError($"Document transform '{descriptor.Id}' is not registered."));
                }

                logicalContent = await transform.ReadAsync(logicalContent, descriptor.Properties.Clone(), cancellationToken);
            }

            if (envelope.LogicalLength != logicalContent.LongLength ||
                !string.Equals(ContentHashHelper.ComputeSha256(logicalContent), stored.ContentHash, StringComparison.OrdinalIgnoreCase))
            {
                return Result<DocumentEntry<T>>.Failure(new DocumentStoreIntegrityError());
            }

            using var stream = new MemoryStream(logicalContent, writable: false);
            var value = this.serializer.Deserialize<T>(stream);
            return value is null
                ? Result<DocumentEntry<T>>.Failure(new DocumentStoreSerializationError("Document deserialized to null."))
                : Result<DocumentEntry<T>>.Success(new DocumentEntry<T>
                {
                    Key = stored.Key,
                    Value = value,
                    ETag = stored.ETag,
                    ContentHash = stored.ContentHash,
                    CreatedAt = stored.CreatedAt,
                    LastModifiedAt = stored.LastModifiedAt,
                    ExpiresAt = stored.ExpiresAt,
                    Properties = stored.Properties?.Clone() ?? new PropertyBag()
                });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<DocumentEntry<T>>.Failure(new DocumentStoreSerializationError("Document deserialization failed.", ex));
        }
    }

    private static Result ValidateKey(DocumentKey key)
    {
        if (string.IsNullOrWhiteSpace(key.PartitionKey) || string.IsNullOrWhiteSpace(key.RowKey))
        {
            return Result.Failure(new DocumentStoreInvalidQueryError("PartitionKey and RowKey must not be null or whitespace."));
        }

        return Result.Success();
    }
}
