// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Defines operations for i user info service.
/// </summary>
public interface IUserInfoService
{
    /// <summary>
    /// Gets user info.
    /// </summary>
    /// <param name="accessToken">The access token used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    UserInfoResponse GetUserInfo(string accessToken);
}
