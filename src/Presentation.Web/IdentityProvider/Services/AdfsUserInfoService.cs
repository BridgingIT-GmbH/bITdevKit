// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;

/// <summary>
/// Service to get user information from a token for ADFS
/// </summary>
/// <param name="tokenService"></param>
/// <param name="options"></param>
public class AdfsUserInfoService(ITokenService tokenService, FakeIdentityProviderEndpointsOptions options)
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
            Name = claims.FirstOrDefault(c => c.Type == "unique_name")?.Value,
            GivenName = claims.FirstOrDefault(c => c.Type == ClaimTypes.GivenName)?.Value,
            FamilyName = claims.FirstOrDefault(c => c.Type == ClaimTypes.Surname)?.Value,
            PreferredUsername = claims.FirstOrDefault(c => c.Type == "upn")?.Value,
            Roles = [.. claims.Where(c => c.Type == ClaimTypes.Role || c.Type == "roles").Select(c => c.Value)],
            Email = claims.FirstOrDefault(c => c.Type == "upn")?.Value,
            EmailVerified = true  // ADFS/AD emails are verified by default
        };
    }
}
