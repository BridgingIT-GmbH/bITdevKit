// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the system metrics endpoints and their snapshot services.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="configure">An optional endpoint configuration callback.</param>
    /// <returns>The updated service collection.</returns>
    /// <example>
    /// <code>
    /// services.AddMetricsEndpoints(options => options.EnableOverview());
    /// </code>
    /// </example>
    public static IServiceCollection AddMetricsEndpoints(
        this IServiceCollection services,
        Action<MetricsEndpointsOptionsBuilder> configure = null)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        var context = MetricsServiceCollectionExtensions.AddMetrics(services);
        if (!context.Options.Enabled)
        {
            return services;
        }

        var builder = new MetricsEndpointsOptionsBuilder();
        configure?.Invoke(builder);

        services.AddSingleton(builder.Target);
        services.AddEndpoints<MetricsEndpoints>(builder.Target.Enabled);

        return services;
    }
}
