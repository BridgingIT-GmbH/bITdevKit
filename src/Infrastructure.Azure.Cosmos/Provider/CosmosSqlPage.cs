// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

/// <summary>
/// Represents one Cosmos SQL feed page.
/// </summary>
/// <typeparam name="TItem">The item type returned by the Cosmos query.</typeparam>
/// <example>
/// <code>
/// var page = await provider.ReadItemsPageResultAsync(take: 100, cancellationToken: cancellationToken);
/// </code>
/// </example>
public sealed class CosmosSqlPage<TItem>
{
    /// <summary>
    /// Gets the items read from the feed page.
    /// </summary>
    public IReadOnlyCollection<TItem> Items { get; init; } = [];

    /// <summary>
    /// Gets the provider-native Cosmos continuation token for the next feed page.
    /// </summary>
    public string ContinuationToken { get; init; }

    /// <summary>
    /// Gets the request charge consumed by this feed page.
    /// </summary>
    public double RequestCharge { get; init; }

    /// <summary>
    /// Gets the Cosmos activity identifier for this feed page.
    /// </summary>
    public string ActivityId { get; init; }
}
