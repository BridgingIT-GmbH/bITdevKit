// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

using System.Text.Encodings.Web;
using BridgingIT.DevKit.Presentation.Web.Logging.Dashboard;
using Microsoft.AspNetCore.Html;

/// <summary>Renders a correlation identifier with a filtered logs link and a copy button.</summary>
/// <example><code>var link = DashboardCorrelationLink.Render(options, record.CorrelationId);</code></example>
public static class DashboardCorrelationLink
{
    /// <summary>Creates safely encoded correlation markup used by dashboard pages and refreshed fragments.</summary>
    /// <param name="options">The dashboard route configuration, or null to use the defaults.</param>
    /// <param name="correlationId">The full identifier displayed and copied.</param>
    /// <param name="shorten">Whether to shorten only the visible label.</param>
    /// <param name="href">An optional logs URL preserving a page's additional filters.</param>
    /// <returns>The logs link and copy control, or a placeholder when the identifier is missing.</returns>
    /// <example><code>var link = DashboardCorrelationLink.Render(options, entry.CorrelationId, shorten: true);</code></example>
    public static IHtmlContent Render(
        DashboardEndpointsOptions options,
        string correlationId,
        bool shorten = false,
        string href = null
    )
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return new HtmlString("<span class=\"text-muted\">—</span>");
        }

        var encoder = HtmlEncoder.Default;
        var identifier = encoder.Encode(correlationId);
        var label = encoder.Encode(
            shorten ? LogEntriesDashboard.ShortId(correlationId) : correlationId
        );
        var destination = encoder.Encode(
            href ?? LogEntriesDashboard.BuildCorrelationHref(options ?? new(), correlationId)
        );
        return new HtmlString(
            $"""
            <span class="d-inline-flex align-items-center gap-1 mw-100" data-dashboard-correlation><a class="text-break text-decoration-none" href="{destination}" title="Show logs for correlation {identifier}">{label}</a><button type="button" class="btn btn-link btn-sm p-0 text-muted flex-shrink-0" data-dashboard-copy="{identifier}" title="Copy correlation ID" aria-label="Copy correlation ID"><i class="bi bi-copy" aria-hidden="true"></i></button></span>
            """
        );
    }
}
