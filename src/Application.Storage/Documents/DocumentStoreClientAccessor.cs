// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

using System.Text;

/// <summary>
/// Adapts a typed document client for operational endpoints and dashboards.
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
    : IDocumentStoreClientAccessor, IDocumentDimensionDiagnosticsAccessor where T : class, new()
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
        var result = await this.UpsertEntryJsonAsync(key, content, options, cancellationToken);
        return result.IsSuccess ? Result.Success().WithMessages(result.Messages) : Result.Failure(result.Messages, result.Errors);
    }

    /// <inheritdoc />
    public async Task<Result<DocumentInfo>> UpsertEntryJsonAsync(DocumentKey key, string content, DocumentWriteOptions options = null, CancellationToken cancellationToken = default)
    {
        T value;
        try
        {
            value = this.serializer.Deserialize<T>(content ?? string.Empty);
            if (value is null) return Result<DocumentInfo>.Failure(new ValidationError("Document content must deserialize to a non-null payload."));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Result<DocumentInfo>.Failure(new ValidationError($"Document content is invalid: {ex.Message}"));
        }

        return await client.UpsertAsync(key, value, options, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<DocumentDimensionDiagnostics>> GetDimensionDiagnosticsAsync(bool includeDictionaryCount = false, CancellationToken cancellationToken = default) =>
        client is IDocumentStoreProviderAccessor { Provider: IDocumentDimensionDiagnosticsProvider diagnostics }
            ? diagnostics.GetDimensionDiagnosticsAsync(this.Descriptor.TypeIdentity, includeDictionaryCount, cancellationToken)
            : Task.FromResult(Result<DocumentDimensionDiagnostics>.Failure(new DocumentDimensionsNotSupportedError()));

    /// <inheritdoc />
    public Task<Result<DocumentInfo>> UpdateDimensionsAsync(DocumentDimensionsUpdate update, CancellationToken cancellationToken = default) => client.UpdateDimensionsAsync(update, cancellationToken);

    /// <inheritdoc />
    public Task<Result<DocumentInfo>> UpdatePropertiesAsync(DocumentPropertiesUpdate update, CancellationToken cancellationToken = default) => client.UpdatePropertiesAsync(update, cancellationToken);

    /// <inheritdoc />
    public Task<Result> DeleteAsync(DocumentKey key, DocumentDeleteOptions options = null, CancellationToken cancellationToken = default) => client.DeleteAsync(key, options, cancellationToken);
    /// <inheritdoc />
    public Task<Result<StoragePermalinkEntry>> GetPermalinkAsync(DocumentKey key, StoragePermalinkCreateOptions options = null, CancellationToken cancellationToken = default) => client.GetPermalinkAsync(key, options, cancellationToken);
}
