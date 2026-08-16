// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BridgingIT.DevKit.Common;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Represents token service base.
/// </summary>
/// <param name="options">The options controlling the operation.</param>
public abstract class TokenServiceBase(FakeIdentityProviderEndpointsOptions options) : ITokenService
{
    /// <summary>
    /// Stores the options.
    /// </summary>
    protected readonly FakeIdentityProviderEndpointsOptions options = options;

    /// <summary>
    /// Executes the generate access token operation.
    /// </summary>
    /// <param name="user">The user used by the operation.</param>
    /// <param name="clientId">The client id used by the operation.</param>
    /// <param name="scope">The scope used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public abstract string GenerateAccessToken(FakeUser user, string clientId, string scope);

    /// <summary>
    /// Executes the generate id token operation.
    /// </summary>
    /// <param name="user">The user used by the operation.</param>
    /// <param name="clientId">The client id used by the operation.</param>
    /// <param name="nonce">The nonce used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public abstract string GenerateIdToken(FakeUser user, string clientId, string nonce = null);

    /// <summary>
    /// Executes the generate refresh token operation.
    /// </summary>
    /// <param name="user">The user used by the operation.</param>
    /// <param name="clientId">The client id used by the operation.</param>
    /// <param name="scope">The scope used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public abstract string GenerateRefreshToken(FakeUser user, string clientId, string scope);

    /// <summary>
    /// Executes the generate service token operation.
    /// </summary>
    /// <param name="clientId">The client id used by the operation.</param>
    /// <param name="scope">The scope used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public abstract string GenerateServiceToken(string clientId, string scope);

    /// <summary>
    /// Validates token.
    /// </summary>
    /// <param name="token">The token used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual TokenValidationResult ValidateToken(string token)
    {
        try
        {
            var now = DateTime.UtcNow;
            var tokenHandler = new JwtSecurityTokenHandler
            {
                InboundClaimTypeMap = new Dictionary<string, string>() // Clear the mapping
            };

            var jwtToken = tokenHandler.ReadJwtToken(token);

            if (!this.options.Issuer.IsNullOrEmpty() &&
                jwtToken.Issuer != this.options.Issuer?.TrimEnd('/'))
            {
                return new TokenValidationResult
                {
                    IsValid = false,
                    Error = "Invalid issuer"
                };
            }

            if (!this.options.ClientId.IsNullOrEmpty() &&
                !jwtToken.Audiences.Contains(this.options.ClientId))
            {
                return new TokenValidationResult
                {
                    IsValid = false,
                    Error = "Invalid audience"
                };
            }

            if (jwtToken.ValidTo < now - TimeSpan.FromMinutes(5))
            {
                return new TokenValidationResult
                {
                    IsValid = false,
                    Error = "Token expired"
                };
            }

            if (!this.options.SigningKey.IsNullOrEmpty())
            {
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this.options.SigningKey)),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = false
                };

                tokenHandler.ValidateToken(token, validationParameters, out _);
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

    /// <summary>
    /// Validates refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public virtual TokenValidationResult ValidateRefreshToken(string refreshToken)
    {
        try
        {
            var now = DateTime.UtcNow;
            var tokenHandler = new JwtSecurityTokenHandler
            {
                InboundClaimTypeMap = new Dictionary<string, string>() // Prevent default claim type mapping
            };

            var jwtToken = tokenHandler.ReadJwtToken(refreshToken);

            // Check if this is actually a refresh token
            if (jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Typ)?.Value != "Refresh")
            {
                return new TokenValidationResult
                {
                    IsValid = false,
                    Error = "Invalid token type"
                };
            }

            // Validate issuer if configured
            if (!this.options.Issuer.IsNullOrEmpty() &&
                jwtToken.Issuer != this.options.Issuer?.TrimEnd('/'))
            {
                return new TokenValidationResult
                {
                    IsValid = false,
                    Error = "Invalid issuer"
                };
            }

            // Validate expiration with 5-minute clock skew
            if (jwtToken.ValidTo < now - TimeSpan.FromMinutes(5))
            {
                return new TokenValidationResult
                {
                    IsValid = false,
                    Error = "Token expired"
                };
            }

            // Validate signature if signing key is configured
            if (!this.options.SigningKey.IsNullOrEmpty())
            {
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this.options.SigningKey)),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = false
                };

                tokenHandler.ValidateToken(refreshToken, validationParameters, out _);
            }

            return new TokenValidationResult
            {
                IsValid = true,
                Claims = jwtToken.Claims
            };
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

    /// <summary>
    /// Creates jwt token.
    /// </summary>
    /// <param name="claims">The claims used by the operation.</param>
    /// <param name="expiration">The expiration used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    protected string CreateJwtToken(IEnumerable<Claim> claims, TimeSpan? expiration = null) // https://cloudentity.com/developers/basics/tokens/json-web-tokens/
    {
        var now = DateTime.UtcNow;
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            //Issuer = this.options.Issuer?.TrimEnd('/'), // causes empty tokens
            Subject = new ClaimsIdentity(claims),
            Expires = expiration.HasValue ? now.Add(expiration.Value) : null,
            IssuedAt = now
            //NotBefore = now
        };

        if (!this.options.SigningKey.IsNullOrEmpty())
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this.options.SigningKey));
            tokenDescriptor.SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);
        }

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
