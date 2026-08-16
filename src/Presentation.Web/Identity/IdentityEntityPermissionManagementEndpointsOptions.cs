// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Configures identity entity permission management endpoints.
/// </summary>
public class IdentityEntityPermissionManagementEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>
    /// Initializes a new instance of the <c>IdentityEntityPermissionManagementEndpointsOptions</c> class.
    /// </summary>
    public IdentityEntityPermissionManagementEndpointsOptions()
    {
        this.GroupPath = "/_bdk/api/identity/management/entities/permissions";
        this.GroupTag = "_bdk.Identity.Management";
        this.RequireAuthorization = true;
    }
}
