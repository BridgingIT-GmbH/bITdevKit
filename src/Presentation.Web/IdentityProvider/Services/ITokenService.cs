// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;

/// <summary>
/// Defines operations for i token service.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Executes the generate access token operation.
    /// </summary>
    /// <param name="user">The user used by the operation.</param>
    /// <param name="clientId">The client id used by the operation.</param>
    /// <param name="scope">The scope used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    string GenerateAccessToken(FakeUser user, string clientId, string scope);

    /// <summary>
    /// Executes the generate refresh token operation.
    /// </summary>
    /// <param name="user">The user used by the operation.</param>
    /// <param name="clientId">The client id used by the operation.</param>
    /// <param name="scope">The scope used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    string GenerateRefreshToken(FakeUser user, string clientId, string scope);

    /// <summary>
    /// Executes the generate id token operation.
    /// </summary>
    /// <param name="user">The user used by the operation.</param>
    /// <param name="clientId">The client id used by the operation.</param>
    /// <param name="nonce">The nonce used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    string GenerateIdToken(FakeUser user, string clientId, string nonce = null);

    /// <summary>
    /// Executes the generate service token operation.
    /// </summary>
    /// <param name="clientId">The client id used by the operation.</param>
    /// <param name="scope">The scope used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    string GenerateServiceToken(string clientId, string scope);

    /// <summary>
    /// Validates token.
    /// </summary>
    /// <param name="token">The token used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    TokenValidationResult ValidateToken(string token);

    /// <summary>
    /// Validates refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    TokenValidationResult ValidateRefreshToken(string refreshToken);
}
