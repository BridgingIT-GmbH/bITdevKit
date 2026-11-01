// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.Web.Auditing;

using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;

/// <summary>Bounded business-first dashboard data; errors remain distinct from empty results.</summary>
/// <example><code>var model = new AuditingViewModel { Path = "/operations/auditing", Records = result };</code></example>
public sealed record AuditingViewModel
{
    /// <summary>Whether the view requested an exact-ID detail, including failed reads.</summary>
    /// <example><code>var detailed = model.HasDetail;</code></example>
    public bool HasDetail { get; init; }

    /// <summary>Host-configured dashboard audit path.</summary>
    /// <example><code>var path = model.Path;</code></example>
    public string Path { get; init; }

    /// <summary>Current scoped live search.</summary>
    /// <example><code>var records = model.Records;</code></example>
    public ResultPaged<AuditRecordSummary> Records { get; init; }

    /// <summary>Verified permitted exact-ID detail when selected.</summary>
    /// <example><code>var details = model.Details;</code></example>
    public Result<AuditRecordDetails> Details { get; init; }

    /// <summary>Explicitly related scoped executions.</summary>
    /// <example><code>var related = model.Related;</code></example>
    public ResultPaged<AuditRecordSummary> Related { get; init; }

    /// <summary>Bounded standard filter JSON used by the native download form.</summary>
    /// <example><code>var filter = model.FilterJson;</code></example>
    public string FilterJson { get; init; } = "{}";

    /// <summary>Protected one-attempt download admission ticket.</summary>
    /// <example><code>var ticket = model.DownloadTicket;</code></example>
    public string DownloadTicket { get; init; }

    /// <summary>Server-issued identity used for companion polling.</summary>
    /// <example><code>var id = model.ExportId;</code></example>
    public Guid ExportId { get; init; }

    /// <summary>Registered format identifiers, without activating another feature.</summary>
    /// <example><code>var formats = model.Formats;</code></example>
    public string[] Formats { get; init; } = [];

    /// <summary>Read-only process diagnostics when requested.</summary>
    /// <example><code>var snapshot = model.Diagnostics;</code></example>
    public AuditDiagnosticsSnapshot Diagnostics { get; init; }
}
