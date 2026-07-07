// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Describes a document-store count query.
/// </summary>
/// <example>
/// <code>
/// var query = DocumentQueries.Count()
///     .ForKey("people", "DE-")
///     .WithRowKeyPrefix()
///     .Build();
/// </code>
/// </example>
public sealed class DocumentCountQuery
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
    /// Gets a value indicating whether a type-wide scan count is explicitly approved for this query.
    /// </summary>
    public bool AllowFullScan { get; init; }
}
