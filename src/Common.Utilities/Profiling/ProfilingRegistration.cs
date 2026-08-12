// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Continues fluent configuration of the one host-wide profiling feature.
/// </summary>
/// <example><code>services.AddProfiling(options => options.Enabled());</code></example>
public sealed class ProfilingBuilderContext
{
    /// <summary>Creates a builder context over the configured services and shared options.</summary>
    /// <param name="services">The service collection being configured.</param>
    /// <param name="options">The shared options instance.</param>
    /// <example><code>var context = new ProfilingBuilderContext(services, options);</code></example>
    public ProfilingBuilderContext(IServiceCollection services, ProfilingOptions options)
    {
        this.Services = services ?? throw new ArgumentNullException(nameof(services));
        this.Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>Gets the service collection being configured.</summary>
    /// <example><code>var services = context.Services;</code></example>
    public IServiceCollection Services { get; }

    /// <summary>Gets the shared mutable options instance.</summary>
    /// <example><code>var enabled = context.Options.Enabled;</code></example>
    public ProfilingOptions Options { get; }
}
