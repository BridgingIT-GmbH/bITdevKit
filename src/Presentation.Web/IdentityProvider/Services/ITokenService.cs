// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;

public interface ITokenService
{
    string GenerateAccessToken(FakeUser user, string clientId, string scope);

    string GenerateRefreshToken(FakeUser user, string clientId, string scope);

    string GenerateIdToken(FakeUser user, string clientId, string nonce = null);

    string GenerateServiceToken(string clientId, string scope);

    TokenValidationResult ValidateToken(string token);

    TokenValidationResult ValidateRefreshToken(string refreshToken);
}
