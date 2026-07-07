// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Describes the query capabilities of a document-store provider.
/// </summary>
public sealed class DocumentStoreProviderCapabilities
{
    /// <summary>
    /// Gets or sets support for exact key matching.
    /// </summary>
    public DocumentQuerySupport FullMatch { get; init; } = DocumentQuerySupport.Unsupported;

    /// <summary>
    /// Gets or sets support for row-key prefix matching.
    /// </summary>
    public DocumentQuerySupport RowKeyPrefixMatch { get; init; } = DocumentQuerySupport.Unsupported;

    /// <summary>
    /// Gets or sets support for row-key suffix matching.
    /// </summary>
    public DocumentQuerySupport RowKeySuffixMatch { get; init; } = DocumentQuerySupport.Unsupported;

    /// <summary>
    /// Gets or sets support for type-wide scans.
    /// </summary>
    public DocumentQuerySupport FullScan { get; init; } = DocumentQuerySupport.Unsupported;

    /// <summary>
    /// Gets or sets support for key-only listing.
    /// </summary>
    public DocumentQuerySupport KeyListing { get; init; } = DocumentQuerySupport.Unsupported;

    /// <summary>
    /// Gets or sets a value indicating whether continuation paging is supported.
    /// </summary>
    public bool SupportsContinuationPaging { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether count can be performed server-side.
    /// </summary>
    public bool SupportsServerSideCount { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether key-only projections avoid payload materialization.
    /// </summary>
    public bool SupportsKeyOnlyProjection { get; init; }
}
