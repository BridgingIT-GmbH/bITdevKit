// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Jobs;

using BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Configures the operational jobs endpoint group.
/// </summary>
/// <example>
/// <code>
/// services.AddJobScheduler()
///     .AddEndpoints(options => options
///         .GroupPath("/api/_system/jobs")
///         .GroupTag("_system/jobs")
///         .RequireAuthorization());
/// </code>
/// </example>
public class JobSchedulerEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JobSchedulerEndpointsOptions" /> class with jobs defaults.
    /// </summary>
    public JobSchedulerEndpointsOptions()
    {
        this.GroupPath = "/api/_system/jobs";
        this.GroupTag = "_system/jobs";
        this.RequireAuthorization = true;
    }
}