// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Storage.Permalinks.Dashboard;

using BridgingIT.DevKit.Presentation.Web.Dashboard;

/// <summary>
/// Builds Storage Permalink dashboard routes.
/// </summary>
/// <example>
/// <code>
/// var path = StoragePermalinkDashboardRoutes.Index(options);
/// </code>
/// </example>
public static class StoragePermalinkDashboardRoutes
{
    /// <summary>
    /// Builds the registry dashboard page route.
    /// </summary>
    public static string Index(DashboardEndpointsOptions options) => DashboardPath.Combine(options?.GroupPath, "/storage/permalinks");

    /// <summary>
    /// Builds the registry maintenance action route.
    /// </summary>
    public static string Actions(DashboardEndpointsOptions options) => DashboardPath.Combine(options?.GroupPath, "/storage/permalinks/actions");
}
