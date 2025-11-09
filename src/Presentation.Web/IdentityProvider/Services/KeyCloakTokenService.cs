// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using BridgingIT.DevKit.Common;

public class KeyCloakTokenService(FakeIdentityProviderEndpointsOptions options) : TokenServiceBase(options)
{
    public override string GenerateAccessToken(FakeUser user, string clientId, string scope)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Aud, clientId ?? string.Empty),
            //new(JwtRegisteredClaimNames.Iss, $"{this.options.Issuer}/realms/{this.options.RealmName}"),
            new(JwtRegisteredClaimNames.Iss, (this.options.Issuer?.TrimEnd('/')) ?? string.Empty),
            new(JwtRegisteredClaimNames.Sub, user.Id ?? string.Empty),
            new(JwtRegisteredClaimNames.Exp, now.Add(this.options.AccessTokenLifetime).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
            new(JwtRegisteredClaimNames.AuthTime, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
            new("scope", scope ??"openid profile email roles offline_access"),
            new(JwtRegisteredClaimNames.Name, user.Name ?? string.Empty),
            new(JwtRegisteredClaimNames.PreferredUsername, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.EmailVerified, "true", ClaimValueTypes.Boolean),
            new(JwtRegisteredClaimNames.Typ, "Bearer"),
            new(JwtRegisteredClaimNames.Azp, clientId ?? string.Empty),
            new(JwtRegisteredClaimNames.Sid, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        if (!string.IsNullOrEmpty(clientId))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Aud, "account")); // Secondary audience
        }

        var nameParts = user.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        claims.Add(new Claim(JwtRegisteredClaimNames.GivenName, nameParts[0]));
        if (nameParts.Length > 1)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.FamilyName, nameParts[1]));
        }

        if (user.Roles?.Any() == true)
        {
            // Keycloak specific role format
            claims.Add(new Claim("realm_access", JsonSerializer.Serialize(new { roles = user.Roles })));

            if (!string.IsNullOrEmpty(clientId))
            {
                claims.Add(new Claim("resource_access", JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    [clientId] = new { roles = user.Roles }
                })));
            }
        }

        return this.CreateJwtToken(claims, this.options.AccessTokenLifetime);
    }

    public override string GenerateIdToken(FakeUser user, string clientId, string nonce = null)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id ?? string.Empty),
            //new(JwtRegisteredClaimNames.Iss, $"{this.options.Issuer}/realms/{this.options.RealmName}"),
            new(JwtRegisteredClaimNames.Iss, (this.options.Issuer?.TrimEnd('/')) ?? string.Empty),
            new(JwtRegisteredClaimNames.Aud, clientId ?? string.Empty),
            new(JwtRegisteredClaimNames.Exp, now.Add(this.options.AccessTokenLifetime).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
            new(JwtRegisteredClaimNames.AuthTime, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
            new(JwtRegisteredClaimNames.Name, user.Name ?? string.Empty),
            new(JwtRegisteredClaimNames.GivenName, user.Name.Split(' ')[0] ?? string.Empty),
            new(JwtRegisteredClaimNames.FamilyName, user.Name.Split(' ').Length > 1 ? user.Name.Split(' ')[1] ?? string.Empty : string.Empty),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.EmailVerified, "true", ClaimValueTypes.Boolean),
            new(JwtRegisteredClaimNames.PreferredUsername, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Sid, Guid.NewGuid().ToString("N")),
            new("acr", "1"), // Authentication Context Class Reference
            new("s_hash", Convert.ToBase64String(Guid.NewGuid().ToByteArray())), // State hash
            new("roles", JsonSerializer.Serialize(user.Roles), JsonClaimValueTypes.JsonArray)
        };

        if (!string.IsNullOrEmpty(nonce))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Nonce, nonce));
        }

        foreach (var claim in user.Claims.SafeNull())
        {
            claims.Add(new Claim(claim.Key, claim.Value));
        }

        return this.CreateJwtToken(claims, this.options.AccessTokenLifetime);
    }

    public override string GenerateRefreshToken(FakeUser user, string clientId, string scope)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Aud, clientId ?? string.Empty),
            //new Claim(JwtRegisteredClaimNames.Iss, $"{this.options.Issuer}/realms/{this.options.RealmName}"),
            new(JwtRegisteredClaimNames.Iss, (this.options.Issuer?.TrimEnd('/')) ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Typ, "Refresh"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim("scope", scope ??"openid profile email roles offline_access"),
            new(JwtRegisteredClaimNames.Sid, Guid.NewGuid().ToString("N"))
        };

        return this.CreateJwtToken(claims, this.options.RefreshTokenLifetime);
    }

    public override string GenerateServiceToken(string clientId, string scope)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Aud, clientId ?? string.Empty),
            //new(JwtRegisteredClaimNames.Iss, $"{this.options.Issuer}/realms/{this.options.RealmName}"),
            new(JwtRegisteredClaimNames.Iss, (this.options.Issuer?.TrimEnd('/')) ?? string.Empty),
            new(JwtRegisteredClaimNames.Sub, clientId ?? string.Empty),
            new(JwtRegisteredClaimNames.Typ, "Bearer"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Aud, "account"), // Keycloak specific
            new("scope", scope ?? "api"),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
            new(JwtRegisteredClaimNames.Exp, now.AddHours(1).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
            // Keycloak specific service claims
            new("realm_access", JsonSerializer.Serialize(new { roles = new[] { "service" } })),
            new("azp", clientId)
        };

        return this.CreateJwtToken(claims, TimeSpan.FromHours(1));
    }

    //public override TokenValidationResult ValidateToken(string token)
    //{
    //    try
    //    {
    //        var tokenHandler = new JwtSecurityTokenHandler
    //        {
    //            InboundClaimTypeMap = new Dictionary<string, string>() // Clear the mapping -> prevents claim type mapping
    //        };

    //        var validationParameters = new TokenValidationParameters
    //        {
    //            ValidateIssuer = !this.options.Issuer.IsNullOrEmpty(),
    //            ValidIssuer = $"{this.options.Issuer}/realms/{this.options.RealmName}",
    //            ValidateAudience = !this.options.ClientId.IsNullOrEmpty(),
    //            ValidAudiences = ["account", this.options.ClientId], // Keycloak can have multiple valid audiences
    //            ValidateLifetime = true,
    //            ClockSkew = TimeSpan.FromMinutes(5)
    //        };

    //        if (!this.options.SigningKey.IsNullOrEmpty())
    //        {
    //            validationParameters.ValidateIssuerSigningKey = true;
    //            validationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this.options.SigningKey));
    //        }

    //        var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
    //        return new TokenValidationResult { IsValid = true, Claims = principal.Claims };
    //    }
    //    catch (Exception ex)
    //    {
    //        return new TokenValidationResult { IsValid = false, Error = ex.Message };
    //    }
    //}
}