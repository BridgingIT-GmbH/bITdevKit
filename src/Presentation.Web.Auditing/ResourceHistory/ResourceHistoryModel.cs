// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.Web.Auditing;

using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;

/// <summary>Host-owned resource history presentation, independent of Dashboard and public audit routes.</summary>
/// <example><code>var model = await ResourceHistoryModel.LoadAsync(queries, new("Customer", customerId), filter, cancellationToken: cancellationToken);</code></example>
public sealed record ResourceHistoryModel
{
    /// <summary>Shows UTC, archive and page-size controls when the host binds AuditReviewFilter.FromRequest on the current route.</summary>
    /// <example><code>var interactive = model with { ShowFilters = true };</code></example>
    public bool ShowFilters { get; init; }

    /// <summary>The mandatory exact typed resource selected by the host.</summary>
    /// <example><code>var resource = model.Resource;</code></example>
    public AuditResourceReference Resource { get; init; }

    /// <summary>Scoped bounded history, including explicit provider failures.</summary>
    /// <example><code>var page = model.History;</code></example>
    public ResultPaged<AuditResourceHistoryItem> History { get; init; }

    /// <summary>Optional host-owned links. Only local paths or HTTP(S) destinations are rendered.</summary>
    /// <example><code>var model = original with { DetailLink = row => $"/customers/history/{row.AuditId}" };</code></example>
    public Func<AuditRecordSummary, string> DetailLink { get; init; }

    /// <summary>Reads under current trusted constraints; suppression is limited to the query itself.</summary>
    /// <example><code>var model = await ResourceHistoryModel.LoadAsync(queries, new("Customer", "C-42"));</code></example>
    public static async Task<ResourceHistoryModel> LoadAsync(IAuditQueryService queries, AuditResourceReference resource, FilterModel filter = null, Func<AuditRecordSummary, string> detailLink = null, CancellationToken cancellationToken = default)
        => new() { Resource = resource, History = await queries.GetResourceHistoryAsync(resource, filter, cancellationToken).ConfigureAwait(false), DetailLink = detailLink };

    /// <summary>Gets a safe host link without accepting executable URI schemes or protocol-relative destinations.</summary>
    /// <example><code>var link = model.GetDetailLink(item.Record);</code></example>
    public string GetDetailLink(AuditRecordSummary record)
    {
        try
        {
            var link = this.DetailLink?.Invoke(record);
            if (link?.Length > 2048 || link?.Any(char.IsControl) == true) return null;
            if (link?.StartsWith('/') == true && !link.StartsWith("//", StringComparison.Ordinal) && !link.Contains('\\')) return link;
            return Uri.TryCreate(link, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" ? link : null;
        }
        catch (Exception) { return null; }
    }
}
