// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Describes a bounded document-store query for payload or key page reads.
/// </summary>
/// <example>
/// <code>
/// var query = DocumentQueries.Query()
///     .ForKey("people", "DE-")
///     .WithRowKeyPrefix()
///     .Take(100)
///     .Build();
/// </code>
/// </example>
public sealed class DocumentQuery
{
    /// <summary>
    /// Gets the optional partition and row key seed for the query.
    /// </summary>
    public DocumentKey? DocumentKey { get; init; }

    /// <summary>
    /// Gets the row-key filter semantics for <see cref="DocumentKey" />.
    /// </summary>
    public DocumentKeyFilter Filter { get; init; } = DocumentKeyFilter.FullMatch;

    /// <summary>
    /// Gets the maximum number of items to return.
    /// </summary>
    public int? Take { get; init; }

    /// <summary>
    /// Gets the opaque continuation token from a previous page.
    /// </summary>
    public string ContinuationToken { get; init; }

    /// <summary>
    /// Gets a value indicating whether a type-wide scan is explicitly approved for this query.
    /// </summary>
    public bool AllowFullScan { get; init; }
}
