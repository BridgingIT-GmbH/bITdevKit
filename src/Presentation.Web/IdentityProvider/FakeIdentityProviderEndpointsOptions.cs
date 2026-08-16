// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Collections.Generic;
using BridgingIT.DevKit.Common;

/// <summary>
/// Configures fake identity provider endpoints.
/// </summary>
public class FakeIdentityProviderEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>
    /// Initializes a new instance of the <c>FakeIdentityProviderEndpointsOptions</c> class.
    /// </summary>
    public FakeIdentityProviderEndpointsOptions()
    {
        this.Enabled = true;
        this.GroupPath = "/_bdk/api/identity/connect";
        this.GroupTag = "_bdk.Identity.Connect";
        this.RequireAuthorization = false;
        this.Issuer = "https://localhost:5001"; // should match Client Authority  "https://localhost:5001/_bdk/api/identity/connect"
        this.EndpointPaths = new FakeIdentityProviderEndpointPaths(); // Default endpoint paths
        this.AccessTokenLifetime = TimeSpan.FromHours(24);
        this.RefreshTokenLifetime = TimeSpan.FromDays(7);
        this.SigningKey = string.Empty; // "your-256-bit-secret-your-256-bit-secret-your-256-bit-secret";
    }

    /// <summary>
    /// Gets or sets the users.
    /// </summary>
    public IReadOnlyList<FakeUser> Users { get; set; } = [];//= FakeUsers.Fantasy;

    /// <summary>
    /// Gets or sets the issuer.
    /// </summary>
    public string Issuer { get; set; }

    /// <summary>
    /// Gets or sets the endpoint paths.
    /// </summary>
    public FakeIdentityProviderEndpointPaths EndpointPaths { get; set; }

    /// <summary>
    /// Gets or sets the clients.
    /// </summary>
    public IReadOnlyList<FakeIdentityProviderClient> Clients { get; set; } = [];

    /// <summary>
    /// Gets or sets the access token lifetime.
    /// </summary>
    public TimeSpan AccessTokenLifetime { get; set; }

    /// <summary>
    /// Gets or sets the refresh token lifetime.
    /// </summary>
    public TimeSpan RefreshTokenLifetime { get; set; }

    /// <summary>
    /// Gets or sets the enable persistent refresh tokens.
    /// </summary>
    public bool EnablePersistentRefreshTokens { get; set; } = true;

    /// <summary>
    /// Gets or sets the signing key.
    /// </summary>
    public string SigningKey { get; set; }

    /// <summary>
    /// Gets or sets the enable user cards.
    /// </summary>
    public bool EnableUserCards { get; set; } = true;

    /// <summary>
    /// Gets or sets the enable login card.
    /// </summary>
    public bool EnableLoginCard { get; set; }

    /// <summary>
    /// Gets or sets the enable cookie single sign on.
    /// </summary>
    public bool EnableCookieSingleSignOn { get; set; } = true;

    /// <summary>
    ///     Gets or sets the cookie authentication scheme used for fake identity provider single sign-on.
    /// </summary>
    /// <example>
    /// <code>
    /// options.CookieAuthenticationScheme = FakeIdentityProviderAuthenticationDefaults.CookieScheme;
    /// </code>
    /// </example>
    public string CookieAuthenticationScheme { get; set; } = FakeIdentityProviderAuthenticationDefaults.CookieScheme;

    /// <summary>
    /// Gets or sets the token provider.
    /// </summary>
    public TokenProvider TokenProvider { get; set; } = TokenProvider.Default;

    // Add provider-specific properties
    /// <summary>
    /// Gets or sets the tenant id.
    /// </summary>
    public string TenantId { get; set; }    // For Azure AD
    /// <summary>
    /// Gets or sets the realm name.
    /// </summary>
    public string RealmName { get; set; }   // For Keycloak
    /// <summary>
    /// Gets or sets the client id.
    /// </summary>
    public string ClientId { get; set; }    // Common
}

/// <summary>
/// Defines the supported token provider values.
/// </summary>
public enum TokenProvider
{
    /// <summary>
    /// Represents the default value.
    /// </summary>
    Default,
    /// <summary>
    /// Represents the entra id v2 value.
    /// </summary>
    EntraIdV2,
    /// <summary>
    /// Represents the keycloak value.
    /// </summary>
    Keycloak,
    /// <summary>
    /// Represents the adfs value.
    /// </summary>
    Adfs
}
