// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Represents token response.
/// </summary>
public class TokenResponse
{
    /// <summary>
    /// Gets or sets the access token.
    /// </summary>
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the refresh token.
    /// </summary>
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets the expires in.
    /// </summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Gets or sets the refresh expires in.
    /// </summary>
    [JsonPropertyName("refresh_expires_in")]
    public int RefreshExpiresIn { get; set; }

    /// <summary>
    /// Gets or sets the token type.
    /// </summary>
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Gets or sets the id token.
    /// </summary>
    [JsonPropertyName("id_token")]
    public string IdToken { get; set; }

    /// <summary>
    /// Gets or sets the session state.
    /// </summary>
    [JsonPropertyName("session_state")]
    public string SessionState { get; set; }

    /// <summary>
    /// Gets or sets the scope.
    /// </summary>
    [JsonPropertyName("scope")]
    public string Scope { get; set; }
}

/// <summary>
/// Represents o auth2 error.
/// </summary>
public class OAuth2Error
{
    /// <summary>
    /// Gets or sets the error.
    /// </summary>
    [JsonPropertyName("error")]
    public string Error { get; init; }

    /// <summary>
    /// Gets or sets the error description.
    /// </summary>
    [JsonPropertyName("error_description")]
    public string ErrorDescription { get; init; }

    /// <summary>
    /// Gets or sets the error uri.
    /// </summary>
    [JsonPropertyName("error_uri")]
    public string ErrorUri { get; init; }
}

/// <summary>
/// Represents user info response.
/// </summary>
public class UserInfoResponse
{
    /// <summary>
    /// Gets or sets the sub.
    /// </summary>
    [JsonPropertyName("sub")]
    public string Sub { get; init; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; }

    /// <summary>
    /// Gets or sets the given name.
    /// </summary>
    [JsonPropertyName("given_name")]
    public string GivenName { get; init; }

    /// <summary>
    /// Gets or sets the family name.
    /// </summary>
    [JsonPropertyName("family_name")]
    public string FamilyName { get; init; }

    /// <summary>
    /// Gets or sets the preferred username.
    /// </summary>
    [JsonPropertyName("preferred_username")]
    public string PreferredUsername { get; init; }

    /// <summary>
    /// Gets or sets the email.
    /// </summary>
    [JsonPropertyName("email")]
    public string Email { get; init; }

    /// <summary>
    /// Gets or sets the email verified.
    /// </summary>
    [JsonPropertyName("email_verified")]
    public bool EmailVerified { get; init; }

    /// <summary>
    /// Gets or sets the roles.
    /// </summary>
    [JsonPropertyName("roles")]
    public IReadOnlyList<string> Roles { get; init; }

    //[JsonPropertyName("claims")]
    //public IReadOnlyDictionary<string, string> Claims { get; init; }
}

/// <summary>
/// Represents open id configuration.
/// </summary>
public class OpenIdConfiguration
{
    /// <summary>
    /// Gets or sets the issuer.
    /// </summary>
    [JsonPropertyName("issuer")]
    public string Issuer { get; init; } // https://localhost:5001

    /// <summary>
    /// Gets or sets the authorization endpoint.
    /// </summary>
    [JsonPropertyName("authorization_endpoint")]
    public string AuthorizationEndpoint { get; init; } // Authorize

    /// <summary>
    /// Gets or sets the token endpoint.
    /// </summary>
    [JsonPropertyName("token_endpoint")]
    public string TokenEndpoint { get; init; } // Token

    /// <summary>
    /// Gets or sets the user info endpoint.
    /// </summary>
    [JsonPropertyName("userinfo_endpoint")]
    public string UserInfoEndpoint { get; init; } // Profile

    /// <summary>
    /// Gets or sets the end session endpoint.
    /// </summary>
    [JsonPropertyName("end_session_endpoint")]
    public string EndSessionEndpoint { get; init; } // Logout

