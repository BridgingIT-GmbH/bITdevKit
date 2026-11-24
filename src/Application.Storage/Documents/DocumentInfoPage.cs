// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>Represents one page of matching document metadata.</summary>
/// <example><code>var next = page.ContinuationToken;</code></example>
public sealed class DocumentInfoPage
{
    /// <summary>Gets the metadata for matching documents in this page.</summary>
    public IReadOnlyCollection<DocumentInfo> Items { get; init; } = [];

    /// <summary>Gets the opaque token for the next page, or null at the end.</summary>
    public string ContinuationToken { get; init; }

    /// <summary>Gets whether a continuation token is available.</summary>
    public bool HasMore => !string.IsNullOrWhiteSpace(this.ContinuationToken);
}
