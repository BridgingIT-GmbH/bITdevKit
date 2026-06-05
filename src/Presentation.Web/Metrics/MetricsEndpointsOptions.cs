// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

/// <summary>
/// Configures the system metrics endpoints.
/// </summary>
/// <example>
/// <code>
/// services.AddMetrics(options => options
///     .Enabled(true)
///     .UseEndpoints(true));
/// </code>
/// </example>
public class MetricsEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MetricsEndpointsOptions"/> class with the default system metrics route.
    /// </summary>
    public MetricsEndpointsOptions()
    {
        this.GroupPath = "/_bdk/api/metrics";
        this.GroupTag = "_bdk.Metrics";
        this.RequireAuthorization = true;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the devkit meter endpoint is enabled.
    /// </summary>
    public bool BdkEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the overview endpoint is enabled.
    /// </summary>
    public bool OverviewEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether .NET runtime metrics exposure is enabled.
    /// </summary>
    public bool DotNetEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether ASP.NET metrics exposure is enabled.
    /// </summary>
    public bool AspNetEnabled { get; set; } = true;
}

/// <summary>
/// Provides fluent configuration for <see cref="MetricsEndpointsOptions"/>.
/// </summary>
public class MetricsEndpointsOptionsBuilder : EndpointsOptionsBuilderBase<MetricsEndpointsOptions, MetricsEndpointsOptionsBuilder>
{
    /// <summary>
    /// Enables or disables the app meter endpoint.
    /// </summary>
    /// <param name="enabled">Indicates whether the endpoint should be enabled.</param>
    /// <returns>The current builder.</returns>
    public MetricsEndpointsOptionsBuilder EnableApp(bool enabled = true)
    {
        this.Target.BdkEnabled = enabled;

        return this;
    }

    /// <summary>
    /// Enables or disables the overview endpoint.
    /// </summary>
    /// <param name="enabled">Indicates whether the endpoint should be enabled.</param>
    /// <returns>The current builder.</returns>
    public MetricsEndpointsOptionsBuilder EnableOverview(bool enabled = true)
    {
        this.Target.OverviewEnabled = enabled;

        return this;
    }

    /// <summary>
    /// Enables or disables the .NET runtime metrics endpoint.
    /// </summary>
    /// <param name="enabled">Indicates whether the endpoint should be enabled.</param>
    /// <returns>The current builder.</returns>
    public MetricsEndpointsOptionsBuilder EnableDotNet(bool enabled = true)
    {
        this.Target.DotNetEnabled = enabled;

        return this;
    }

    /// <summary>
    /// Enables or disables the ASP.NET metrics endpoint.
    /// </summary>
    /// <param name="enabled">Indicates whether the endpoint should be enabled.</param>
    /// <returns>The current builder.</returns>
    public MetricsEndpointsOptionsBuilder EnableAspNet(bool enabled = true)
    {
        this.Target.AspNetEnabled = enabled;

        return this;
    }
}