    /// <summary>
    /// Gets or sets the grant types supported.
    /// </summary>
    [JsonPropertyName("grant_types_supported")]
    public IReadOnlyList<string> GrantTypesSupported { get; init; } =
    [
        "authorization_code", // For both SPA and server web apps
        "password", // For server web apps,  Resource Owner Password flow
        "client_credentials", // For server web apps, Client Credentials flow
        "refresh_token" // For both SPA and server web apps
    ];

    /// <summary>
    /// Gets or sets the response types supported.
    /// </summary>
    [JsonPropertyName("response_types_supported")]
    public IReadOnlyList<string> ResponseTypesSupported { get; init; } =
    [
        "code" // For both SPA and server web apps
    ];

    /// <summary>
    /// Gets or sets the response modes supported.
    /// </summary>
    [JsonPropertyName("response_modes_supported")]
    public IReadOnlyList<string> ResponseModesSupported { get; init; } =
    [
        "query", "form_post"
    ];

    /// <summary>
    /// Gets or sets the scopes supported.
    /// </summary>
    [JsonPropertyName("scopes_supported")]
    public IReadOnlyList<string> ScopesSupported { get; init; } =
    [
        "openid", // Required
        "profile", // Optional
        "email", // Optional
        "roles", // Optional
        "offline_access" // Optional
    ];

    /// <summary>
    /// Gets or sets the claims supported.
    /// </summary>
    [JsonPropertyName("claims_supported")]
    public IReadOnlyList<string> ClaimsSupported { get; init; } =
    [
        "sub", // Subject
        "name", // Full name
        "family_name", // Surname
        "given_name", // First name
        "preferred_username", // Nickname
        "email", // Email
        "email_verified", // Email verified
        "nonce" // Authorization request nonce
    ];

    /// <summary>
    /// Gets or sets the token endpoint auth methods supported.
    /// </summary>
    [JsonPropertyName("token_endpoint_auth_methods_supported")]
    public IReadOnlyList<string> TokenEndpointAuthMethodsSupported { get; init; } =
    [
        "client_secret_post", // Secret in request body
        "client_secret_basic", // Secret in Authorization header
        "none"  // For public clients
    ];
}

/// <summary>
/// Represents authorization code model.
/// </summary>
public class AuthorizationCodeModel
{
    /// <summary>
    /// Gets or sets the user id.
    /// </summary>
    public string UserId { get; init; }

    /// <summary>
    /// Gets or sets the client id.
    /// </summary>
    public string ClientId { get; init; }

    /// <summary>
    /// Gets or sets the redirect uri.
    /// </summary>
    public string RedirectUri { get; init; }

    /// <summary>
    /// Gets or sets the scope.
    /// </summary>
    public string Scope { get; init; }

    /// <summary>
    /// Gets or sets the nonce.
    /// </summary>
    public string Nonce { get; init; }

    /// <summary>
    /// Gets or sets the expires at.
    /// </summary>
    public DateTime ExpiresAt { get; init; }
}

/// <summary>
/// Represents authorize request.
/// </summary>
public class AuthorizeRequest
{
    /// <summary>
    /// Gets or sets the response type.
    /// </summary>
    [FromQuery(Name = "response_type")]
    public string ResponseType { get; init; }

    /// <summary>
    /// Gets or sets the client id.
    /// </summary>
    [FromQuery(Name = "client_id")]
    public string ClientId { get; init; }

    /// <summary>
    /// Gets or sets the redirect uri.
    /// </summary>
    [FromQuery(Name = "redirect_uri")]
    public string RedirectUri { get; init; }

    /// <summary>
    /// Gets or sets the scope.
    /// </summary>
    [FromQuery(Name = "scope")]
    public string Scope { get; init; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    [FromQuery(Name = "state")]
    public string State { get; init; }

    /// <summary>
    /// Gets or sets the nonce.
    /// </summary>
    [FromQuery(Name = "nonce")]
    public string Nonce { get; init; }
}

/// <summary>
/// Represents token request.
/// </summary>
public class TokenRequest
{
    /// <summary>
    /// Gets or sets the grant type.
    /// </summary>
    [FromForm(Name = "grant_type")]
    public string GrantType { get; init; }

