// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Builds <see cref="DocumentCountQuery" /> instances without executing them.
/// </summary>
/// <example>
/// <code>
/// var query = DocumentCountQueryBuilder.Create()
///     .ForKey("people", "DE-")
///     .WithRowKeyPrefix()
///     .Build();
/// </code>
/// </example>
public sealed class DocumentCountQueryBuilder
{
    private DocumentKey? documentKey;
    private DocumentKeyFilter filter = DocumentKeyFilter.FullMatch;
    private bool allowFullScan;

    private DocumentCountQueryBuilder() { }

    /// <summary>
    /// Creates a new count query builder.
    /// </summary>
    /// <returns>A new document count query builder.</returns>
    /// <example>
    /// <code>
    /// var builder = DocumentCountQueryBuilder.Create();
    /// </code>
    /// </example>
    public static DocumentCountQueryBuilder Create() => new();

    /// <summary>
    /// Sets the query key.
    /// </summary>
    /// <param name="documentKey">The partition and row key to filter by.</param>
    /// <returns>The current builder instance.</returns>
    /// <example>
    /// <code>
    /// var query = DocumentQueries.Count()
    ///     .ForKey(new DocumentKey("people", "42"))
    ///     .Build();
    /// </code>
    /// </example>
    public DocumentCountQueryBuilder ForKey(DocumentKey documentKey)
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
    /// var query = DocumentQueries.Count()
    ///     .ForKey("people", "DE-")
    ///     .WithRowKeyPrefix()
    ///     .Build();
    /// </code>
    /// </example>
    public DocumentCountQueryBuilder ForKey(string partitionKey, string rowKey) =>
        this.ForKey(new DocumentKey(partitionKey, rowKey));

    /// <summary>
    /// Uses exact row-key matching.
    /// </summary>
    /// <returns>The current builder instance.</returns>
    /// <example>
    /// <code>
    /// var query = DocumentQueries.Count()
    ///     .ForKey("people", "42")
    ///     .WithFullMatch()
    ///     .Build();
    /// </code>
    /// </example>
    public DocumentCountQueryBuilder WithFullMatch()
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
    /// var query = DocumentQueries.Count()
    ///     .ForKey("people", "DE-")
    ///     .WithRowKeyPrefix()
    ///     .Build();
    /// </code>
    /// </example>
    public DocumentCountQueryBuilder WithRowKeyPrefix()
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
    /// var query = DocumentQueries.Count()
    ///     .ForKey("people", "-2026")
    ///     .WithRowKeySuffix()
    ///     .Build();
    /// </code>
    /// </example>
    public DocumentCountQueryBuilder WithRowKeySuffix()
    {
        this.filter = DocumentKeyFilter.RowKeySuffixMatch;
        return this;
    }

    /// <summary>
    /// Allows this count query shape to be interpreted as a full scan.
    /// </summary>
    /// <returns>The current builder instance.</returns>
    /// <example>
    /// <code>
    /// var query = DocumentQueries.Count()
    ///     .AllowFullScan()
    ///     .Build();
    /// </code>
    /// </example>
    public DocumentCountQueryBuilder AllowFullScan()
    {
        this.allowFullScan = true;
        return this;
    }

    /// <summary>
    /// Builds the count query model.
    /// </summary>
    /// <returns>The immutable count query model.</returns>
    /// <example>
    /// <code>
    /// var query = DocumentQueries.Count()
    ///     .ForKey("people", "42")
    ///     .Build();
    /// </code>
    /// </example>
    public DocumentCountQuery Build() => new()
    {
        DocumentKey = this.documentKey,
        Filter = this.filter,
        AllowFullScan = this.allowFullScan
    };
}
