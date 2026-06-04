// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

public class DashboardEndpointPathsBuilder
{
    private readonly DashboardEndpointPaths _paths;

    public DashboardEndpointPathsBuilder()
    {
        this._paths = new DashboardEndpointPaths();
    }

    public DashboardEndpointPathsBuilder MetricsPath(string path)
    {
        this._paths.Metrics = path;
        return this;
    }

    public DashboardEndpointPaths Build()
    {
        return this._paths;
    }
}
