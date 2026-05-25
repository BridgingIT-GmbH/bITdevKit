// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
/// <summary>
/// Provides backward-compatible web endpoint integration for the shared metrics builder context.
/// </summary>
public static class MetricsBuilderExtensions
{
    /// <summary>
    /// Enables or disables the system metrics endpoints for the current metrics registration.
    /// </summary>
    /// <param name="context">The metrics builder context.</param>
    /// <param name="enabled">Indicates whether the endpoints should be registered.</param>
    /// <param name="configure">An optional endpoints configuration callback.</param>
    /// <returns>The current metrics builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddMetrics(options => options
    ///     .Enabled(true)
    ///     .AddEndpoints(true));
    /// </code>
    /// </example>
    [Obsolete("Configure metrics endpoints via AddMetrics(options => options.AddEndpoints(...)) instead.")]
    public static MetricsBuilderContext UseEndpoints(
        this MetricsBuilderContext context,
        bool enabled = true,
        Action<MetricsEndpointsOptionsBuilder> configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Options.EndpointsEnabled = enabled;

        if (!enabled || !context.Options.Enabled)
        {
            return context;
        }

        if (configure is null)
        {
            context.Services.AddMetricsEndpoints();
        }
        else
        {
            context.Services.AddMetricsEndpoints(configure);
        }

        return context;
    }
}
