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
using Microsoft.IdentityModel.Tokens;

public class EntraIdTokenService(FakeIdentityProviderEndpointsOptions options) : TokenServiceBase(options)
{
    public override string GenerateAccessToken(FakeUser user, string clientId, string scope)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            // Base claims similar to DefaultTokenService
            new(JwtRegisteredClaimNames.Aud, clientId ?? string.Empty),
            new(JwtRegisteredClaimNames.Iss, (this.options.Issuer?.TrimEnd('/')) ?? string.Empty), // should match Authority (appsettings) due to strict OIDC checking
            new(JwtRegisteredClaimNames.Sub, user.Id ?? string.Empty),
            new(JwtRegisteredClaimNames.Exp, now.Add(this.options.AccessTokenLifetime).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Name, user.Name ?? string.Empty),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),

            //new("scope", scope ?? "openid profile email roles offline_access"),
            new("scp", scope ?? "openid profile email roles offline_access"),
            new("roles", JsonSerializer.Serialize(user.Roles), JsonClaimValueTypes.JsonArray),

            // Essential EntraId claims
            new("tid", this.options.TenantId ?? string.Empty),
            new("ver", "2.0"),
            new("oid", user.Id ?? string.Empty)
        };

        // Roles claim
        //if (user.Roles?.Any() == true)
        //{
        //    claims.Add(new Claim("roles", JsonSerializer.Serialize(user.Roles), JsonClaimValueTypes.JsonArray));
        //}

        return this.CreateJwtToken(claims, this.options.AccessTokenLifetime);
    }

    public override string GenerateIdToken(FakeUser user, string clientId, string nonce = null)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            // Required OIDC claims
            new(JwtRegisteredClaimNames.Aud, clientId ?? string.Empty),
            new(JwtRegisteredClaimNames.Iss, (this.options.Issuer?.TrimEnd('/')) ?? string.Empty), // should match Authority (appsettings) due to strict OIDC checking
            new(JwtRegisteredClaimNames.Sub, user.Id ?? string.Empty),
            new(JwtRegisteredClaimNames.Exp, now.Add(this.options.AccessTokenLifetime).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
            new(JwtRegisteredClaimNames.Nbf, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),

            // Azure specific claims
            new("oid", user.Id ?? string.Empty),  // Object ID
            new("tid", this.options.TenantId ?? string.Empty),
            new("azp", clientId ?? string.Empty),
            new("aio", Guid.NewGuid().ToString("N")),
            new("ver", "2.0"),

            // User profile claims
            new(JwtRegisteredClaimNames.Name, user.Name ?? string.Empty),
            new(JwtRegisteredClaimNames.PreferredUsername, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.EmailVerified, "true", ClaimValueTypes.Boolean),
        };

        if (!string.IsNullOrEmpty(nonce))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Nonce, nonce));
        }

        // Name parts
        var nameParts = user.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (nameParts.Length > 0)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.GivenName, nameParts[0]));
        }

        if (nameParts.Length > 1)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.FamilyName, nameParts[1]));
        }

        // Roles claim
        if (user.Roles?.Any() == true)
        {
            claims.Add(new Claim("roles", JsonSerializer.Serialize(user.Roles), JsonClaimValueTypes.JsonArray));
        }

        return this.CreateJwtToken(claims, this.options.AccessTokenLifetime);
    }

    public override string GenerateRefreshToken(FakeUser user, string clientId, string scope)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Aud, clientId ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Iss, (this.options.Issuer?.TrimEnd('/')) ?? string.Empty), // should match Authority (appsettings) due to strict OIDC checking
            new Claim(JwtRegisteredClaimNames.Sub, user.Id ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Typ, "Refresh"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim("tid", this.options.TenantId ?? string.Empty),
            new Claim("scp", scope ?? "openid profile email roles offline_access"),
            new Claim("oid", user.Id ?? string.Empty),
            new Claim("azp", clientId ?? string.Empty)
        };

        return this.CreateJwtToken(claims, this.options.RefreshTokenLifetime);
    }

    public override string GenerateServiceToken(string clientId, string scope)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Aud, clientId ?? string.Empty),
            new(JwtRegisteredClaimNames.Iss, (this.options.Issuer?.TrimEnd('/')) ?? string.Empty), // should match Authority (appsettings) due to strict OIDC checking
            new(JwtRegisteredClaimNames.Sub, clientId ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
            new(JwtRegisteredClaimNames.Nbf, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),
            new(JwtRegisteredClaimNames.Exp, now.AddHours(1).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer),

            // Azure specific claims
            new("tid", this.options.TenantId ?? string.Empty),
            new("oid", clientId ?? string.Empty),  // For app-only tokens, clientId is used as oid
            new("azp", clientId ?? string.Empty),
            new("aio", Guid.NewGuid().ToString("N")),
            new("appid", clientId ?? string.Empty),
            new("ver", "2.0"),
            new("scp", scope ?? "api")
        };

        return this.CreateJwtToken(claims, TimeSpan.FromHours(1));
    }

    public override TokenValidationResult ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler
            {
                InboundClaimTypeMap = new Dictionary<string, string>() // Clear the mapping
            };

            var jwtToken = tokenHandler.ReadJwtToken(token);

            // Validate issuer (EntraID specific format)
            if (!this.options.Issuer.IsNullOrEmpty() &&
                jwtToken.Issuer != this.options.Issuer?.TrimEnd('/'))
            {
                return new TokenValidationResult
                {
                    IsValid = false,
                    Error = "Invalid issuer"
                };
            }

            // Validate audience
            if (!this.options.ClientId.IsNullOrEmpty() &&
                !jwtToken.Audiences.Contains(this.options.ClientId))
            {
                return new TokenValidationResult
                {
                    IsValid = false,
                    Error = "Invalid audience"
                };
            }

            // Validate expiration with 5-minute clock skew
            if (jwtToken.ValidTo < DateTime.UtcNow - TimeSpan.FromMinutes(5))
            {
                return new TokenValidationResult
                {
                    IsValid = false,
                    Error = "Token expired"
                };
            }

            // Validate signature if signing key is present
            if (!this.options.SigningKey.IsNullOrEmpty())
            {
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(this.options.SigningKey)),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = false
                };

                tokenHandler.ValidateToken(token, validationParameters, out _);
            }

            // EntraID specific validation
            var tidClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "tid");
            if (tidClaim == null || tidClaim.Value != this.options.TenantId)
            {
                return new TokenValidationResult
                {
                    IsValid = false,
                    Error = "Invalid tenant ID"
                };
            }

            return new TokenValidationResult
            {
                IsValid = true,
                Claims = jwtToken.Claims
            };
        }
        catch (SecurityTokenMalformedException exmf)
        {
            throw new UnauthorizedAccessException("Invalid token", exmf);
        }
        catch (Exception ex)
        {
            return new TokenValidationResult
            {
                IsValid = false,
                Error = ex.Message
            };
        }
    }
}