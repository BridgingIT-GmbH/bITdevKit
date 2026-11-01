// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Auditing;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;

/// <summary>Selects one bounded source extraction. HTTP callers continue to submit FilterModel directly.</summary>
/// <example><code>var value = new AuditExportRequest();</code></example>
public sealed record AuditExportRequest
{
    /// <summary>Optional trusted host-issued attempt identity. Public HTTP callers cannot set it directly.</summary>
    /// <example><code>var request = new AuditExportRequest { ExportId = protectedTicket.ExportId };</code></example>
    public Guid? ExportId { get; init; }

    /// <summary>The existing bounded business selection; paging never limits the exported selection.</summary>
    /// <example><code>var value = snapshot.Filter;</code></example>
    public FilterModel Filter { get; init; }

    /// <summary>A registered format identifier.</summary>
    /// <example><code>var value = snapshot.Format;</code></example>
    public string Format { get; init; } = "ndjson";

    /// <summary>Previously protected or trusted progress, revalidated against current scope and exposure.</summary>
    /// <example><code>var value = snapshot.Resume;</code></example>
    public AuditExportResume Resume { get; init; }
}
