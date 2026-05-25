// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Reflection;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Provides dependency injection helpers for the devkit metrics feature.
/// </summary>
public static class MetricsServiceCollectionExtensions
{
    /// <summary>
    /// Adds the shared devkit metrics services and returns a builder context for optional feature wiring.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="configure">An optional metrics configuration callback.</param>
    /// <returns>The metrics builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddMetrics(options => options
    ///     .Enabled(true)
    ///     .AddEndpoints(true));
    /// </code>
    /// </example>
    public static MetricsBuilderContext AddMetrics(
        this IServiceCollection services,
        Action<MetricsOptionsBuilder> configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.FirstOrDefault(x => x.ServiceType == typeof(MetricsOptions))?.ImplementationInstance as MetricsOptions
            ?? new MetricsOptions();
        var builder = new MetricsOptionsBuilder(options);
        configure?.Invoke(builder);

        services.TryAddSingleton(options);

        if (options.Enabled)
        {
            services.TryAddSingleton<IMetricsService, MetricsService>();
            TryAddWebMetricsServices(services);

            if (options.EndpointsEnabled)
            {
                TryAddWebMetricsEndpoints(services);
            }
        }

        return new MetricsBuilderContext(services, options);
    }

    private static void TryAddWebMetricsServices(IServiceCollection services)
    {
        var metricsSnapshotInterface = ResolvePresentationWebType("BridgingIT.DevKit.Presentation.Web.IMetricsSnapshotService");
        var metricsSnapshotType = ResolvePresentationWebType("BridgingIT.DevKit.Presentation.Web.MetricsSnapshotService");
        var dotNetSnapshotInterface = ResolvePresentationWebType("BridgingIT.DevKit.Presentation.Web.IDotNetMetricsSnapshotService");
        var dotNetSnapshotType = ResolvePresentationWebType("BridgingIT.DevKit.Presentation.Web.DotNetMetricsSnapshotService");
        var aspNetTrackerType = ResolvePresentationWebType("BridgingIT.DevKit.Presentation.Web.AspNetMetricsTracker");
        var aspNetSnapshotInterface = ResolvePresentationWebType("BridgingIT.DevKit.Presentation.Web.IAspNetMetricsSnapshotService");
        var aspNetSnapshotType = ResolvePresentationWebType("BridgingIT.DevKit.Presentation.Web.AspNetMetricsSnapshotService");

        TryAddSingleton(services, metricsSnapshotInterface, metricsSnapshotType);
        TryAddSingleton(services, dotNetSnapshotInterface, dotNetSnapshotType);
        TryAddSingleton(services, aspNetTrackerType, aspNetTrackerType);
        TryAddSingleton(services, aspNetSnapshotInterface, aspNetSnapshotType);
    }

    private static void TryAddWebMetricsEndpoints(IServiceCollection services)
    {
        var endpointsOptionsType = ResolvePresentationWebType("BridgingIT.DevKit.Presentation.Web.MetricsEndpointsOptions");
        var endpointsInterfaceType = ResolvePresentationWebType("BridgingIT.DevKit.Presentation.Web.IEndpoints");
        var endpointsType = ResolvePresentationWebType("BridgingIT.DevKit.Presentation.Web.MetricsEndpoints");

        if (endpointsOptionsType is not null)
        {
            services.TryAddSingleton(endpointsOptionsType, endpointsOptionsType);
        }

        if (endpointsInterfaceType is not null && endpointsType is not null)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(endpointsInterfaceType, endpointsType));
        }
    }

    private static Type ResolvePresentationWebType(string fullName)
    {
        return Type.GetType($"{fullName}, BridgingIT.DevKit.Presentation.Web", throwOnError: false);
    }

    private static void TryAddSingleton(IServiceCollection services, Type serviceType, Type implementationType)
    {
        if (serviceType is null || implementationType is null)
        {
            return;
        }

        services.TryAddSingleton(serviceType, implementationType);
    }
}
