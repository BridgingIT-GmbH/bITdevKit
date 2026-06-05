// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Jobs.Dashboard;

using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Maps the Jobs dashboard plugin pages and content fragment routes.
/// </summary>
/// <example>
/// <code>
/// services.AddDashboard(options => options.WithPluginAssemblyContaining&lt;DashboardEndpoints&gt;());
/// </code>
/// </example>
public sealed class DashboardEndpoints(DashboardEndpointsOptions options) : EndpointsBase, IDashboardEndpoints
{
    internal const string JobsPath = "/jobs";
    internal const string JobsContentPath = "/jobs/content";

    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        options ??= new DashboardEndpointsOptions();

        if (!options.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, options)
            .WithTags("_bdk.Dashboard");

        group.MapDashboardPage<Pages.Index>(
            JobsPath,
            "_bdk.Dashboard.Jobs",
            "Dashboard Jobs",
            "Shows registered jobs with their triggers, occurrences, and management actions.");

        group.MapDashboardPage<Pages.Content>(
            JobsContentPath,
            "_bdk.Dashboard.JobsContent",
            "Dashboard Jobs Content",
            "Shows the jobs dashboard content fragment.");
    }

    internal static string BuildJobsPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, JobsPath);

    internal static string BuildJobsContentPath(DashboardEndpointsOptions opts) =>
        DashboardPath.Combine(opts?.GroupPath, JobsContentPath);
}
