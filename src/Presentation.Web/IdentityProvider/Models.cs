// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

public class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_expires_in")]
    public int RefreshExpiresIn { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("id_token")]
    public string IdToken { get; set; }

    [JsonPropertyName("session_state")]
    public string SessionState { get; set; }

    [JsonPropertyName("scope")]
    public string Scope { get; set; }
}

public class OAuth2Error
{
    [JsonPropertyName("error")]
    public string Error { get; init; }

    [JsonPropertyName("error_description")]
    public string ErrorDescription { get; init; }

    [JsonPropertyName("error_uri")]
    public string ErrorUri { get; init; }
}

public class UserInfoResponse
{
    [JsonPropertyName("sub")]
    public string Sub { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; }

    [JsonPropertyName("given_name")]
    public string GivenName { get; init; }

    [JsonPropertyName("family_name")]
    public string FamilyName { get; init; }

    [JsonPropertyName("preferred_username")]
    public string PreferredUsername { get; init; }

    [JsonPropertyName("email")]
    public string Email { get; init; }

    [JsonPropertyName("email_verified")]
    public bool EmailVerified { get; init; }

    [JsonPropertyName("roles")]
    public IReadOnlyList<string> Roles { get; init; }

    //[JsonPropertyName("claims")]
    //public IReadOnlyDictionary<string, string> Claims { get; init; }
}

public class OpenIdConfiguration
{
    [JsonPropertyName("issuer")]
    public string Issuer { get; init; } // https://localhost:5001

    [JsonPropertyName("authorization_endpoint")]
    public string AuthorizationEndpoint { get; init; } // Authorize

    [JsonPropertyName("token_endpoint")]
    public string TokenEndpoint { get; init; } // Token

    [JsonPropertyName("userinfo_endpoint")]
    public string UserInfoEndpoint { get; init; } // Profile

    [JsonPropertyName("end_session_endpoint")]
    public string EndSessionEndpoint { get; init; } // Logout

    [JsonPropertyName("grant_types_supported")]
    public IReadOnlyList<string> GrantTypesSupported { get; init; } =
    [
        "authorization_code", // For both SPA and server web apps
        "password", // For server web apps,  Resource Owner Password flow
        "client_credentials", // For server web apps, Client Credentials flow
        "refresh_token" // For both SPA and server web apps
    ];

    [JsonPropertyName("response_types_supported")]
    public IReadOnlyList<string> ResponseTypesSupported { get; init; } =
    [
        "code" // For both SPA and server web apps
    ];

    [JsonPropertyName("response_modes_supported")]
    public IReadOnlyList<string> ResponseModesSupported { get; init; } =
    [
        "query", "form_post"
    ];

    [JsonPropertyName("scopes_supported")]
    public IReadOnlyList<string> ScopesSupported { get; init; } =
    [
        "openid", // Required
        "profile", // Optional
        "email", // Optional
        "roles", // Optional
        "offline_access" // Optional
    ];

    [JsonPropertyName("claims_supported")]
    public IReadOnlyList<string> ClaimsSupported { get; init; } =
    [
        "sub", // Subject
        "name", // Full name
        "family_name", // Surname
        "given_name", // First name
        "preferred_username", // Nickname
        "email", // Email
        "email_verified" // Email verified
    ];

    [JsonPropertyName("token_endpoint_auth_methods_supported")]
    public IReadOnlyList<string> TokenEndpointAuthMethodsSupported { get; init; } =
    [
        "client_secret_post", // Secret in request body
        "client_secret_basic", // Secret in Authorization header
        "none"  // For public clients
    ];
}

public class AuthorizationCodeModel
{
    public string UserId { get; init; }

    public string ClientId { get; init; }

    public string RedirectUri { get; init; }

    public string Scope { get; init; }

    public DateTime ExpiresAt { get; init; }
}

public class AuthorizeRequest
{
    [FromQuery(Name = "response_type")]
    public string ResponseType { get; init; }

    [FromQuery(Name = "client_id")]
    public string ClientId { get; init; }

    [FromQuery(Name = "redirect_uri")]
    public string RedirectUri { get; init; }

    [FromQuery(Name = "scope")]
    public string Scope { get; init; }

    [FromQuery(Name = "state")]
    public string State { get; init; }
}

public class TokenRequest
{
    [FromForm(Name = "grant_type")]
    public string GrantType { get; init; }

    [FromForm(Name = "client_id")]
    public string ClientId { get; init; }

    [FromForm(Name = "code")]
    public string Code { get; init; }

    [FromForm(Name = "refresh_token")]
    public string RefreshToken { get; init; }

    [FromForm(Name = "username")]
    public string Username { get; init; }

    [FromForm(Name = "password")]
    public string Password { get; init; }

    [FromForm(Name = "scope")]
    public string Scope { get; init; }

    [FromForm(Name = "redirect_uri")]
    public string RedirectUri { get; init; }
}

public class TokenValidationResult
{
    public bool IsValid { get; set; }

    public IEnumerable<Claim> Claims { get; set; } = [];

    public string Error { get; set; }

    public string ErrorDescription { get; set; }
}

public class DebugInfoResponse
{
    public string TokenIssuer { get; init; }

    public string TokenProvider { get; internal set; }

    public IReadOnlyList<DevClientDebugInfo> ConfiguredClients { get; init; }

    public IReadOnlyList<UserDebugInfo> ConfiguredUsers { get; init; }

    public EndpointDebugInfo Endpoints { get; init; }
}

public class DevClientDebugInfo
{
    public string ClientId { get; init; }

    public string Name { get; init; }

    public IReadOnlyList<string> RedirectUris { get; init; }

    public IReadOnlyList<string> AllowedScopes { get; init; }
}

public class UserDebugInfo
{
    public string Id { get; init; }

    public string Email { get; init; }

    public string Name { get; init; }

    public IReadOnlyList<string> Roles { get; init; }

    public bool IsDefault { get; init; }
}

public class EndpointDebugInfo
{
    public string Authorization { get; init; }
    public string Token { get; init; }
    public string UserInfo { get; init; }
    public string Logout { get; init; }
}