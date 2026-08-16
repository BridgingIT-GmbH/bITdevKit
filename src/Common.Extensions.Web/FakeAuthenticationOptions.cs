// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Security.Claims;

/// <summary>
///     Configures the users and shared claims available to fake authentication.
/// </summary>
public class FakeAuthenticationOptions
{
    /// <summary>
    ///     Gets or sets whether fake authentication is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets the users that may be authenticated.
    /// </summary>
    public IReadOnlyCollection<FakeUser> Users { get; init; } = [];

    /// <summary>
    ///     Gets claims added to every authenticated fake user.
    /// </summary>
    public IReadOnlyCollection<Claim> Claims { get; init; } = [];
}
