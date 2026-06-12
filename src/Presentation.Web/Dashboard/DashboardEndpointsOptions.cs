// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

using System.Reflection;

public class DashboardEndpointsOptions : EndpointsOptionsBase
{
    public DashboardEndpointsOptions()
    {
        this.Enabled = true;
        this.GroupPath = "/_bdk/dashboard";
        this.GroupTag = "_bdk.Dashboard";
        this.Title = "BDK Dashboard";
        this.EndpointPaths = new DashboardEndpointPaths(); // Default endpoint paths
    }

    public DashboardEndpointPaths EndpointPaths { get; set; }

    public string Title { get; set; }

    public List<Assembly> PluginAssemblies { get; } = [];
}
