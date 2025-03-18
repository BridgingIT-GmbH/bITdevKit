// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using BridgingIT.DevKit.Common;

public class DefaultTokenService(FakeIdentityProviderEndpointsOptions options) : TokenServiceBase(options)
{
    public override string GenerateAccessToken(FakeUser user, string clientId, string scope) // https://cloudentity.com/developers/basics/tokens/access-token/
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Aud, clientId ?? string.Empty),
            new(JwtRegisteredClaimNames.Iss, (this.options.Issuer?.TrimEnd('/')) ?? string.Empty), // should match Authority (appsettings) due to strict OIDC checking
            new(JwtRegisteredClaimNames.Sub, user.Id ?? string.Empty),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Name, user.Name ?? string.Empty),
            new(JwtRegisteredClaimNames.PreferredUsername, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.EmailVerified, "true", ClaimValueTypes.Boolean),
            new(JwtRegisteredClaimNames.AuthTime, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
            new(JwtRegisteredClaimNames.Exp, now.Add(this.options.AccessTokenLifetime).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
            new("scope", scope ?? "openid profile email roles offline_access"),
            new("roles", JsonSerializer.Serialize(user.Roles), JsonClaimValueTypes.JsonArray)
        };

        // Add name parts
        var nameParts = user.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        claims.Add(new Claim(JwtRegisteredClaimNames.GivenName, nameParts[0]));
        if (nameParts.Length > 1)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.FamilyName, nameParts[1]));
        }

        //if (user.Roles?.Any() == true)
        //{
        //    //claims.Add(new Claim("roles", JsonSerializer.Serialize(user.Roles)));
        //    claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        //}

        return this.CreateJwtToken(claims, this.options.AccessTokenLifetime);
    }

    public override string GenerateRefreshToken(FakeUser user, string clientId, string scope) // https://cloudentity.com/developers/basics/tokens/refresh-token/
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Aud, clientId ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Iss, (this.options.Issuer?.TrimEnd('/')) ?? string.Empty), // should match Authority (appsettings) due to strict OIDC checking
            new Claim(JwtRegisteredClaimNames.Sub, user.Id ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Typ, "Refresh"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim("scope", scope ?? "openid profile email roles offline_access")
        };

        return this.CreateJwtToken(claims, this.options.RefreshTokenLifetime);
    }

    public override string GenerateIdToken(FakeUser user, string clientId, string nonce = null) // https://cloudentity.com/developers/basics/tokens/id-token/
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            // Required claims
            new(JwtRegisteredClaimNames.Iss, (this.options.Issuer.TrimEnd('/')) ?? string.Empty), // should match Authority (appsettings) due to strict OIDC checking
            new(JwtRegisteredClaimNames.Sub, user.Id ?? string.Empty),
            new(JwtRegisteredClaimNames.Aud, clientId ?? string.Empty),  // Must match the client_id
            new(JwtRegisteredClaimNames.Exp, now.AddHours(1).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),

            // Recommended claims
            new(JwtRegisteredClaimNames.Name, user.Name ?? string.Empty),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.EmailVerified, "true", ClaimValueTypes.Boolean),
            new(JwtRegisteredClaimNames.AuthTime, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
            new("roles", JsonSerializer.Serialize(user.Roles), JsonClaimValueTypes.JsonArray)
        };

        if (!string.IsNullOrEmpty(nonce))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Nonce, nonce));
        }

        // Add name parts
        var nameParts = user.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        claims.Add(new Claim(JwtRegisteredClaimNames.GivenName, nameParts[0]));
        if (nameParts.Length > 1)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.FamilyName, nameParts[1]));
        }

        foreach (var claim in user.Claims.SafeNull())
        {
            claims.Add(new Claim(claim.Key, claim.Value));
        }

        return this.CreateJwtToken(claims, this.options.AccessTokenLifetime);
    }

    public override string GenerateServiceToken(string clientId, string scope)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Iss, (this.options.Issuer ?.TrimEnd('/')) ?? string.Empty),
            new(JwtRegisteredClaimNames.Sub, clientId ?? string.Empty),
            new(JwtRegisteredClaimNames.Aud, clientId ?? string.Empty),
            new(JwtRegisteredClaimNames.Typ, "Bearer"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
            new(JwtRegisteredClaimNames.Exp, now.AddHours(1).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
            new("scope", scope ?? "api")
        };

        return this.CreateJwtToken(claims, TimeSpan.FromHours(1));
    }
}