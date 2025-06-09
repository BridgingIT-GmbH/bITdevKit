// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Endpoints;

/// <summary>
/// Configuration options for the log endpoints.
/// </summary>
public class LogEntryEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LogEntryEndpointsOptions"/> class.
    /// </summary>
    public LogEntryEndpointsOptions()
    {
        this.GroupPath = "/api/_system/logentries";
        this.GroupTag = "_system/logentries";
        this.RequireAuthorization = true;
    }
}
