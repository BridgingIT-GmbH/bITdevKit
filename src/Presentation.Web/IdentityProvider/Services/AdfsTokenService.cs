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

public class AdfsTokenService(FakeIdentityProviderEndpointsOptions options) : TokenServiceBase(options)
{
    public override string GenerateAccessToken(FakeUser user, string clientId, string scope)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
       {
           new(JwtRegisteredClaimNames.Aud, clientId ?? string.Empty),
           new(JwtRegisteredClaimNames.Iss, (this.options.Issuer?.TrimEnd('/')) ?? string.Empty),
           new(JwtRegisteredClaimNames.Sub, user.Id ?? string.Empty),
           new("upn", user.Email ?? string.Empty), // ADFS specific
           new("unique_name", user.Name ?? string.Empty), // ADFS specific
           new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
           new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
           new(JwtRegisteredClaimNames.AuthTime, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
           new(JwtRegisteredClaimNames.Exp, now.Add(this.options.AccessTokenLifetime).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
           new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
           new("appid", clientId ?? string.Empty), // ADFS specific
           new("ver", "1.0"), // ADFS specific
           new("scope", scope ?? "openid profile email offline_access")
       };

        // Add name parts
        var nameParts = user.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        claims.Add(new Claim(ClaimTypes.GivenName, nameParts[0])); // ADFS uses Windows claim types
        if (nameParts.Length > 1)
        {
            claims.Add(new Claim(ClaimTypes.Surname, nameParts[1])); // ADFS uses Windows claim types
        }

        if (user.Roles?.Any() == true)
        {
            claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        }

        return this.CreateJwtToken(claims, this.options.AccessTokenLifetime);
    }

    public override string GenerateRefreshToken(FakeUser user, string clientId, string scope)
    {
        var claims = new[]
        {
           new Claim(JwtRegisteredClaimNames.Aud, clientId ?? string.Empty),
           new Claim(JwtRegisteredClaimNames.Iss, (this.options.Issuer ?.TrimEnd('/')) ?? string.Empty),
           new Claim(JwtRegisteredClaimNames.Sub, user.Id ?? string.Empty),
           new Claim(JwtRegisteredClaimNames.Typ, "Refresh"),
           new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
           new Claim("scope", scope ?? "openid profile email offline_access"),
           new Claim("appid", clientId ?? string.Empty) // ADFS specific
       };

        return this.CreateJwtToken(claims, this.options.RefreshTokenLifetime);
    }

    public override string GenerateIdToken(FakeUser user, string clientId, string nonce = null)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
       {
           // Required claims
           new(JwtRegisteredClaimNames.Iss, (this.options.Issuer.TrimEnd('/')) ?? string.Empty),
           new(JwtRegisteredClaimNames.Sub, user.Id ?? string.Empty),
           new(JwtRegisteredClaimNames.Aud, clientId ?? string.Empty),
           new(JwtRegisteredClaimNames.Exp, now.AddHours(1).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
           new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
           new(JwtRegisteredClaimNames.AuthTime, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),

           // ADFS specific claims
           new("upn", user.Email ?? string.Empty),
           new("unique_name", user.Name ?? string.Empty),
           new("appid", clientId ?? string.Empty),
           new("ver", "1.0"),

           // Standard claims with ADFS claim types
           new(ClaimTypes.Name, user.Name ?? string.Empty),
           new(ClaimTypes.Email, user.Email ?? string.Empty),
           new("roles", JsonSerializer.Serialize(user.Roles), JsonClaimValueTypes.JsonArray)
       };

        if (!string.IsNullOrEmpty(nonce))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Nonce, nonce));
        }

        // Add name parts using Windows claim types
        var nameParts = user.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        claims.Add(new Claim(ClaimTypes.GivenName, nameParts[0]));
        if (nameParts.Length > 1)
        {
            claims.Add(new Claim(ClaimTypes.Surname, nameParts[1]));
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
           new("scope", scope ?? "api"),
           new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
           new(JwtRegisteredClaimNames.Exp, now.AddHours(1).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
           new("appid", clientId ?? string.Empty),
           new("ver", "1.0")
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
    //            ValidIssuer = this.options.Issuer?.TrimEnd('/'),
    //            ValidateAudience = !this.options.ClientId.IsNullOrEmpty(),
    //            ValidAudience = this.options.ClientId,
    //            ValidateLifetime = true,
    //            ClockSkew = TimeSpan.FromMinutes(5),
    //            NameClaimType = ClaimTypes.Name,
    //            RoleClaimType = ClaimTypes.Role
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