// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.JobScheduling;

public class JobSchedulingEndpointsOptions : EndpointsOptionsBase
{
    public JobSchedulingEndpointsOptions()
    {
        this.GroupPath = "/api/_system/jobs";
        this.GroupTag = "_system";
    }
}