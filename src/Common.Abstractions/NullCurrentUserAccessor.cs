// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Security.Claims;

/// <summary>
///     The NullCurrentUserAccessor class is an implementation of the ICurrentUserAccessor interface that provides
///     access to user details with all properties returning null. This implementation is typically used when there
///     is no current user context available or to avoid null reference exceptions.
/// </summary>
public class NullCurrentUserAccessor : ICurrentUserAccessor
{
    /// <summary>
    ///     Gets the principal representing the current user.
    ///     Returns null if there is no current user context.
    /// </summary>
    public ClaimsPrincipal Principal => null;

    /// <summary>
    ///     Gets a value indicating whether the current user is authenticated.
    ///     Returns false as there is no current user context.
    /// </summary>
    public bool IsAuthenticated => false;

    /// <summary>
    ///     Gets the unique identifier for the current user.
    ///     Returns null if there is no current user or the identifier is unavailable.
    /// </summary>
    public string UserId => null;

    /// <summary>
    ///     Gets the user name of the current user.
    /// </summary>
    /// <remarks>
    ///     This property returns the name of the currently logged-in user.
    ///     It may return null if no user is logged in or if the user does not have a name.
    /// </remarks>
    public string UserName => null;

    /// <summary>
    ///     Gets the email address of the current user.
    /// </summary>
    /// <remarks>
    ///     The email address can be used for identifying the user, sending notifications,
    ///     or other actions where user email is required.
    ///     Implementers should ensure that the email address is accurately provided and
    ///     conforms to a valid email format.
    ///     <example>
    ///         An implementer of this interface may access the email from different sources
    ///         such as HttpContext in a web application or a fake user accessor for testing purposes.
    ///     </example>
    /// </remarks>
    public string Email => null;

    /// <summary>
    ///     Gets the roles associated with the current user.
    /// </summary>
    public string[] Roles => null;
}