// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching.Dashboard;

using BridgingIT.DevKit.Presentation.Web.Dashboard;

/// <summary>Builds directly addressable caching tabs and fragments beneath the configured Dashboard group.</summary>
/// <example><code>var path = CacheDashboardRoutes.Collection(options, "cards", "entries");</code></example>
public static class CacheDashboardRoutes
{
    /// <summary>Gets the five cache-level routed views.</summary>
    public static IReadOnlyList<CacheDashboardTab> GlobalTabs { get; } =
    [
        new("overview", "Overview", ""), new("collections", "Collections", "collections"),
        new("shared-loads", "Shared Loads", "shared-loads"), new("diagnostics", "Diagnostics", "diagnostics"),
        new("operations", "Operations", "operations")
    ];

    /// <summary>Gets the six collection-level routed views.</summary>
    public static IReadOnlyList<CacheDashboardTab> CollectionTabs { get; } =
    [
        new("overview", "Overview", ""), new("entries", "Entries", "entries"), new("shared-loads", "Shared Loads", "shared-loads"),
        new("diagnostics", "Diagnostics", "diagnostics"), new("operations", "Operations", "operations"), new("settings", "Settings", "settings")
    ];

    /// <summary>Builds one global tab path.</summary>
    public static string Global(DashboardEndpointsOptions options, string tab = null) => DashboardPath.Combine(options.GroupPath, "caching", Segment(GlobalTabs, tab));

    /// <summary>Builds one declared collection tab path while keeping collection names outside raw path composition.</summary>
    public static string Collection(DashboardEndpointsOptions options, string collection, string tab = null) =>
        DashboardPath.Combine(options.GroupPath, "caching", "collections", Uri.EscapeDataString(collection), Segment(CollectionTabs, tab));

    /// <summary>Builds the common operation action prefix.</summary>
    public static string Actions(DashboardEndpointsOptions options) => DashboardPath.Combine(options.GroupPath, "caching", "actions");

    /// <summary>Builds the read-only history route without enabling external cache-resource endpoints.</summary>
    public static string History(DashboardEndpointsOptions options, string collection = null) =>
        collection is null ? DashboardPath.Combine(options.GroupPath, "caching", "history") :
        DashboardPath.Combine(options.GroupPath, "caching", "collections", Uri.EscapeDataString(collection), "history");

    /// <summary>Resolves a view name from the explicitly mapped current route, independent of query and configured group prefix.</summary>
    public static string View(string path, string collection = null)
    {
        path = path?.TrimEnd('/') ?? "";
        if (path.EndsWith("/content", StringComparison.OrdinalIgnoreCase)) { path = path[..^8]; }

        if (collection is not null)
        {
            var marker = "/collections/" + Uri.EscapeDataString(collection);
            var index = path.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
            var segment = index < 0 ? "" : path[(index + marker.Length)..].Trim('/');
            return CollectionTabs.FirstOrDefault(tab => tab.Segment.Equals(segment, StringComparison.OrdinalIgnoreCase))?.Key ?? "overview";
        }

        return GlobalTabs.Concat(CollectionTabs).FirstOrDefault(tab => tab.Segment.Length > 0 &&
            path.EndsWith("/" + tab.Segment, StringComparison.OrdinalIgnoreCase))?.Key ?? "overview";
    }

    private static string Segment(IReadOnlyList<CacheDashboardTab> tabs, string key) => tabs.FirstOrDefault(tab =>
        string.Equals(tab.Key, key ?? "overview", StringComparison.OrdinalIgnoreCase))?.Segment ??
        throw new ArgumentException("The cache Dashboard tab is not declared.", nameof(key));
}

/// <summary>Describes a finite directly linked Dashboard view.</summary>
/// <param name="Key">Stable remembered-view key.</param>
/// <param name="Title">Accessible tab label.</param>
/// <param name="Segment">Declared route suffix.</param>
/// <example><code>var tab = CacheDashboardRoutes.GlobalTabs[0];</code></example>
public sealed record CacheDashboardTab(string Key, string Title, string Segment);