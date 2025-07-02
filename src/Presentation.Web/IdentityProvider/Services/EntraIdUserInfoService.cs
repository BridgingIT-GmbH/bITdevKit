// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;

public class EntraIdUserInfoService(ITokenService tokenService, FakeIdentityProviderEndpointsOptions options)
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
        var user = options.Users.FirstOrDefault(u => u.Id == claims.FirstOrDefault(c => c.Type == "oid")?.Value)
            ?? throw new OAuth2Exception("invalid_grant", "Invalid credentials");

        return new UserInfoResponse
        {
            Sub = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value,
            Name = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Name)?.Value,
            GivenName = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.GivenName)?.Value,
            FamilyName = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.FamilyName)?.Value,
            PreferredUsername = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.PreferredUsername)?.Value,
            Email = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value,
            EmailVerified = true, // Azure AD emails are verified by default
            Roles = [.. claims.Where(c => c.Type == ClaimTypes.Role || c.Type == "roles").Select(c => c.Value)],
            //Claims = user.Claims
        };
    }
}