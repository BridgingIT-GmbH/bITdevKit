// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Endpoints;

/// <summary>
/// Configuration options for the log endpoints.
/// </summary>
public class LogEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LogEndpointsOptions"/> class.
    /// </summary>
    public LogEndpointsOptions()
    {
        this.GroupPath = "/api/_system/logging";
        this.GroupTag = "_system/logging";
        this.RequireAuthorization = false;
    }
}
