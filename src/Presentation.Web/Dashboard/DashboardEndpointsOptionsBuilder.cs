// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

public class DashboardEndpointsOptionsBuilder
{
    private readonly DashboardEndpointsOptions options;

    public DashboardEndpointsOptionsBuilder()
    {
        this.options = new DashboardEndpointsOptions();
    }

    public DashboardEndpointsOptionsBuilder Enabled(bool enabled = true)
    {
        this.options.Enabled = enabled;

        return this;
    }

    public DashboardEndpointsOptionsBuilder WithGroupPath(string path)
    {
        this.options.GroupPath = path;

        return this;
    }

    public DashboardEndpointsOptionsBuilder WithGroupTag(string tag)
    {
        this.options.GroupTag = tag;

        return this;
    }

    /// <summary>
    /// Configure the endpoint paths for the dashboard.
    /// </summary>
    /// <param name="configurePaths"></param>
    /// <returns></returns>
    public DashboardEndpointsOptionsBuilder WithPaths(Action<DashboardEndpointPathsBuilder> configurePaths)
    {
        var pathsBuilder = new DashboardEndpointPathsBuilder();
        configurePaths(pathsBuilder);
        this.options.EndpointPaths = pathsBuilder.Build();

        return this;
    }

    /// <summary>
    /// Build the dashboard endpoint paths.
    /// </summary>
    /// <returns></returns>
    public DashboardEndpointsOptions Build()
    {
        return this.options;
    }
}
