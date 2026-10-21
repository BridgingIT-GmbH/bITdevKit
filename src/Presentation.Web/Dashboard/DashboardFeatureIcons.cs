// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

using System.Collections.Frozen;
using System.Xml.Linq;
using Microsoft.AspNetCore.Html;

/// <summary>
/// Resolves built-in dashboard page keys to the embedded, theme-aware feature SVGs.
/// </summary>
internal static class DashboardFeatureIcons
{
    private const string RESOURCE_PREFIX = "BridgingIT.DevKit.Presentation.Web.Dashboard.Icons.";

    private static readonly FrozenDictionary<string, HtmlString> icons = new Dictionary<string, string>
    {
        ["dashboard.overview"] = "dashboard",
        ["dashboard.system"] = "system",
        ["metrics"] = "metrics",
        ["profiling"] = "profiling",
        ["profiling.operations"] = "profiling-operations",
        ["profiling.requests"] = "profiling-requests",
        ["health"] = "health",
        ["identity"] = "identity",
        ["logging.logs"] = "log-entries",
        ["logging.errors"] = "errors",
        ["logging.stream"] = "streams",
        ["console"] = "console-commands",
        ["mcp"] = "mcp",
        ["broadcasting"] = "broadcasting",
        ["jobs"] = "jobs",
        ["orchestrations"] = "orchestrations",
        ["messaging"] = "messaging",
        ["queueing"] = "queueing",
        ["storage.files"] = "storage-files",
        ["storage.documents"] = "storage-documents",
        ["storage.blobs"] = "storage-blobs",
        ["storage.permalinks"] = "storage-permalinks",
        ["change-history"] = "change-history"
    }.ToFrozenDictionary(pair => pair.Key, pair => LoadSvg(pair.Value), StringComparer.Ordinal);

    internal static HtmlString GetSvg(string pageKey) =>
        pageKey is not null && icons.TryGetValue(pageKey, out var icon) ? icon : null;

    private static HtmlString LoadSvg(string featureId)
    {
        using var stream = typeof(DashboardFeatureIcons).Assembly.GetManifestResourceStream($"{RESOURCE_PREFIX}{featureId}.svg")
            ?? throw new InvalidOperationException($"Dashboard feature icon '{featureId}' is not embedded.");
        var svg = XElement.Load(stream);
        svg.Elements(svg.Name.Namespace + "title").Remove();
        svg.SetAttributeValue("role", null);
        svg.SetAttributeValue("aria-labelledby", null);
        svg.SetAttributeValue("aria-hidden", "true");
        svg.SetAttributeValue("focusable", "false");
        svg.SetAttributeValue("class", "dashboard-feature-icon me-2");
        svg.SetAttributeValue("data-dashboard-icon", featureId);

        // Markup comes only from the embedded canonical assets, never from page input.
        return new HtmlString(svg.ToString(SaveOptions.DisableFormatting));
    }
}
