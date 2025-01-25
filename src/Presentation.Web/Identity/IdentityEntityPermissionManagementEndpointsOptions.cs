// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

public class IdentityEntityPermissionManagementEndpointsOptions : EndpointsOptionsBase
{
    public IdentityEntityPermissionManagementEndpointsOptions()
    {
        this.GroupPath = "/api/_system/identity/management/entities/permissions";
        this.GroupTag = "_system/identity/management";
        this.RequireAuthorization = true;
    }
}