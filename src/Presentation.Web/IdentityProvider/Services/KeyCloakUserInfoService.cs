// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text.Json;

// Keycloak implementation
/// <summary>
/// Represents key cloak user info service.
/// </summary>
/// <param name="tokenService">The token service used by the operation.</param>
/// <param name="options">The options controlling the operation.</param>
public class KeyCloakUserInfoService(ITokenService tokenService, FakeIdentityProviderEndpointsOptions options)
    : IUserInfoService
{
    /// <summary>
    /// Gets user info.
    /// </summary>
    /// <param name="accessToken">The access token used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public UserInfoResponse GetUserInfo(string accessToken)
    {
        var validationResult = tokenService.ValidateToken(accessToken);
        if (!validationResult.IsValid)
        {
            throw new UnauthorizedAccessException("Invalid token");
        }

        var claims = validationResult.Claims;
        var user = options.Users.FirstOrDefault(u => u.Id == claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value)
            ?? throw new OAuth2Exception("invalid_grant", "Invalid credentials");

        // Extract roles from Keycloak's realm_access claim
        var realmAccessJson = claims.FirstOrDefault(c => c.Type == "realm_access")?.Value;
        var realmAccess = !string.IsNullOrEmpty(realmAccessJson) ? JsonSerializer.Deserialize<KeyCloakRealmAccess>(realmAccessJson) : new KeyCloakRealmAccess();

        return new UserInfoResponse
        {
            Sub = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value,
            Name = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Name)?.Value,
            GivenName = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.GivenName)?.Value,
            FamilyName = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.FamilyName)?.Value,
            PreferredUsername = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.PreferredUsername)?.Value,
            Roles = realmAccess.Roles ?? [],
            Email = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value,
            EmailVerified = true  // keycloak emails are verified by default
            //Claims = user.Claims
        };
    }

    private class KeyCloakRealmAccess
    {
        public string[] Roles { get; set; }
    }
}
