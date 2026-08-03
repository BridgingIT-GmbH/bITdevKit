// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Describes a provider-neutral blob listing query.
/// </summary>
/// <example>
/// <code>
/// var query = new BlobQuery
/// {
///     Container = "reports",
///     Prefix = "2026/06/",
///     Take = 100
/// };
/// </code>
/// </example>
public sealed class BlobQuery
{
    /// <summary>
    /// Gets the container to list.
    /// </summary>
    /// <example>
    /// <code>
    /// var container = query.Container;
    /// </code>
    /// </example>
    public string Container { get; init; }

    /// <summary>
    /// Gets the optional name prefix used to constrain listing.
    /// </summary>
    /// <example>
    /// <code>
    /// var prefix = query.Prefix;
    /// </code>
    /// </example>
    public string Prefix { get; init; }

    /// <summary>
    /// Gets the requested page size when supplied.
    /// </summary>
    /// <example>
    /// <code>
    /// var take = query.Take;
    /// </code>
    /// </example>
    public int? Take { get; init; }

    /// <summary>
    /// Gets the opaque continuation token when supplied.
    /// </summary>
    /// <example>
    /// <code>
    /// var continuationToken = query.ContinuationToken;
    /// </code>
    /// </example>
    public string ContinuationToken { get; init; }

    /// <summary>
    /// Gets a value indicating whether this query explicitly allows a full container scan.
    /// </summary>
    /// <example>
    /// <code>
    /// var allowFullScan = query.AllowFullScan;
    /// </code>
    /// </example>
    public bool AllowFullScan { get; init; }
}
