// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Text;

/// <summary>
/// Adapts a typed document client for dashboard operations.
/// </summary>
/// <typeparam name="T">
/// The document type.
/// </typeparam>
/// <example>
/// <code>
/// var accessor = new DocumentStoreClientAccessor&lt;Person&gt;(descriptor, client);
/// </code>
/// </example>
public sealed class DocumentStoreClientAccessor<T>(DocumentStoreClientDescriptor descriptor, IDocumentStoreClient<T> client, ISerializer serializer = null)
    : IDocumentStoreClientAccessor where T : class, new()
{
    private readonly ISerializer serializer = serializer ?? new SystemTextJsonSerializer();
    /// <inheritdoc />
    public DocumentStoreClientDescriptor Descriptor { get; } = descriptor;
    /// <inheritdoc />
    public bool PermalinksEnabled => StoragePermalinkExtensions.FindDocumentAccessor(client) is not null;
    /// <inheritdoc />
    public Task<Result<DocumentKeyPage>> ListPageAsync(DocumentQuery query, CancellationToken cancellationToken = default) => client.ListPageAsync(query, cancellationToken);
    /// <inheritdoc />
    public async Task<Result<DocumentJsonPage>> FindJsonPageAsync(DocumentQuery query, CancellationToken cancellationToken = default)
    {
        var result = await client.FindPageAsync(query, cancellationToken);
        if (result.IsFailure) return result.Wrap<DocumentJsonPage>();

        return Result<DocumentJsonPage>.Success(new()
        {
            Items = result.Value.Items.Select(entry =>
            {
                var content = this.serializer.SerializeToString(entry.Value);
                return new DocumentJsonEntry
                {
                    Content = content,
                    Info = entry,
                    Size = Encoding.UTF8.GetByteCount(content ?? string.Empty)
                };
            }).ToArray(),
            ContinuationToken = result.Value.ContinuationToken
        }).WithMessages(result.Messages);
    }
    /// <inheritdoc />
    public Task<Result<long>> CountAsync(DocumentCountQuery query, CancellationToken cancellationToken = default) => client.CountAsync(query, cancellationToken);
    /// <inheritdoc />
    public Task<Result<bool>> ExistsAsync(DocumentKey key, CancellationToken cancellationToken = default) => client.ExistsAsync(key, cancellationToken);
    /// <inheritdoc />
    public async Task<Result<string>> GetJsonAsync(DocumentKey key, CancellationToken cancellationToken = default)
    {
        var result = await this.GetEntryJsonAsync(key, cancellationToken);
        return result.IsFailure ? result.Wrap<string>() : Result<string>.Success(result.Value.Content).WithMessages(result.Messages);
    }
    /// <inheritdoc />
    public async Task<Result<DocumentJsonEntry>> GetEntryJsonAsync(DocumentKey key, CancellationToken cancellationToken = default)
    {
        var result = await client.GetAsync(key, cancellationToken);
        if (result.IsFailure) return result.Wrap<DocumentJsonEntry>();

        var content = this.serializer.SerializeToString(result.Value.Value);
        return Result<DocumentJsonEntry>.Success(new()
        {
            Content = content,
            Info = result.Value,
            Size = Encoding.UTF8.GetByteCount(content ?? string.Empty)
        }).WithMessages(result.Messages);
    }
    /// <inheritdoc />
    public async Task<Result> UpsertJsonAsync(DocumentKey key, string content, DocumentWriteOptions options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = this.serializer.Deserialize<T>(content ?? string.Empty);
            if (value is null) return Result.Failure(new ValidationError("Document content must deserialize to a non-null payload."));
            var result = await client.UpsertAsync(key, value, options, cancellationToken);
            return result.IsSuccess ? Result.Success().WithMessages(result.Messages) : Result.Failure().WithMessages(result.Messages);
        }
        catch (Exception ex)
        {
            return Result.Failure(new ValidationError($"Document content is invalid: {ex.Message}"));
        }
    }
    /// <inheritdoc />
    public Task<Result> DeleteAsync(DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default) => client.DeleteAsync(key, options, cancellationToken);
    /// <inheritdoc />
    public Task<Result<StoragePermalinkEntry>> GetPermalinkAsync(DocumentKey key, StoragePermalinkCreateOptions options = null, CancellationToken cancellationToken = default) => client.GetPermalinkAsync(key, options, cancellationToken);
}
