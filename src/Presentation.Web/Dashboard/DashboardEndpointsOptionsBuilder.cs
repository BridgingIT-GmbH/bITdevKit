// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Dashboard;

using System.Reflection;

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

    public DashboardEndpointsOptionsBuilder WithTitle(string title)
    {
        this.options.Title = string.IsNullOrWhiteSpace(title) ? "BDK Dashboard" : title.Trim();

        return this;
    }

    /// <summary>
    /// Adds an assembly that contains dashboard plugin endpoints.
    /// </summary>
    /// <param name="assembly">The assembly to scan for <see cref="IDashboardEndpoints" /> implementations.</param>
    /// <returns>The same builder instance.</returns>
    public DashboardEndpointsOptionsBuilder WithPluginAssembly(Assembly assembly)
    {
        if (assembly is not null && !this.options.PluginAssemblies.Contains(assembly))
        {
            this.options.PluginAssemblies.Add(assembly);
        }

        return this;
    }

    /// <summary>
    /// Adds assemblies that contain dashboard plugin endpoints.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan for <see cref="IDashboardEndpoints" /> implementations.</param>
    /// <returns>The same builder instance.</returns>
    public DashboardEndpointsOptionsBuilder WithPluginAssemblies(params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies ?? [])
        {
            this.WithPluginAssembly(assembly);
        }

        return this;
    }

    /// <summary>
    /// Adds the assembly containing <typeparamref name="T" /> as a dashboard plugin assembly.
    /// </summary>
    /// <typeparam name="T">A marker type from the plugin assembly.</typeparam>
    /// <returns>The same builder instance.</returns>
    public DashboardEndpointsOptionsBuilder WithPluginAssemblyContaining<T>()
    {
        return this.WithPluginAssembly(typeof(T).Assembly);
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
