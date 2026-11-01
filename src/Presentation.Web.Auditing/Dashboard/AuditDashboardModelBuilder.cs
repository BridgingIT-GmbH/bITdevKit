// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.Web.Auditing;

using System.Globalization;
using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Builds scoped dashboard views and serializes the same approved selection for native downloads.</summary>
/// <example><code>var model = await AuditDashboardModelBuilder.BuildAsync(http, path);</code></example>
public static class AuditDashboardModelBuilder
{
    /// <summary>Builds one bounded search/detail/diagnostics view without enabling source capture.</summary>
    /// <example><code>var model = await AuditDashboardModelBuilder.BuildAsync(http, "/ops/auditing", auditId);</code></example>
    public static async Task<AuditingViewModel> BuildAsync(HttpContext http, string path, Guid? auditId = null, bool diagnostics = false)
    {
        if (diagnostics) return new() { Path = path, Diagnostics = await http.RequestServices.GetRequiredService<AuditDiagnosticsService>().GetSnapshotAsync(http.RequestAborted).ConfigureAwait(false) };
        var queries = http.RequestServices.GetRequiredService<IAuditQueryService>();
        var filter = auditId is { } id ? new FilterModel { Filters = [new("AuditId", FilterOperator.Equal, id), new("StorageState", FilterOperator.IsNotNull, null)] } : Filter(http);
        var ticket = await AuditDownloadHandlers.IssueAsync(http).ConfigureAwait(false);
        var exported = AuditFilterSnapshot.Copy(filter); exported.Page = 0; exported.Orderings = [];
        var model = new AuditingViewModel
        {
            Path = path, FilterJson = System.Text.Json.JsonSerializer.Serialize(exported, AuditFilterJson.CreateOptions()), DownloadTicket = ticket.Ticket, ExportId = ticket.ExportId,
            Formats = http.RequestServices.GetRequiredService<AuditExportRegistry>().Formatters.Select(item => item.Format).Order(StringComparer.Ordinal).ToArray()
        };
        if (auditId is { } exact)
        {
            var details = await queries.FindAsync(exact, http.RequestAborted).ConfigureAwait(false);
            return model with { HasDetail = true, Details = details, Related = details.IsSuccess ? await queries.FindRelatedAsync(exact, cancellationToken: http.RequestAborted).ConfigureAwait(false) : ResultPaged<AuditRecordSummary>.Failure(new AuditError("AuditReadUnavailable")) };
        }

        return model with { Records = await queries.SearchAsync(filter, http.RequestAborted).ConfigureAwait(false) };
    }

    /// <summary>Combines bounded advanced criteria with explicit business controls using outer AND groups.</summary>
    /// <example><code>var filter = AuditDashboardModelBuilder.Filter(http);</code></example>
    public static FilterModel Filter(HttpContext http) => AuditReviewFilter.FromRequest(http);
}
