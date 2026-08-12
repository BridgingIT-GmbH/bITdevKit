// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Registers the opt-in profiling feature configuration.</summary>
/// <example><code>services.AddProfiling(options => options.Enabled());</code></example>
public static class ProfilingServiceCollectionExtensions
{
    /// <summary>
    /// Adds or reopens profiling configuration without starting runtime work.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="configure">An optional configuration callback.</param>
    /// <returns>The profiling builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddProfiling(options => options
    ///     .Enabled(environment.IsDevelopment())
    ///     .Duration(TimeSpan.FromSeconds(30)));
    /// </code>
    /// </example>
    public static ProfilingBuilderContext AddProfiling(
        this IServiceCollection services,
        Action<ProfilingOptionsBuilder> configure = null
    )
    {
        ArgumentNullException.ThrowIfNull(services);

        var options =
            services
                .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(ProfilingOptions))
                ?.ImplementationInstance as ProfilingOptions
            ?? new ProfilingOptions();

        configure?.Invoke(new ProfilingOptionsBuilder(options));
        services.TryAddSingleton(options);
        if (options.Enabled)
        {
            services
                .AddBroadcasting()
                .AddHandler<ProfilingStartBroadcast, ProfilingStartBroadcastHandler>()
                .AddHandler<ProfilingStopBroadcast, ProfilingStopBroadcastHandler>()
                .AddHandler<ProfilingSnapshotBroadcast, ProfilingSnapshotBroadcastHandler>()
                .AddHandler<
                    ProfilingGarbageCollectionBroadcast,
                    ProfilingGarbageCollectionBroadcastHandler
                >();
            services.TryAddSingleton<ProfilingBroadcastExecutionTracker>();
            services.TryAddSingleton<IProfilingBroadcastService>(
                provider => new ProfilingBroadcastService(
                    provider.GetRequiredService<BroadcastingOptions>(),
                    provider.GetRequiredService<IBroadcastNodeIdentityProvider>(),
                    provider.GetRequiredService<IBroadcastRegistryStore>(),
                    provider.GetRequiredService<IBroadcastReceiver>(),
                    provider.GetRequiredService<IBroadcastTransport>(),
                    provider.GetRequiredService<ISerializer>(),
                    provider.GetService<TimeProvider>() ?? TimeProvider.System,
                    provider.GetService<IMetricsService>(),
                    provider.GetService<ILogger<BroadcastService>>(),
                    provider.GetService<ILogger<ProfilingBroadcastService>>()
                )
            );
            services.TryAddSingleton<IProfilingStore, InMemoryProfilingStore>();
            services.TryAddSingleton<ProfilingActiveSessionContext>();
            services.TryAddSingleton<ProfilingSegmentContext>();
            services.TryAddSingleton<
                IProfilingNodeIdentityProvider,
                ProfilingNodeIdentityProvider
            >();
            services.TryAddSingleton<
                IProfilingRuntimeContextFactory,
                ProfilingRuntimeContextFactory
            >();
            services.TryAddSingleton<IProfilingSnapshotProbe>(
                provider => new ProfilingSnapshotProbe(
                    provider.GetService<TimeProvider>() ?? TimeProvider.System
                )
            );
            services.TryAddSingleton<ProfilingSessionFinalizer>(
                provider => new ProfilingSessionFinalizer(
                    provider.GetRequiredService<IProfilingStore>(),
                    options,
                    provider.GetService<TimeProvider>() ?? TimeProvider.System
                )
            );
            services.TryAddSingleton<ProfilingStartupReconciler>(
                provider => new ProfilingStartupReconciler(
                    provider.GetRequiredService<IProfilingStore>(),
                    options,
                    provider.GetService<TimeProvider>() ?? TimeProvider.System,
                    provider.GetRequiredService<ProfilingSessionFinalizer>()
                )
            );
            services.TryAddSingleton<ProfilingCollector>(provider => new ProfilingCollector(
                provider.GetRequiredService<IProfilingStore>(),
                provider.GetRequiredService<IProfilingSnapshotProbe>(),
                provider.GetRequiredService<IProfilingRuntimeContextFactory>(),
                provider.GetRequiredService<IProfilingNodeIdentityProvider>(),
                provider.GetRequiredService<ProfilingSessionFinalizer>(),
                options,
                provider.GetService<TimeProvider>() ?? TimeProvider.System,
                provider.GetService<IBroadcastRegistryStore>(),
                provider.GetService<IBroadcastNodeIdentityProvider>(),
                provider.GetRequiredService<ProfilingActiveSessionContext>()
            ));
            services.TryAddSingleton<IProfilingCollector>(provider =>
                provider.GetRequiredService<ProfilingCollector>()
            );
            services.TryAddSingleton<ProfilingCustomMetricListener>(
                provider => new ProfilingCustomMetricListener(
                    provider.GetRequiredService<IProfilingStore>(),
                    provider.GetRequiredService<ProfilingActiveSessionContext>(),
                    provider.GetRequiredService<ProfilingSegmentContext>(),
                    options,
                    provider.GetService<TimeProvider>() ?? TimeProvider.System
                )
            );
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHostedService, ProfilingCollectorHostedService>()
            );
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHostedService, ProfilingCustomMetricHostedService>()
            );
            services.TryAddSingleton<IProfilingStressService>(provider => new ProfilingStressService(
                provider.GetService<ILogger<ProfilingStressService>>(),
                provider.GetService<IProfilingMeasurementService>()
            ));
        }

        services.TryAddSingleton<IProfilingControlService>(provider => new ProfilingControlService(
            options,
            provider.GetService<TimeProvider>() ?? TimeProvider.System,
            provider.GetService<IProfilingStore>(),
            provider.GetService<IProfilingBroadcastService>(),
            provider.GetService<IProfilingNodeIdentityProvider>(),
            provider.GetService<BroadcastingOptions>()
        ));
        services.TryAddSingleton<IProfilingMeasurementService>(
            provider => new ProfilingMeasurementService(
                options,
                provider.GetService<IProfilingControlService>(),
                provider.GetService<IProfilingStore>(),
                provider.GetService<IProfilingNodeIdentityProvider>(),
                provider.GetService<IBroadcastRegistryStore>(),
                provider.GetService<IBroadcastNodeIdentityProvider>(),
                provider.GetService<ProfilingActiveSessionContext>(),
                provider.GetService<ProfilingSegmentContext>(),
                provider.GetService<TimeProvider>() ?? TimeProvider.System
            )
        );
        services.TryAddSingleton<IProfilingEvaluationService>(provider => new ProfilingEvaluator(
            options,
            provider.GetService<IProfilingStore>()
        ));
        services.TryAddSingleton<IProfilingArchiveService>(provider => new ProfilingArchiveService(
            options,
            provider.GetService<IProfilingStore>(),
            provider.GetService<TimeProvider>() ?? TimeProvider.System
        ));
        services.TryAddSingleton<IProfilingPerfettoExportService>(
            provider => new ProfilingPerfettoExportService(
                options,
                provider.GetService<IProfilingStore>()
            )
        );
        services.TryAddSingleton<IProfilingQueryService>(provider => new ProfilingQueryService(
            options,
            provider.GetService<IProfilingStore>(),
            provider.GetService<IProfilingControlService>(),
            provider.GetService<IProfilingEvaluationService>()
        ));

        return new ProfilingBuilderContext(services, options);
    }
}