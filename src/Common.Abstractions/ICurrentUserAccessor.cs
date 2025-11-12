// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Security.Claims;

/// <summary>
///     The ICurrentUserAccessor interface provides mechanisms to access information about the current user.
///     Implementing classes will provide access to user details such as user ID, user name, email, and roles.
/// </summary>
public interface ICurrentUserAccessor
{
    /// <summary>
    ///     Gets the ClaimsPrincipal representing the current user.
    /// </summary>
    ClaimsPrincipal Principal { get; }

    /// <summary>
    ///     Gets a value indicating whether the current user is authenticated.
    ///     This property returns true if the user is authenticated; otherwise, false.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    ///     Gets the identifier for the current user.
    /// </summary>
    public string UserId { get; }

    /// <summary>
    ///     Gets the user name of the current user.
    /// </summary>
    public string UserName { get; }

    /// <summary>
    ///     Gets the email address of the current user.
    /// </summary>
    public string Email { get; }

    /// <summary>
    ///     Gets the roles associated with the current user.
    /// </summary>
    /// <value>
    ///     An array of strings representing the roles assigned to the current user.
    /// </value>
    public string[] Roles { get; }
}