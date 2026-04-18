// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.JobScheduling;

/// <summary>
/// Configures the operational job scheduling endpoint group.
/// </summary>
/// <example>
/// <code>
/// services.AddJobScheduling()
///     .AddEndpoints(options => options
///         .GroupPath("/api/_system/jobs")
///         .GroupTag("_system/jobs")
///         .RequireAuthorization());
/// </code>
/// </example>
public class JobSchedulingEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JobSchedulingEndpointsOptions" /> class with job scheduling defaults.
    /// </summary>
    public JobSchedulingEndpointsOptions()
    {
        this.GroupPath = "/api/_system/jobs";
        this.GroupTag = "_system/jobs";
    }
}
