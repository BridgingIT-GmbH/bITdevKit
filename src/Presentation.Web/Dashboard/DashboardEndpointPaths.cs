// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

/// <summary>
///     Defines built-in dashboard endpoint paths below the configured dashboard group path.
/// </summary>
/// <example>
/// <code>
/// options.EndpointPaths.SignOut = "/signout";
/// </code>
/// </example>
public class DashboardEndpointPaths
{
    /// <summary>
    ///     Gets or sets the dashboard index content fragment path.
    /// </summary>
    public string IndexContent { get; set; } = "/content";

    /// <summary>
    ///     Gets or sets the dashboard system overview path.
    /// </summary>
    public string System { get; set; } = "/system";

    /// <summary>
    ///     Gets or sets the dashboard system overview content fragment path.
    /// </summary>
    public string SystemContent { get; set; } = "/system/content";

    /// <summary>
    ///     Gets or sets the dashboard identity page path.
    /// </summary>
    public string Identity { get; set; } = "/identity";

    /// <summary>
    ///     Gets or sets the dashboard identity client credentials login action path.
    /// </summary>
    public string IdentityClientCredentialsLogin { get; set; } = "/identity/client-credentials/login";

    /// <summary>
    ///     Gets or sets the dashboard access-denied page path.
    /// </summary>
    /// <example>
    /// <code>
    /// options.EndpointPaths.AccessDenied = "/access-denied";
    /// </code>
    /// </example>
    public string AccessDenied { get; set; } = "/access-denied";

    /// <summary>
    ///     Gets or sets the dashboard sign-out action path.
    /// </summary>
    /// <example>
    /// <code>
    /// options.EndpointPaths.SignOut = "/signout";
    /// </code>
    /// </example>
    public string SignOut { get; set; } = "/signout";

    /// <summary>
    ///     Gets or sets the dashboard metrics page path.
    /// </summary>
    public string Metrics { get; set; } = "/metrics";

    /// <summary>
    ///     Gets or sets the dashboard metrics content fragment path.
    /// </summary>
    public string MetricsContent { get; set; } = "/metrics/content";
}
