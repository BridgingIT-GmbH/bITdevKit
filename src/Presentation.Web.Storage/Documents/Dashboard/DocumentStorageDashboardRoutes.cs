// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Storage.Documents.Dashboard;

using BridgingIT.DevKit.Presentation.Web.Dashboard;

/// <summary>Builds stable Document Storage dashboard routes.</summary>
/// <example><code>var path = DocumentStorageDashboardRoutes.Documents(options);</code></example>
public static class DocumentStorageDashboardRoutes
{
    private const string DocumentsPath = "/storage/documents";
    private const string ContentPath = "/storage/documents/content";
    private const string DownloadPath = "/storage/documents/download";
    private const string ActionsPath = "/storage/documents/actions";
    /// <summary>Builds the dashboard page route.</summary>
    public static string Documents(DashboardEndpointsOptions options) => DashboardPath.Combine(options?.GroupPath, DocumentsPath);
    /// <summary>Builds the refreshable content route.</summary>
    public static string Content(DashboardEndpointsOptions options) => DashboardPath.Combine(options?.GroupPath, ContentPath);
    /// <summary>Builds the document download route.</summary>
    public static string Download(DashboardEndpointsOptions options) => DashboardPath.Combine(options?.GroupPath, DownloadPath);
    /// <summary>Builds the mutation action base route.</summary>
    public static string Actions(DashboardEndpointsOptions options) => DashboardPath.Combine(options?.GroupPath, ActionsPath);
}
