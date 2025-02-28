// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

public class IdentityEntityPermissionEvaluationEndpointsOptions : EndpointsOptionsBase
{
    public bool BypassCache { get; set; }

    public IdentityEntityPermissionEvaluationEndpointsOptions()
    {
        this.GroupPath = "/api/_system/identity/entities/permissions/evaluate";
        this.GroupTag = "_system/identity";
        this.RequireAuthorization = true;
    }
}