    /// <summary>
    /// Gets or sets the client id.
    /// </summary>
    [FromForm(Name = "client_id")]
    public string ClientId { get; init; }

    /// <summary>
    /// Gets or sets the code.
    /// </summary>
    [FromForm(Name = "code")]
    public string Code { get; init; }

    /// <summary>
    /// Gets or sets the refresh token.
    /// </summary>
    [FromForm(Name = "refresh_token")]
    public string RefreshToken { get; init; }

    /// <summary>
    /// Gets or sets the username.
    /// </summary>
    [FromForm(Name = "username")]
    public string Username { get; init; }

    /// <summary>
    /// Gets or sets the password.
    /// </summary>
    [FromForm(Name = "password")]
    public string Password { get; init; }

    /// <summary>
    /// Gets or sets the scope.
    /// </summary>
    [FromForm(Name = "scope")]
    public string Scope { get; init; }

    /// <summary>
    /// Gets or sets the redirect uri.
    /// </summary>
    [FromForm(Name = "redirect_uri")]
    public string RedirectUri { get; init; }
}

/// <summary>
/// Represents token validation result.
/// </summary>
public class TokenValidationResult
{
    /// <summary>
    /// Gets or sets the is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the claims.
    /// </summary>
    public IEnumerable<Claim> Claims { get; set; } = [];

    /// <summary>
    /// Gets or sets the error.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets or sets the error description.
    /// </summary>
    public string ErrorDescription { get; set; }
}

/// <summary>
/// Represents debug info response.
/// </summary>
public class DebugInfoResponse
{
    /// <summary>
    /// Gets or sets the token issuer.
    /// </summary>
    public string TokenIssuer { get; init; }

    /// <summary>
    /// Gets or sets the token provider.
    /// </summary>
    public string TokenProvider { get; internal set; }

    /// <summary>
    /// Gets or sets the configured clients.
    /// </summary>
    public IReadOnlyList<DevClientDebugInfo> ConfiguredClients { get; init; }

    /// <summary>
    /// Gets or sets the configured users.
    /// </summary>
    public IReadOnlyList<UserDebugInfo> ConfiguredUsers { get; init; }

    /// <summary>
    /// Gets or sets the endpoints.
    /// </summary>
    public EndpointDebugInfo Endpoints { get; init; }
}

/// <summary>
/// Represents dev client debug info.
/// </summary>
public class DevClientDebugInfo
{
    /// <summary>
    /// Gets or sets the client id.
    /// </summary>
    public string ClientId { get; init; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets or sets the redirect uris.
    /// </summary>
    public IReadOnlyList<string> RedirectUris { get; init; }

    /// <summary>
    /// Gets or sets the allowed scopes.
    /// </summary>
    public IReadOnlyList<string> AllowedScopes { get; init; }
}

/// <summary>
/// Represents user debug info.
/// </summary>
public class UserDebugInfo
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Gets or sets the email.
    /// </summary>
    public string Email { get; init; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets or sets the roles.
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; }

    /// <summary>
    /// Gets or sets the is default.
    /// </summary>
    public bool IsDefault { get; init; }
}

/// <summary>
/// Represents endpoint debug info.
/// </summary>
public class EndpointDebugInfo
{
    /// <summary>
    /// Gets or sets the authorization.
    /// </summary>
    public string Authorization { get; init; }
    /// <summary>
    /// Gets or sets the token.
    /// </summary>
    public string Token { get; init; }
    /// <summary>
    /// Gets or sets the user info.
    /// </summary>
    public string UserInfo { get; init; }
    /// <summary>
    /// Gets or sets the logout.
    /// </summary>
    public string Logout { get; init; }
}
