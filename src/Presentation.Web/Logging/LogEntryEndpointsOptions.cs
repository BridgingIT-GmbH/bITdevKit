// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
///     Configures the built-in log entry endpoint group.
/// </summary>
/// <remarks>
///     The constructor enables authorization and sets the default group path to <c>/_bdk/api/logentries</c>. Route
///     handlers use these options when the endpoint group is created.
/// </remarks>
public class LogEntryEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="LogEntryEndpointsOptions" /> class with secure system-log defaults.
    /// </summary>
    public LogEntryEndpointsOptions()
    {
        this.GroupPath = "/_bdk/api/logentries";
        this.GroupTag = "_bdk/logentries";
        this.RequireAuthorization = true;
    }
}
