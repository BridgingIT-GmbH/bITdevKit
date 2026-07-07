// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Represents the default client implementation for storing and retrieving documents through an <see cref="IDocumentStoreProvider" />.
/// </summary>
/// <typeparam name="T">The type of documents managed by this client.</typeparam>
/// <example>
/// <code>
/// var client = new DocumentStoreClient&lt;Person&gt;(provider);
/// await client.UpsertResultAsync(new DocumentKey("people", "42"), person, cancellationToken);
/// var result = await client.GetResultAsync(new DocumentKey("people", "42"), cancellationToken);
/// </code>
/// </example>
public class DocumentStoreClient<T> : IDocumentStoreClient<T>
    where T : class, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentStoreClient{T}" /> class.
    /// </summary>
    /// <param name="provider">The provider used to execute document-store operations.</param>
    /// <example>
    /// <code>
    /// var client = new DocumentStoreClient&lt;Person&gt;(provider);
    /// </code>
    /// </example>
    public DocumentStoreClient(IDocumentStoreProvider provider)
    {
        EnsureArg.IsNotNull(provider, nameof(provider));

        this.Provider = provider;
    }

    /// <summary>
    /// Gets the provider used to execute document-store operations.
    /// </summary>
    protected IDocumentStoreProvider Provider { get; }

    /// <inheritdoc />
    public Task<Result<T>> GetResultAsync(DocumentKey documentKey, CancellationToken cancellationToken = default) =>
        this.Provider.GetResultAsync<T>(documentKey, cancellationToken);

    /// <inheritdoc />
    public Task<Result<DocumentPage<T>>> FindPageResultAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        this.Provider.FindPageResultAsync<T>(query, cancellationToken);

    /// <inheritdoc />
    public Task<Result<DocumentKeyPage>> ListPageResultAsync(DocumentQuery query, CancellationToken cancellationToken = default) =>
        this.Provider.ListPageResultAsync<T>(query, cancellationToken);

    /// <inheritdoc />
    public Task<Result<long>> CountResultAsync(DocumentCountQuery query, CancellationToken cancellationToken = default) =>
        this.Provider.CountResultAsync<T>(query, cancellationToken);

    /// <inheritdoc />
    public Task<Result<bool>> ExistsResultAsync(DocumentKey documentKey, CancellationToken cancellationToken = default) =>
        this.Provider.ExistsResultAsync<T>(documentKey, cancellationToken);

    /// <inheritdoc />
    public Task<Result> UpsertResultAsync(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default) =>
        this.Provider.UpsertResultAsync(documentKey, entity, cancellationToken);

    /// <inheritdoc />
    public Task<Result> UpsertResultAsync(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default) =>
        this.Provider.UpsertResultAsync(entities, cancellationToken);

    /// <inheritdoc />
    public Task<Result> DeleteResultAsync(DocumentKey documentKey, CancellationToken cancellationToken = default) =>
        this.Provider.DeleteResultAsync<T>(documentKey, cancellationToken);
}
