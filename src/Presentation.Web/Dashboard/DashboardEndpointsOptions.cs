// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

using System.Reflection;

/// <summary>
///     Configures the built-in dashboard endpoint group and dashboard plugin discovery.
/// </summary>
/// <example>
/// <code>
/// services.AddDashboard(options => options
///     .WithGroupPath("/_bdk/dashboard")
///     .Authorize(authorization => authorization.RequireRole(Role.Administrators)));
/// </code>
/// </example>
public class DashboardEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DashboardEndpointsOptions" /> class with dashboard defaults.
    /// </summary>
    /// <example>
    /// <code>
    /// var options = new DashboardEndpointsOptions();
    /// </code>
    /// </example>
    public DashboardEndpointsOptions()
    {
        this.Enabled = true;
        this.GroupPath = "/_bdk/dashboard";
        this.GroupTag = "_bdk.Dashboard";
        this.ExcludeFromDescription = true;
        this.Title = "BDK Dashboard";
        this.EndpointPaths = new DashboardEndpointPaths(); // Default endpoint paths
    }

    /// <summary>
    ///     Gets or sets how dashboard authorization is applied when the dashboard endpoint group is mapped.
    /// </summary>
    /// <example>
    /// <code>
    /// options.AuthorizationMode = DashboardAuthorizationMode.Auto;
    /// </code>
    /// </example>
    public DashboardAuthorizationMode AuthorizationMode { get; set; } = DashboardAuthorizationMode.Auto;

    /// <summary>
    ///     Gets or sets the authentication schemes used when dashboard sign-out is requested.
    /// </summary>
    /// <remarks>
    ///     When empty, dashboard sign-out falls back to <see cref="EndpointsOptionsBase.RequireAuthenticationSchemes" />.
    /// </remarks>
    /// <example>
    /// <code>
    /// options.SignOutAuthenticationSchemes = ["Dashboard"];
    /// </code>
    /// </example>
    public string[] SignOutAuthenticationSchemes { get; set; } = [];

    /// <summary>
    ///     Gets the dashboard authentication registration selected by the authorization builder.
    /// </summary>
    /// <example>
    /// <code>
    /// var registration = options.Authentication;
    /// </code>
    /// </example>
    public DashboardAuthenticationRegistration Authentication { get; } = new();

    /// <summary>
    ///     Gets or sets the built-in dashboard endpoint paths below <see cref="EndpointsOptionsBase.GroupPath" />.
    /// </summary>
    /// <example>
    /// <code>
    /// options.EndpointPaths.IndexContent = "/content";
    /// </code>
    /// </example>
    public DashboardEndpointPaths EndpointPaths { get; set; }

    /// <summary>
    ///     Gets or sets the dashboard shell title.
    /// </summary>
    /// <example>
    /// <code>
    /// options.Title = "Operations Dashboard";
    /// </code>
    /// </example>
    public string Title { get; set; }

    /// <summary>
    ///     Gets the assemblies that are scanned for dashboard endpoint and page provider plugins.
    /// </summary>
    /// <example>
    /// <code>
    /// options.PluginAssemblies.Add(typeof(MyDashboardPlugin).Assembly);
    /// </code>
    /// </example>
    public List<Assembly> PluginAssemblies { get; } = [];
}

/// <summary>
///     Defines how the dashboard applies authorization metadata to its endpoint group.
/// </summary>
/// <example>
/// <code>
/// builder.Services.AddDashboard(options => options
///     .Authorize(authorization => authorization.Auto()));
/// </code>
/// </example>
public enum DashboardAuthorizationMode
{
    /// <summary>
    ///     Requires authorization only when the host application has registered authentication schemes.
    /// </summary>
    Auto,

    /// <summary>
    ///     Always requires an authenticated principal for dashboard routes.
    /// </summary>
    RequireAuthenticated,

    /// <summary>
    ///     Explicitly allows anonymous dashboard access.
    /// </summary>
    Anonymous
}
