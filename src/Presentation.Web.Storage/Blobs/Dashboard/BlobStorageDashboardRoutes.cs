// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Blobs.Dashboard;

using BridgingIT.DevKit.Presentation.Web.Dashboard;

/// <summary>
/// Builds Blob Storage dashboard routes from dashboard endpoint options.
/// </summary>
/// <example>
/// <code>
/// var path = BlobStorageDashboardRoutes.BuildBlobsPath(options);
/// </code>
/// </example>
public static class BlobStorageDashboardRoutes
{
    private const string BlobsPath = "/storage/blobs";
    private const string BlobsContentPath = "/storage/blobs/content";
    private const string BlobsDownloadPath = "/storage/blobs/download";
    private const string ActionsPath = "/storage/blobs/actions";

    /// <summary>
    /// Builds the main blob dashboard path.
    /// </summary>
    /// <param name="options">The dashboard endpoint options.</param>
    /// <returns>The combined route.</returns>
    /// <example>
    /// <code>
    /// var path = BlobStorageDashboardRoutes.BuildBlobsPath(options);
    /// </code>
    /// </example>
    public static string BuildBlobsPath(DashboardEndpointsOptions options) =>
        DashboardPath.Combine(options?.GroupPath, BlobsPath);

    /// <summary>
    /// Builds the blob dashboard content-fragment path.
    /// </summary>
    /// <param name="options">The dashboard endpoint options.</param>
    /// <returns>The combined route.</returns>
    /// <example>
    /// <code>
    /// var path = BlobStorageDashboardRoutes.BuildBlobsContentPath(options);
    /// </code>
    /// </example>
    public static string BuildBlobsContentPath(DashboardEndpointsOptions options) =>
        DashboardPath.Combine(options?.GroupPath, BlobsContentPath);

    /// <summary>
    /// Builds the blob dashboard download path.
    /// </summary>
    /// <param name="options">The dashboard endpoint options.</param>
    /// <returns>The combined route.</returns>
    /// <example>
    /// <code>
    /// var path = BlobStorageDashboardRoutes.BuildBlobsDownloadPath(options);
    /// </code>
    /// </example>
    public static string BuildBlobsDownloadPath(DashboardEndpointsOptions options) =>
        DashboardPath.Combine(options?.GroupPath, BlobsDownloadPath);

    /// <summary>
    /// Builds the blob dashboard action base path.
    /// </summary>
    /// <param name="options">The dashboard endpoint options.</param>
    /// <returns>The combined route.</returns>
    /// <example>
    /// <code>
    /// var path = BlobStorageDashboardRoutes.BuildBlobsActionBase(options);
    /// </code>
    /// </example>
    public static string BuildBlobsActionBase(DashboardEndpointsOptions options) =>
        DashboardPath.Combine(options?.GroupPath, ActionsPath);
}
