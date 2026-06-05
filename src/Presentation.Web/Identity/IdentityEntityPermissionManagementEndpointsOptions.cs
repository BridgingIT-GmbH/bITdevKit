// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

public class IdentityEntityPermissionManagementEndpointsOptions : EndpointsOptionsBase
{
    public IdentityEntityPermissionManagementEndpointsOptions()
    {
        this.GroupPath = "/_bdk/api/identity/management/entities/permissions";
        this.GroupTag = "_bdk/identity/management";
        this.RequireAuthorization = true;
    }
}