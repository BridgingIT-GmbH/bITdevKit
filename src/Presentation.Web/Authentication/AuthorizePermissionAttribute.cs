// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Authorization;

/// <summary>
/// Identifies a declaration with authorize permission metadata.
/// </summary>
public class AuthorizePermissionAttribute : AuthorizeAttribute
{
    /// <summary>
    /// Initializes a new instance of the <c>AuthorizePermissionAttribute</c> class.
    /// </summary>
    /// <param name="permission">The permission used by the operation.</param>
    public AuthorizePermissionAttribute(string permission)
    {
        this.Policy = permission;
    }
}
