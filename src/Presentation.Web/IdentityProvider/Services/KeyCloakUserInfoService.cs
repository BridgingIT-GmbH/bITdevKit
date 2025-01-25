// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text.Json;
using BridgingIT.DevKit.Common;

// Keycloak implementation
public class KeyCloakUserInfoService(ITokenService tokenService, FakeIdentityProviderEndpointsOptions options)
    : IUserInfoService
{
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