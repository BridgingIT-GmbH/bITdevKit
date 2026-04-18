// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Queueing;

using BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Configures the operational queueing endpoint group.
/// </summary>
/// <example>
/// <code>
/// services.AddQueueingEndpoints(options => options
///     .GroupPath("/api/_system/queueing")
///     .GroupTag("_System.Queueing")
///     .RequireAuthorization());
/// </code>
/// </example>
public class QueueingEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueueingEndpointsOptions"/> class with queueing-specific defaults.
    /// </summary>
    public QueueingEndpointsOptions()
    {
        this.GroupPath = "/api/_system/queueing";
        this.GroupTag = "_System.Queueing";
    }
}
