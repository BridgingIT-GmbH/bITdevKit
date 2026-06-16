// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

/// <summary>
///     Provides default authentication scheme names used by dashboard-owned authentication.
/// </summary>
/// <example>
/// <code>
/// policy.AddAuthenticationSchemes(DashboardAuthenticationDefaults.AuthenticationScheme);
/// </code>
/// </example>
public static class DashboardAuthenticationDefaults
{
    /// <summary>
    ///     The dashboard policy authentication scheme.
    /// </summary>
    /// <example>
    /// <code>
    /// authorization.UseExistingScheme(DashboardAuthenticationDefaults.AuthenticationScheme);
    /// </code>
    /// </example>
    public const string AuthenticationScheme = "_bdk.dashboard";

    /// <summary>
    ///     The dashboard-owned cookie authentication scheme.
    /// </summary>
    /// <example>
    /// <code>
    /// var scheme = DashboardAuthenticationDefaults.CookieScheme;
    /// </code>
    /// </example>
    public const string CookieScheme = "_bdk.dashboard.cookie";

    /// <summary>
    ///     The dashboard-owned OpenID Connect authentication scheme.
    /// </summary>
    /// <example>
    /// <code>
    /// var scheme = DashboardAuthenticationDefaults.OpenIdConnectScheme;
    /// </code>
    /// </example>
    public const string OpenIdConnectScheme = "_bdk.dashboard.oidc";

    /// <summary>
    ///     The default OpenID Connect client id for dashboard-owned interactive sign-in.
    /// </summary>
    /// <example>
    /// <code>
    /// options.ClientId = DashboardAuthenticationDefaults.ClientId;
    /// </code>
    /// </example>
    public const string ClientId = "dashboard";
}
