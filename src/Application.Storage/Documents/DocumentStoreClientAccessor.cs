// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using BridgingIT.DevKit.Common;

/// <summary>
/// Adapts a typed <see cref="IDocumentStoreClient{T}" /> for dashboard selection and server rendering.
/// </summary>
/// <typeparam name="T">The document type handled by the selected client.</typeparam>
/// <param name="descriptor">The selected client descriptor.</param>
/// <param name="client">The typed document-store client.</param>
/// <param name="serializer">The serializer used to render and parse document payload JSON.</param>
/// <example>
/// <code>
/// var accessor = new DocumentStoreClientAccessor&lt;Person&gt;(descriptor, client, serializer);
/// var json = await accessor.GetJsonResultAsync(new DocumentKey("people", "person-1"), ct);
/// </code>
/// </example>
public sealed class DocumentStoreClientAccessor<T>(
    DocumentStoreClientDescriptor descriptor,
    IDocumentStoreClient<T> client,
    ISerializer serializer = null) : IDocumentStoreClientAccessor
    where T : class, new()
{
    private readonly ISerializer serializer = serializer ?? new SystemTextJsonSerializer();

    /// <inheritdoc />
    public DocumentStoreClientDescriptor Descriptor { get; } = descriptor;

    /// <inheritdoc />
    public Task<Result<DocumentKeyPage>> ListPageResultAsync(
        DocumentQuery query,
        CancellationToken cancellationToken = default) =>
        client.ListPageResultAsync(query, cancellationToken);

    /// <inheritdoc />
    public Task<Result<long>> CountResultAsync(
        DocumentCountQuery query,
        CancellationToken cancellationToken = default) =>
        client.CountResultAsync(query, cancellationToken);

    /// <inheritdoc />
    public async Task<Result<string>> GetJsonResultAsync(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
    {
        var result = await client.GetResultAsync(documentKey, cancellationToken);
        if (result.IsFailure)
        {
            return result.Wrap<string>();
        }

        return Result<string>.Success(this.serializer.SerializeToString(result.Value))
            .WithMessages(result.Messages);
    }

    /// <inheritdoc />
    public async Task<Result> UpsertJsonResultAsync(
        DocumentKey documentKey,
        string content,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = this.serializer.Deserialize<T>(content ?? string.Empty);
            if (entity is null)
            {
                return Result.Failure(new ValidationError("Document content must deserialize to a non-null payload."));
            }

            return await client.UpsertResultAsync(documentKey, entity, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure(new ValidationError($"Document content is not valid for {typeof(T).PrettyName()}: {ex.Message}"));
        }
    }

    /// <inheritdoc />
    public Task<Result> DeleteResultAsync(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default) =>
        client.DeleteResultAsync(documentKey, cancellationToken);
}
