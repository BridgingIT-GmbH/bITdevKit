// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard.Pages;

using BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Represents the server-rendered dashboard metrics page state.
/// </summary>
/// <example>
/// <code>
/// var model = new DashboardMetricsViewModel
/// {
///     ContentPath = "/_bdk/dashboard/metrics/content",
///     App = metricsSnapshotService.GetSnapshot()
/// };
/// </code>
/// </example>
public class DashboardMetricsViewModel
{
    /// <summary>
    /// Gets or sets the dashboard-local endpoint path used to refresh the metrics content fragment.
    /// </summary>
    public string ContentPath { get; set; } = "/_bdk/dashboard/metrics/content";

    /// <summary>
    /// Gets or sets the UTC timestamp when this dashboard model was captured.
    /// </summary>
    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the full devkit metrics snapshot.
    /// </summary>
    public MetricsSnapshotModel App { get; set; } = new();

    /// <summary>
    /// Gets or sets the dashboard-oriented devkit metrics overview.
    /// </summary>
    public MetricsOverviewSnapshotModel Overview { get; set; } = new();

    /// <summary>
    /// Gets or sets the .NET runtime metrics snapshot.
    /// </summary>
    public DotNetMetricsSnapshotModel DotNet { get; set; } = new();

    /// <summary>
    /// Gets or sets the ASP.NET request metrics snapshot.
    /// </summary>
    public AspNetMetricsSnapshotModel AspNet { get; set; } = new();

    /// <summary>
    /// Gets or sets the ASP.NET route metrics snapshot.
    /// </summary>
    public AspNetRouteMetricsSnapshotModel AspNetRoutes { get; set; } = new();

    /// <summary>
    /// Gets the names of metrics services that are not registered.
    /// </summary>
    public List<string> UnavailableServices { get; } = [];

    /// <summary>
    /// Gets the snapshot retrieval errors that should be shown on the dashboard.
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// Gets a value indicating whether at least one snapshot source is available.
    /// </summary>
    public bool HasAnyMetrics =>
        this.UnavailableServices.Count < 3 || this.App.Features.Count > 0 || this.AspNet.TotalRequests > 0;
}
