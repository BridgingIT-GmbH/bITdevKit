// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Represents one bounded page of document keys.
/// </summary>
/// <example>
/// <code>
/// var page = await documents.ListPageResultAsync(query, cancellationToken);
/// foreach (var key in page.Value.Items)
/// {
///     var document = await documents.GetResultAsync(key, cancellationToken);
/// }
/// </code>
/// </example>
public sealed class DocumentKeyPage
{
    /// <summary>
    /// Gets the document keys returned by this page.
    /// </summary>
    public IReadOnlyCollection<DocumentKey> Items { get; init; } = [];

    /// <summary>
    /// Gets the opaque token used to retrieve the next page.
    /// </summary>
    public string ContinuationToken { get; init; }

    /// <summary>
    /// Gets a value indicating whether another page is available.
    /// </summary>
    public bool HasMore => !string.IsNullOrWhiteSpace(this.ContinuationToken);
}
