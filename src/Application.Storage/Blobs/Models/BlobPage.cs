// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Represents one page of blob information returned from listing.
/// </summary>
/// <example>
/// <code>
/// foreach (var item in page.Items)
/// {
///     var name = item.Key.Name;
/// }
/// </code>
/// </example>
public sealed class BlobPage
{
    /// <summary>
    /// Gets the provider-neutral blob information for this page.
    /// </summary>
    /// <example>
    /// <code>
    /// var count = page.Items.Count;
    /// </code>
    /// </example>
    public IReadOnlyCollection<BlobInfo> Items { get; init; } = [];

    /// <summary>
    /// Gets the opaque continuation token when more results exist.
    /// </summary>
    /// <example>
    /// <code>
    /// var next = page.ContinuationToken;
    /// </code>
    /// </example>
    public string ContinuationToken { get; init; }

    /// <summary>
    /// Gets a value indicating whether the page has a continuation token.
    /// </summary>
    /// <example>
    /// <code>
    /// if (page.HasMore)
    /// {
    ///     // request the next page
    /// }
    /// </code>
    /// </example>
    public bool HasMore => !string.IsNullOrWhiteSpace(this.ContinuationToken);
}
