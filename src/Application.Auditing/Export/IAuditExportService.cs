// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Auditing;

using BridgingIT.DevKit.Common;

/// <summary>Validates current selection and access before opening a bounded persisted source window.</summary>
/// <example><code>var opened = await exports.OpenAsync(new() { Filter = filter, Format = "ndjson" }, cancellationToken);</code></example>
public interface IAuditExportService
{
    /// <summary>Opens one owned attempt without buffering the result set or implicitly flushing pending source writers.</summary>
    /// <example><code>await using var session = (await exports.OpenAsync(request)).Value;</code></example>
    Task<Result<AuditExportSession>> OpenAsync(AuditExportRequest request = null, CancellationToken cancellationToken = default);
}
