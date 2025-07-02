// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;

/// <summary>
/// Service to get user information from a token
/// </summary>
/// <param name="tokenService"></param>
/// <param name="options"></param>
public class DefaultUserInfoService(ITokenService tokenService, FakeIdentityProviderEndpointsOptions options)
    : IUserInfoService
{
    public UserInfoResponse GetUserInfo(string accessToken)
    {
        var validationResult = tokenService.ValidateToken(accessToken);
        if (!validationResult.IsValid)
        {
            throw new OAuth2Exception("Invalid token", validationResult.Error);
        }

        var claims = validationResult.Claims;
        var user = options.Users.FirstOrDefault(u => u.Id == claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value)
            ?? throw new OAuth2Exception("invalid_grant", "Invalid credentials");

        return new UserInfoResponse
        {
            Sub = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value,
            Name = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Name)?.Value,
            GivenName = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.GivenName)?.Value,
            FamilyName = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.FamilyName)?.Value,
            PreferredUsername = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.PreferredUsername)?.Value,
            Email = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value,
            EmailVerified = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.EmailVerified)?.Value == "true",
            Roles = [.. claims.Where(c => c.Type == "roles").Select(c => c.Value)],
            //Claims = user.Claims
        };
    }
}
