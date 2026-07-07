// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Represents one bounded page of document payload instances.
/// </summary>
/// <typeparam name="T">The document payload type.</typeparam>
/// <example>
/// <code>
/// var page = await documents.FindPageResultAsync(query, cancellationToken);
/// if (page.Value.HasMore)
/// {
///     var nextQuery = DocumentQueries.Query()
///         .ContinueWith(page.Value.ContinuationToken)
///         .Build();
/// }
/// </code>
/// </example>
public sealed class DocumentPage<T>
{
    /// <summary>
    /// Gets the payload instances returned by this page.
    /// </summary>
    public IReadOnlyCollection<T> Items { get; init; } = [];

    /// <summary>
    /// Gets the opaque token used to retrieve the next page.
    /// </summary>
    public string ContinuationToken { get; init; }

    /// <summary>
    /// Gets a value indicating whether another page is available.
    /// </summary>
    public bool HasMore => !string.IsNullOrWhiteSpace(this.ContinuationToken);
}
