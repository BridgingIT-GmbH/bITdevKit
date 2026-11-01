// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.Web.Auditing;

using System.Globalization;
using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;

/// <summary>Shared bounded review controls for dashboard and host-owned history routes, independent of Dashboard registration.</summary>
/// <example><code>var filter = AuditReviewFilter.FromRequest(http);</code></example>
public static class AuditReviewFilter
{
    /// <summary>Combines approved FilterModel JSON with review controls using outer AND groups. Business keys use substring matching.</summary>
    /// <example><code>var model = await ResourceHistoryModel.LoadAsync(queries, resource, AuditReviewFilter.FromRequest(http));</code></example>
    public static FilterModel FromRequest(HttpContext http)
    {
        var filter = AuditHttpHandlers.QueryFilter(http) ?? new();
        var selected = new List<FilterCriteria>();
        if (filter.Filters?.Count > 0) selected.Add(new() { Filters = filter.Filters });
        void Scalar(string query, string field, FilterOperator operation = FilterOperator.Equal)
        {
            var text = http.Request.Query[query].ToString();
            if (text.Length > 256) throw new AuditExportException("AuditFilterInvalid");
            if (text.Length > 0) selected.Add(new(field, operation, text));
        }

        Scalar("key", "Key", FilterOperator.Contains); Scalar("module", "ModuleName"); Scalar("outcome", "Outcome");
        Scalar("actor", http.Request.Query["actorRole"] == "initiating" ? "InitiatingUserId" : "UserId");
        foreach (var (query, operation) in new[] { ("fromUtc", FilterOperator.GreaterThanOrEqual), ("toUtc", FilterOperator.LessThan) })
        {
            var text = http.Request.Query[query].ToString();
            if (text.Length == 0) continue;
            if (text.Length > 64 || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time)) throw new AuditExportException("AuditFilterInvalid");
            selected.Add(new("StartedUtc", operation, time));
        }

        var lifecycle = http.Request.Query["lifecycle"].ToString();
        if (lifecycle == "All") selected.Add(new("StorageState", FilterOperator.IsNotNull, null));
        else if (lifecycle is "Active" or "Archived") selected.Add(new("StorageState", FilterOperator.Equal, lifecycle));
        else if (lifecycle.Length > 0) throw new AuditExportException("AuditFilterInvalid");
        filter.Filters = selected;
        if (int.TryParse(http.Request.Query["page"], out var page)) filter.Page = page;
        if (int.TryParse(http.Request.Query["pageSize"], out var size)) filter.PageSize = size;
        return filter;
    }
}
