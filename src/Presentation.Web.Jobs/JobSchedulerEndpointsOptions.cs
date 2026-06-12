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
///         .GroupPath("/_bdk/api/jobs")
///         .GroupTag("_bdk/jobs")
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
        this.GroupPath = "/_bdk/api/jobs";
        this.GroupTag = "_bdk.Jobs";
        this.RequireAuthorization = true;
    }
}