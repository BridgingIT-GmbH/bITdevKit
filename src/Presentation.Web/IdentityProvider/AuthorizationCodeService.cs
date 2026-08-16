// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System;
using System.Collections.Generic;
using BridgingIT.DevKit.Common;

/// <summary>
/// Defines operations for i authorization code service.
/// </summary>
public interface IAuthorizationCodeService
{
    /// <summary>
    /// Executes the generate code operation.
    /// </summary>
    /// <param name="user">The user used by the operation.</param>
    /// <param name="request">The request used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    string GenerateCode(FakeUser user, AuthorizeRequest request);

    /// <summary>
    /// Validates code.
    /// </summary>
    /// <param name="code">The code used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    AuthorizationCodeModel ValidateCode(string code);
}

/// <summary>
/// Represents authorization code service.
/// </summary>
public class AuthorizationCodeService : IAuthorizationCodeService
{
    private readonly Dictionary<string, AuthorizationCodeModel> authCodes = [];

    /// <summary>
    /// Executes the generate code operation.
    /// </summary>
    /// <param name="user">The user used by the operation.</param>
    /// <param name="request">The request used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public string GenerateCode(FakeUser user, AuthorizeRequest request)
    {
        //var code = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var code = Guid.NewGuid().ToString("N");

        this.authCodes[code] = new AuthorizationCodeModel
        {
            UserId = user.Id,
            ClientId = request.ClientId,
            RedirectUri = request.RedirectUri,
            Scope = request.Scope,
            Nonce = request.Nonce,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        return code;
    }

    /// <summary>
    /// Validates code.
    /// </summary>
    /// <param name="code">The code used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public AuthorizationCodeModel ValidateCode(string code)
    {
        if (!this.authCodes.TryGetValue(code, out var data))
        {
            throw new OAuth2Exception("invalid_grant", "Invalid authorization code (data)");
        }

        if (data.ExpiresAt < DateTime.UtcNow)
        {
            throw new OAuth2Exception("invalid_grant", "Authorization code expired");
        }

        this.authCodes.Remove(code); // Remove code after use (one-time use)

        return data;
    }
}
