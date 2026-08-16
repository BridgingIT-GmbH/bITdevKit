// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using Microsoft.AspNetCore.Authorization;

/// <summary>
/// Identifies a declaration with authorize roles metadata.
/// </summary>
public class AuthorizeRolesAttribute : AuthorizeAttribute
{
    /// <summary>
    /// Initializes a new instance of the <c>AuthorizeRolesAttribute</c> class.
    /// </summary>
    /// <param name="roles">The roles used by the operation.</param>
    public AuthorizeRolesAttribute(params string[] roles)
    {
        this.Roles = string.Join(",", roles);
    }
}
