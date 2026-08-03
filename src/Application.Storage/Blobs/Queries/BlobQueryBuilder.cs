// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Builds <see cref="BlobQuery" /> instances without executing them.
/// </summary>
/// <example>
/// <code>
/// var query = BlobQueries.Query()
///     .InContainer("reports")
///     .WithPrefix("2026/06/")
///     .Take(50)
///     .Build();
/// </code>
/// </example>
public sealed class BlobQueryBuilder
{
    private string container;
    private string prefix;
    private int? take;
    private string continuationToken;
    private bool allowFullScan;

    private BlobQueryBuilder() { }

    /// <summary>
    /// Creates a new query builder.
    /// </summary>
    /// <returns>A new blob query builder.</returns>
    /// <example>
    /// <code>
    /// var builder = BlobQueryBuilder.Create();
    /// </code>
    /// </example>
    public static BlobQueryBuilder Create() => new();

    /// <summary>
    /// Sets the container to list.
    /// </summary>
    /// <param name="container">The container name.</param>
    /// <returns>The current builder instance.</returns>
    /// <example>
    /// <code>
    /// var query = BlobQueries.Query().InContainer("reports").Build();
    /// </code>
    /// </example>
    public BlobQueryBuilder InContainer(string container)
    {
        if (string.IsNullOrWhiteSpace(container))
        {
            throw new ArgumentException("Container must not be null or whitespace.", nameof(container));
        }

        this.container = container;
        return this;
    }

    /// <summary>
    /// Sets the optional blob name prefix.
    /// </summary>
    /// <param name="prefix">The blob name prefix. Empty prefixes are allowed and validate as full scans.</param>
    /// <returns>The current builder instance.</returns>
    /// <example>
    /// <code>
    /// var query = BlobQueries.Query().InContainer("reports").WithPrefix("2026/").Build();
    /// </code>
    /// </example>
    public BlobQueryBuilder WithPrefix(string prefix)
    {
        if (prefix is null)
        {
            throw new ArgumentNullException(nameof(prefix));
        }

        this.prefix = prefix;
        return this;
    }

    /// <summary>
    /// Sets the requested page size.
    /// </summary>
    /// <param name="take">The requested page size.</param>
    /// <returns>The current builder instance.</returns>
    /// <example>
    /// <code>
    /// var query = BlobQueries.Query().InContainer("reports").Take(25).Build();
    /// </code>
    /// </example>
    public BlobQueryBuilder Take(int take)
    {
        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "Take must be greater than zero.");
        }

        this.take = take;
        return this;
    }

    /// <summary>
    /// Sets the opaque continuation token from a previous page.
    /// </summary>
    /// <param name="continuationToken">The opaque continuation token.</param>
    /// <returns>The current builder instance.</returns>
    /// <example>
    /// <code>
    /// var query = BlobQueries.Query().InContainer("reports").ContinueWith(page.ContinuationToken).Build();
    /// </code>
    /// </example>
    public BlobQueryBuilder ContinueWith(string continuationToken)
    {
        if (string.IsNullOrWhiteSpace(continuationToken))
        {
            throw new ArgumentException("Continuation token must not be null or whitespace.", nameof(continuationToken));
        }

        this.continuationToken = continuationToken;
        return this;
    }

    /// <summary>
    /// Allows this query shape to be interpreted as a full container scan.
    /// </summary>
    /// <returns>The current builder instance.</returns>
    /// <example>
    /// <code>
    /// var query = BlobQueries.Query().InContainer("reports").AllowFullScan().Build();
    /// </code>
    /// </example>
    public BlobQueryBuilder AllowFullScan()
    {
        this.allowFullScan = true;
        return this;
    }

    /// <summary>
    /// Builds the query model.
    /// </summary>
    /// <returns>The query model.</returns>
    /// <example>
    /// <code>
    /// var query = BlobQueries.Query().InContainer("reports").Build();
    /// </code>
    /// </example>
    public BlobQuery Build() => new()
    {
        Container = this.container,
        Prefix = this.prefix,
        Take = this.take,
        ContinuationToken = this.continuationToken,
        AllowFullScan = this.allowFullScan
    };
}
