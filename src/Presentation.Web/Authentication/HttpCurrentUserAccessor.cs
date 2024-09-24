// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Security.Claims;
using Common;
using Microsoft.AspNetCore.Http;

/// <summary>
///     The HttpCurrentUserAccessor class provides mechanisms to access information about the current user in the context
///     of an HTTP request.
///     This information includes user ID, user name, email, and user roles.
/// </summary>
public class HttpCurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    /// <summary>
    ///     Gets the User ID of the current user.
    /// </summary>
    /// <value>
    ///     The User ID is a unique identifier assigned to the user,
    ///     typically obtained from the security claims.
    /// </value>
    public string UserId => httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>
    ///     Gets the user name of the currently authenticated user.
    ///     The user name is determined from the authenticated user's claims.
    /// </summary>
    public string UserName => httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Name);

    /// <summary>
    ///     Gets the email address of the currently authenticated user.
    ///     Returns null if the email claim is not present in the user's claims.
    /// </summary>
    public string Email => httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email);

    /// <summary>
    ///     Gets the roles associated with the current user.
    ///     It retrieves the roles from the current HTTP context's user claims.
    /// </summary>
    public string[] Roles =>
        httpContextAccessor.HttpContext?.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
}