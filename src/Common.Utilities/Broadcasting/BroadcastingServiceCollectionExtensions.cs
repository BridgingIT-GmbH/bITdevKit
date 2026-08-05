// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

/// <summary>Registers the shared Broadcasting runtime.</summary>
/// <example><code>services.AddBroadcasting(options => options.Scopes("MyApp"));</code></example>
public static class BroadcastingServiceCollectionExtensions
{
    /// <summary>
    /// Adds or reopens the one re-entrant Broadcasting builder for this service collection.
    /// </summary>
    /// <example><code>services.AddBroadcasting(options => options.Scopes("MyApp"));</code></example>
    public static BroadcastingBuilderContext AddBroadcasting(
        this IServiceCollection services,
        Action<BroadcastingOptionsBuilder> configure = null
    )
    {
        ArgumentNullException.ThrowIfNull(services);

        var options =
            services
                .FirstOrDefault(x => x.ServiceType == typeof(BroadcastingOptions))
                ?.ImplementationInstance as BroadcastingOptions
            ?? new BroadcastingOptions();
        var state =
            services
                .FirstOrDefault(x => x.ServiceType == typeof(BroadcastingRegistrationState))
                ?.ImplementationInstance as BroadcastingRegistrationState
            ?? new BroadcastingRegistrationState();

        configure?.Invoke(new BroadcastingOptionsBuilder(options));
        options.EnsureDefaultScope();

        services.TryAddSingleton(options);
        services.TryAddSingleton(state);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ISerializer, SystemTextJsonSerializer>();
        services.TryAddSingleton<
            IBroadcastNodeIdentityProvider,
            DefaultBroadcastNodeIdentityProvider
        >();
        services.TryAddSingleton<IBroadcastRegistryStore, InMemoryBroadcastRegistryStore>();
        services.TryAddSingleton<IBroadcastTransport, LocalOnlyBroadcastTransport>();
        services.TryAddSingleton<
            IBroadcastOperationalAuthorizer,
            DenyBroadcastOperationalAuthorizer
        >();
        services.TryAddSingleton<IBroadcastingDiagnostics, BroadcastingDiagnostics>();
        services.TryAddSingleton<RecentBroadcastTracker>();
        services.TryAddSingleton<IBroadcastReceiver, BroadcastReceiver>();
        services.TryAddSingleton<BroadcastLocalDispatcher>();
        services.TryAddSingleton<IBroadcastLocalDispatcher>(serviceProvider =>
            serviceProvider.GetRequiredService<BroadcastLocalDispatcher>()
        );
        services.TryAddSingleton<IBroadcastService, BroadcastService>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, BroadcastLocalDispatchHostedService>()
        );
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, BroadcastNodeLifecycleService>()
        );
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, BroadcastRegistrationLeaseService>()
        );

        return new BroadcastingBuilderContext(services, options, state)
            .AddHandler<BroadcastProbe, BroadcastProbeHandler>();
    }
}