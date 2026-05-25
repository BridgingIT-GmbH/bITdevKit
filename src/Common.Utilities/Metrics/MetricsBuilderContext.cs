// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides fluent configuration for the devkit metrics feature registration.
/// </summary>
/// <example>
/// <code>
/// services.AddMetrics(options => options
///     .Enabled(true)
///     .AddEndpoints(true));
/// </code>
/// </example>
public class MetricsBuilderContext(IServiceCollection services, MetricsOptions options)
{
    /// <summary>
    /// Gets the service collection being configured.
    /// </summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Gets the metrics feature options being configured.
    /// </summary>
    public MetricsOptions Options { get; } = options;
}
