// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

/// <summary>
///     Defines which authentication handlers the dashboard registers or reuses.
/// </summary>
/// <example>
/// <code>
/// if (options.Authentication.Kind == DashboardAuthenticationRegistrationKind.OpenIdConnect)
/// {
///     // The dashboard owns its interactive sign-in flow.
/// }
/// </code>
/// </example>
public enum DashboardAuthenticationRegistrationKind
{
    /// <summary>
    ///     No dashboard-specific authentication scheme was selected.
    /// </summary>
    None,

    /// <summary>
    ///     The dashboard reuses a scheme registered by the host application.
    /// </summary>
    ExistingScheme,

    /// <summary>
    ///     The dashboard registers its own cookie, policy, and OpenID Connect schemes.
    /// </summary>
    OpenIdConnect
}

/// <summary>
///     Stores the authentication scheme registration selected for the dashboard.
/// </summary>
/// <example>
/// <code>
/// builder.Services.AddDashboard(options => options
///     .Authorize(authorization => authorization
///         .UseOpenIdConnect("https://idp.example")));
/// </code>
/// </example>
public sealed class DashboardAuthenticationRegistration
{
    /// <summary>
    ///     Gets or sets which authentication registration mode the dashboard uses.
    /// </summary>
    /// <example>
    /// <code>
    /// registration.Kind = DashboardAuthenticationRegistrationKind.ExistingScheme;
    /// </code>
    /// </example>
    public DashboardAuthenticationRegistrationKind Kind { get; set; } = DashboardAuthenticationRegistrationKind.None;

    /// <summary>
    ///     Gets or sets the scheme name used by dashboard authorization metadata.
    /// </summary>
    /// <example>
    /// <code>
    /// registration.Scheme = DashboardAuthenticationDefaults.AuthenticationScheme;
    /// </code>
    /// </example>
    public string Scheme { get; set; } = DashboardAuthenticationDefaults.AuthenticationScheme;

    /// <summary>
    ///     Gets or sets the dashboard-owned cookie scheme name.
    /// </summary>
    /// <example>
    /// <code>
    /// registration.CookieScheme = DashboardAuthenticationDefaults.CookieScheme;
    /// </code>
    /// </example>
    public string CookieScheme { get; set; } = DashboardAuthenticationDefaults.CookieScheme;

    /// <summary>
    ///     Gets or sets the dashboard-owned OpenID Connect scheme name.
    /// </summary>
    /// <example>
    /// <code>
    /// registration.OpenIdConnectScheme = DashboardAuthenticationDefaults.OpenIdConnectScheme;
    /// </code>
    /// </example>
    public string OpenIdConnectScheme { get; set; } = DashboardAuthenticationDefaults.OpenIdConnectScheme;

    /// <summary>
    ///     Gets or sets a value indicating whether the dashboard sign-out route should sign out the selected scheme.
    /// </summary>
    /// <example>
    /// <code>
    /// registration.SignOutEnabled = false;
    /// </code>
    /// </example>
    public bool SignOutEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets a callback that customizes the dashboard-owned cookie handler.
    /// </summary>
    /// <example>
    /// <code>
    /// registration.ConfigureCookie = options => options.ExpireTimeSpan = TimeSpan.FromHours(8);
    /// </code>
    /// </example>
    public Action<CookieAuthenticationOptions> ConfigureCookie { get; set; }

    /// <summary>
    ///     Gets or sets a callback that customizes the dashboard-owned OpenID Connect handler.
    /// </summary>
    /// <example>
    /// <code>
    /// registration.ConfigureOpenIdConnect = options => options.ClientId = "dashboard";
    /// </code>
    /// </example>
    public Action<OpenIdConnectOptions> ConfigureOpenIdConnect { get; set; }
}
