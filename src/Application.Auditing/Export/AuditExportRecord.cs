// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>Provides only a permitted projection and explicit original-document references to formatters.</summary>
/// <example><code>var value = new AuditExportRecord();</code></example>
public sealed record AuditExportRecord
{
    /// <summary>Stable source identity for receiver deduplication.</summary>
    /// <example><code>var value = snapshot.SourceStoreId;</code></example>
    public Guid SourceStoreId { get; init; }

    /// <summary>Committed source publication position.</summary>
    /// <example><code>var value = snapshot.Position;</code></example>
    public long? Position { get; init; }

    /// <summary>An original-document comparison reference, not independent authenticity proof.</summary>
    /// <example><code>var value = snapshot.OriginalContentHash;</code></example>
    public string OriginalContentHash { get; init; }

    /// <summary>The already verified, exposure-restricted graph with its source envelope omitted.</summary>
    /// <example><code>var value = snapshot.Details;</code></example>
    public AuditRecordDetails Details { get; init; }
}
