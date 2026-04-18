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
/// await client.UpsertAsync(new DocumentKey("people", "42"), person, cancellationToken);
/// var people = await client.FindAsync(new DocumentKey("people", "4"), DocumentKeyFilter.RowKeyPrefixMatch, cancellationToken);
/// </code>
/// </example>
public class DocumentStoreClient<T> : IDocumentStoreClient<T>
    where T : class, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentStoreClient{T}" /> class.
    /// </summary>
    /// <param name="provider">The provider that executes the underlying document-store operations.</param>
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
    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await this.Provider.CountAsync<T>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        await this.Provider.DeleteAsync<T>(documentKey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        return await this.Provider.ExistsAsync<T>(documentKey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> FindAsync(CancellationToken cancellationToken = default)
    {
        return await this.Provider.FindAsync<T>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> FindAsync(DocumentKey documentKey, CancellationToken cancellationToken = default)
    {
        return await this.Provider.FindAsync<T>(documentKey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<T>> FindAsync(
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default)
    {
        return await this.Provider.FindAsync<T>(documentKey, filter, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DocumentKey>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await this.Provider.ListAsync<T>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DocumentKey>> ListAsync(
        DocumentKey documentKey,
        CancellationToken cancellationToken = default)
    {
        return await this.Provider.ListAsync<T>(documentKey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DocumentKey>> ListAsync(
        DocumentKey documentKey,
        DocumentKeyFilter filter,
        CancellationToken cancellationToken = default)
    {
        return await this.Provider.ListAsync<T>(documentKey, filter, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpsertAsync(DocumentKey documentKey, T entity, CancellationToken cancellationToken = default)
    {
        await this.Provider.UpsertAsync(documentKey, entity, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpsertAsync(
        IEnumerable<(DocumentKey DocumentKey, T Entity)> entities,
        CancellationToken cancellationToken = default)
    {
        await this.Provider.UpsertAsync(entities, cancellationToken);
    }
}
