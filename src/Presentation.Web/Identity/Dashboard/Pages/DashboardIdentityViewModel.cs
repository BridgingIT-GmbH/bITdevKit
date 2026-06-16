// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Identity.Dashboard.Pages;

/// <summary>
/// Represents the current user state shown on the dashboard identity page.
/// </summary>
/// <example>
/// <code>
/// var model = new DashboardIdentityViewModel
/// {
///     IsAccessorRegistered = true,
///     IsAuthenticated = true,
///     UserName = "Luke Skywalker"
/// };
/// </code>
/// </example>
public class DashboardIdentityViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether an <c>ICurrentUserAccessor</c> was available for the request.
    /// </summary>
    public bool IsAccessorRegistered { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the current user is authenticated.
    /// </summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// Gets or sets the authentication type reported by the current identity.
    /// </summary>
    public string AuthenticationType { get; set; }

    // /// <summary>
    // /// Gets or sets a value indicating whether the fake identity provider client-credentials login button can be shown.
    // /// </summary>
    // public bool CanLoginWithClientCredentials { get; set; }

    /// <summary>
    /// Gets or sets the dashboard-local action URL for the client-credentials login bridge.
    /// </summary>
    public string ClientCredentialsLoginPath { get; set; }

    /// <summary>
    /// Gets or sets the safe local return URL used after the client-credentials login bridge completes.
    /// </summary>
    public string ClientCredentialsReturnUrl { get; set; }

    /// <summary>
    /// Gets or sets the fake identity provider token endpoint used by the client-credentials flow.
    /// </summary>
    public string ClientCredentialsTokenEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the configured fake identity provider client name.
    /// </summary>
    public string ClientCredentialsClientName { get; set; }

    /// <summary>
    /// Gets or sets the configured fake identity provider client identifier.
    /// </summary>
    public string ClientCredentialsClientId { get; set; }

    /// <summary>
    /// Gets or sets the current user identifier.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the current user name.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the current user email address.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets the current user roles.
    /// </summary>
    public List<string> Roles { get; } = [];

    /// <summary>
    /// Gets the current user claims.
    /// </summary>
    public List<DashboardIdentityClaimViewModel> Claims { get; } = [];

    /// <summary>
    /// Gets the authentication tokens saved in the current authentication ticket.
    /// </summary>
    /// <example>
    /// <code>
    /// model.Tokens.Add(new DashboardIdentityTokenViewModel
    /// {
    ///     Name = "access_token",
    ///     DisplayName = "Access Token",
    ///     Value = "ey..."
    /// });
    /// </code>
    /// </example>
    public List<DashboardIdentityTokenViewModel> Tokens { get; } = [];

    /// <summary>
    /// Gets the informational messages shown on the identity dashboard.
    /// </summary>
    public List<string> Messages { get; } = [];

    /// <summary>
    /// Gets the error messages shown on the identity dashboard.
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// Gets or sets the UTC timestamp when this model was captured.
    /// </summary>
    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Represents a single claim shown on the dashboard identity page.
/// </summary>
/// <example>
/// <code>
/// var claim = new DashboardIdentityClaimViewModel
/// {
///     Type = "email",
///     Value = "user@example.com"
/// };
/// </code>
/// </example>
public class DashboardIdentityClaimViewModel
{
    /// <summary>
    /// Gets or sets the claim type.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the claim value.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets the claim value type.
    /// </summary>
    public string ValueType { get; set; }

    /// <summary>
    /// Gets or sets the claim issuer.
    /// </summary>
    public string Issuer { get; set; }
}

/// <summary>
/// Represents a saved authentication token shown on the dashboard identity page.
/// </summary>
/// <example>
/// <code>
/// var token = new DashboardIdentityTokenViewModel
/// {
///     Name = "id_token",
///     DisplayName = "ID Token",
///     Value = "ey..."
/// };
/// </code>
/// </example>
public class DashboardIdentityTokenViewModel
{
    /// <summary>
    /// Gets or sets the token name stored in the authentication ticket.
    /// </summary>
    /// <example>
    /// <code>
    /// token.Name = "access_token";
    /// </code>
    /// </example>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the display name shown in the dashboard.
    /// </summary>
    /// <example>
    /// <code>
    /// token.DisplayName = "Access Token";
    /// </code>
    /// </example>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the token value.
    /// </summary>
    /// <example>
    /// <code>
    /// token.Value = "ey...";
    /// </code>
    /// </example>
    public string Value { get; set; }
}
