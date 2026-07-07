// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Builds <see cref="DocumentQuery" /> instances without executing them.
/// </summary>
/// <example>
/// <code>
/// var query = DocumentQueryBuilder.Create()
///     .ForKey("people", "DE-")
///     .WithRowKeyPrefix()
///     .Take(100)
///     .Build();
/// </code>
/// </example>
public sealed class DocumentQueryBuilder
{
    private DocumentKey? documentKey;
    private DocumentKeyFilter filter = DocumentKeyFilter.FullMatch;
    private int? take;
    private string continuationToken;
    private bool allowFullScan;

    private DocumentQueryBuilder() { }

    /// <summary>
    /// Creates a new query builder.
    /// </summary>
    /// <returns>A new document query builder.</returns>
    /// <example>
    /// <code>
    /// var builder = DocumentQueryBuilder.Create();
    /// </code>
    /// </example>
    public static DocumentQueryBuilder Create() => new();

    /// <summary>
    /// Sets the query key.
    /// </summary>
    /// <param name="documentKey">The partition and row key to filter by.</param>
    /// <returns>The current builder instance.</returns>
    /// <example>
    /// <code>
    /// var query = DocumentQueries.Query()
    ///     .ForKey(new DocumentKey("people", "42"))
    ///     .Build();
    /// </code>
    /// </example>
    public DocumentQueryBuilder ForKey(DocumentKey documentKey)
    {
        if (documentKey == default)
        {
            throw new ArgumentException("Document key must not be default.", nameof(documentKey));
        }

        this.documentKey = documentKey;
        return this;
    }

    /// <summary>
    /// Sets the query key.
    /// </summary>
    /// <param name="partitionKey">The partition key to filter by.</param>
    /// <param name="rowKey">The row key, prefix, or suffix to filter by depending on the selected filter.</param>
    /// <returns>The current builder instance.</returns>
    /// <example>
    /// <code>
    /// var query = DocumentQueries.Query()
    ///     .ForKey("people", "DE-")
    ///     .WithRowKeyPrefix()
    ///     .Build();
    /// </code>
    /// </example>
    public DocumentQueryBuilder ForKey(string partitionKey, string rowKey) =>
        this.ForKey(new DocumentKey(partitionKey, rowKey));

    /// <summary>
    /// Uses exact row-key matching.
    /// </summary>
    /// <returns>The current builder instance.</returns>
    /// <example>
    /// <code>
    /// var query = DocumentQueries.Query()
    ///     .ForKey("people", "42")
    ///     .WithFullMatch()
    ///     .Build();
    /// </code>
    /// </example>
    public DocumentQueryBuilder WithFullMatch()
    {
        this.filter = DocumentKeyFilter.FullMatch;
        return this;
    }

    /// <summary>
    /// Uses row-key prefix matching.
    /// </summary>
    /// <returns>The current builder instance.</returns>
    /// <example>
    /// <code>
    /// var query = DocumentQueries.Query()
    ///     .ForKey("people", "DE-")
    ///     .WithRowKeyPrefix()
    ///     .Build();
    /// </code>
    /// </example>
    public DocumentQueryBuilder WithRowKeyPrefix()
    {
        this.filter = DocumentKeyFilter.RowKeyPrefixMatch;
        return this;
    }

    /// <summary>
    /// Uses row-key suffix matching.
    /// </summary>
    /// <returns>The current builder instance.</returns>
    /// <example>
    /// <code>
    /// var query = DocumentQueries.Query()
    ///     .ForKey("people", "-2026")
    ///     .WithRowKeySuffix()
    ///     .Build();
    /// </code>
    /// </example>
    public DocumentQueryBuilder WithRowKeySuffix()
    {
        this.filter = DocumentKeyFilter.RowKeySuffixMatch;
        return this;
    }

    /// <summary>
    /// Sets the page size.
    /// </summary>
    /// <param name="take">The maximum number of documents or keys to request.</param>
    /// <returns>The current builder instance.</returns>
    /// <example>
    /// <code>
    /// var query = DocumentQueries.Query()
    ///     .ForKey("people", "DE-")
    ///     .WithRowKeyPrefix()
    ///     .Take(50)
    ///     .Build();
    /// </code>
    /// </example>
    public DocumentQueryBuilder Take(int take)
    {
        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "Take must be greater than zero.");
        }

        this.take = take;
        return this;
    }

    /// <summary>
    /// Sets the continuation token from a previous page.
    /// </summary>
    /// <param name="continuationToken">The continuation token returned by the previous page.</param>
    /// <returns>The current builder instance.</returns>
    /// <example>
    /// <code>
    /// var nextQuery = DocumentQueries.Query()
    ///     .ForKey("people", "DE-")
    ///     .WithRowKeyPrefix()
    ///     .Take(50)
    ///     .ContinueWith(firstPage.Value.ContinuationToken)
    ///     .Build();
    /// </code>
    /// </example>
    public DocumentQueryBuilder ContinueWith(string continuationToken)
    {
        if (string.IsNullOrWhiteSpace(continuationToken))
        {
            throw new ArgumentException("Continuation token must not be null or whitespace.", nameof(continuationToken));
        }

        this.continuationToken = continuationToken;
        return this;
    }

    /// <summary>
    /// Allows this query shape to be interpreted as a full scan.
    /// </summary>
    /// <returns>The current builder instance.</returns>
    /// <example>
    /// <code>
    /// var query = DocumentQueries.Query()
    ///     .AllowFullScan()
    ///     .Take(100)
    ///     .Build();
    /// </code>
    /// </example>
    public DocumentQueryBuilder AllowFullScan()
    {
        this.allowFullScan = true;
        return this;
    }

    /// <summary>
    /// Builds the query model.
    /// </summary>
    /// <returns>The immutable query model.</returns>
    /// <example>
    /// <code>
    /// var query = DocumentQueries.Query()
    ///     .ForKey("people", "42")
    ///     .Build();
    /// </code>
    /// </example>
    public DocumentQuery Build() => new()
    {
        DocumentKey = this.documentKey,
        Filter = this.filter,
        Take = this.take,
        ContinuationToken = this.continuationToken,
        AllowFullScan = this.allowFullScan
    };
}
