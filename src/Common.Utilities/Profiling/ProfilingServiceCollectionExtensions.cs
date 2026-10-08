// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Composes the overarching feature and its explicit Runtime capability.</summary>
/// <example><code>services.AddProfiling(o => o.Enabled()).WithRuntimeProfiling();</code></example>
public static class ProfilingServiceCollectionExtensions
{
    /// <summary>Registers shared services without implicitly enabling a capture capability.</summary>
    /// <example><code>services.AddProfiling(o => o.Enabled(environment.IsDevelopment()));</code></example>
    public static ProfilingBuilderContext AddProfiling(this IServiceCollection services, Action<ProfilingOptionsBuilder> configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var state = services.FirstOrDefault(d => d.ServiceType == typeof(ProfilingRegistrationState))?.ImplementationInstance as ProfilingRegistrationState;
        if (state is null)
        {
            state = new ProfilingRegistrationState();
            services.AddSingleton(state);
            services.TryAddSingleton(state.Options);
            RegisterShared(services, state.Options);
        }

        configure?.Invoke(new ProfilingOptionsBuilder(state.Options));
        ConfigureRuntime(services, state);
        ConfigureOperations(services, state);
        return new ProfilingBuilderContext(services, state.Options);
    }

    /// <summary>Explicitly enables and configures Runtime sampling and Broadcast control.</summary>
    /// <example><code>services.AddProfiling(o => o.Enabled()).WithRuntimeProfiling(o => o.Duration(TimeSpan.FromSeconds(30)));</code></example>
    public static ProfilingBuilderContext WithRuntimeProfiling(this ProfilingBuilderContext context, Action<RuntimeProfilingOptionsBuilder> configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var state = context.Services.GetRegistrationState();
        if (!state.RuntimeConfigured)
        {
            state.RuntimeConfigured = true;
            context.Options.Runtime.Enabled = true;
        }

        configure?.Invoke(new RuntimeProfilingOptionsBuilder(context.Options.Runtime));
        ConfigureRuntime(context.Services, state);
        return context;
    }

    /// <summary>Enables injectable operation capture and the bounded periodic writer.</summary>
    /// <example><code>services.AddProfiling(o => o.Enabled()).WithOperationProfiling(o => o.BatchSize(256));</code></example>
    public static ProfilingBuilderContext WithOperationProfiling(this ProfilingBuilderContext context, Action<OperationProfilingOptionsBuilder> configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var state = context.Services.GetRegistrationState();
        if (!state.OperationsConfigured)
        {
            state.OperationsConfigured = true;
            context.Options.Operations.Enabled = true;
        }

        configure?.Invoke(new OperationProfilingOptionsBuilder(context.Options.Operations));
        ConfigureOperations(context.Services, state);
        return context;
    }

    /// <summary>Selects the process-local provider for both profiling datasets.</summary>
    /// <example><code>services.AddProfiling().WithInMemoryProvider();</code></example>
    public static ProfilingBuilderContext WithInMemoryProvider(this ProfilingBuilderContext context) =>
        context.WithProvider<InMemoryProfilingStorageProvider>(provider => new(provider.GetRequiredService<ProfilingOptions>(), provider.GetService<TimeProvider>()));

    /// <summary>Selects one singleton provider; conflicting explicit selections fail setup.</summary>
    /// <example><code>services.AddProfiling().WithProvider&lt;CustomProfilingProvider&gt;();</code></example>
    public static ProfilingBuilderContext WithProvider<TProvider>(this ProfilingBuilderContext context, Func<IServiceProvider, TProvider> factory = null)
        where TProvider : class, IProfilingStorageProvider
    {
        ArgumentNullException.ThrowIfNull(context);
        var state = context.Services.GetRegistrationState();
        if (state.ExplicitProvider is not null)
        {
            if (state.ExplicitProvider != typeof(TProvider))
            {
                throw new InvalidOperationException("Only one explicit Profiling storage provider can be selected.");
            }

            return context;
        }

        if (context.Services.Any(descriptor => descriptor.ServiceType == typeof(IRuntimeProfilingStore)
            && (descriptor.ImplementationInstance is not null || descriptor.ImplementationType is not null)))
        {
            throw new InvalidOperationException("A different profiling store provider is already registered. Select the shared provider instead of a separate facet.");
        }

        state.ExplicitProvider = typeof(TProvider);
        context.Services.RemoveAll<IProfilingStorageProvider>();
        if (factory is null)
        {
            context.Services.TryAddSingleton<TProvider>();
        }
        else
        {
            context.Services.TryAddSingleton(factory);
        }

        context.Services.AddSingleton<IProfilingStorageProvider>(provider => provider.GetRequiredService<TProvider>());
        return context;
    }

    private static void ConfigureOperations(IServiceCollection services, ProfilingRegistrationState state)
    {
        foreach (var descriptor in state.OperationDescriptors)
        {
            services.Remove(descriptor);
        }

        state.OperationDescriptors.Clear();
        var before = services.ToHashSet();
        if (state.Options.Enabled)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ProfilingMaintenanceService>());
        }

        if (state.Options.OperationEnabled)
        {
            services.TryAddSingleton<OperationProfilingWriterService>();
            services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<OperationProfilingWriterService>());
            // Reverse host stop ordering closes live capture before the writer's bounded drain.
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OperationProfilingCleanupService>());
        }

        state.OperationDescriptors.AddRange(services.Where(descriptor => !before.Contains(descriptor)));
    }

    private static void ConfigureRuntime(IServiceCollection services, ProfilingRegistrationState state)
    {
        if (!state.Options.RuntimeEnabled)
        {
            foreach (var descriptor in state.RuntimeDescriptors)
            {
                services.Remove(descriptor);
            }

            state.RuntimeDescriptors.Clear();
            var broadcasting = services.FirstOrDefault(d => d.ServiceType == typeof(BroadcastingRegistrationState))?.ImplementationInstance as BroadcastingRegistrationState;
            foreach (var handler in state.RuntimeHandlers)
            {
                broadcasting?.RemoveHandler(handler.PayloadType, handler.HandlerType);
            }

            state.RuntimeHandlers.Clear();
            return;
        }

        if (state.RuntimeDescriptors.Count > 0)
        {
            return;
        }

        var before = services.ToHashSet();
        var existingBroadcasting = services.FirstOrDefault(d => d.ServiceType == typeof(BroadcastingRegistrationState))?.ImplementationInstance as BroadcastingRegistrationState;
        var existingHandlers = existingBroadcasting?.Handlers.Select(h => h.PayloadType).ToHashSet() ?? [];
        RegisterRuntime(services, state.Options);
        state.RuntimeDescriptors.AddRange(services.Where(d => !before.Contains(d)));
        var broadcastingState = services.First(d => d.ServiceType == typeof(BroadcastingRegistrationState)).ImplementationInstance as BroadcastingRegistrationState;
        state.RuntimeHandlers.AddRange(broadcastingState.Handlers.Where(h => !existingHandlers.Contains(h.PayloadType)));
    }

    private static void RegisterRuntime(IServiceCollection services, ProfilingOptions options)
    {

        services
            .AddBroadcasting()
            .AddHandler<RuntimeProfilingStartBroadcast, RuntimeProfilingStartBroadcastHandler>()
            .AddHandler<RuntimeProfilingStopBroadcast, RuntimeProfilingStopBroadcastHandler>()
            .AddHandler<RuntimeProfilingSnapshotBroadcast, RuntimeProfilingSnapshotBroadcastHandler>()
            .AddHandler<
                RuntimeProfilingGarbageCollectionBroadcast,
                RuntimeProfilingGarbageCollectionBroadcastHandler
            >();
        services.TryAddSingleton<RuntimeProfilingBroadcastExecutionTracker>();
        services.TryAddSingleton<IRuntimeProfilingBroadcastService>(
            provider => new RuntimeProfilingBroadcastService(
                provider.GetRequiredService<BroadcastingOptions>(),
                provider.GetRequiredService<IBroadcastNodeIdentityProvider>(),
                provider.GetRequiredService<IBroadcastRegistryStore>(),
                provider.GetRequiredService<IBroadcastReceiver>(),
                provider.GetRequiredService<IBroadcastTransport>(),
                provider.GetRequiredService<ISerializer>(),
                provider.GetService<TimeProvider>() ?? TimeProvider.System,
                provider.GetService<IMetricsService>(),
                provider.GetService<ILogger<BroadcastService>>(),
                provider.GetService<ILogger<RuntimeProfilingBroadcastService>>()
            )
        );
        services.TryAddSingleton<RuntimeProfilingActiveSessionContext>();
        services.TryAddSingleton<RuntimeProfilingSegmentContext>();
        services.TryAddSingleton<IRuntimeProfilingNodeRegistrationAdapter, RuntimeProfilingNodeRegistrationAdapter>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, RuntimeProfilingNodeRegistrationHostedService>());
        services.TryAddSingleton<
            IRuntimeProfilingContextFactory,
            RuntimeProfilingContextFactory
        >();
        services.TryAddSingleton<IRuntimeProfilingSnapshotProbe>(
            provider => new RuntimeProfilingSnapshotProbe(
                provider.GetService<TimeProvider>() ?? TimeProvider.System
            )
        );
        services.TryAddSingleton(
            provider => new RuntimeProfilingSessionFinalizer(
                provider.GetRequiredService<IRuntimeProfilingStore>(),
                options,
                provider.GetService<TimeProvider>() ?? TimeProvider.System
            )
        );
        services.TryAddSingleton(
            provider => new RuntimeProfilingStartupReconciler(
                provider.GetRequiredService<IRuntimeProfilingStore>(),
                options,
                provider.GetService<TimeProvider>() ?? TimeProvider.System,
                provider.GetRequiredService<RuntimeProfilingSessionFinalizer>()
            )
        );
        services.TryAddSingleton(provider => new RuntimeProfilingCollector(
            provider.GetRequiredService<IRuntimeProfilingStore>(),
            provider.GetRequiredService<IRuntimeProfilingSnapshotProbe>(),
            provider.GetRequiredService<IRuntimeProfilingContextFactory>(),
            provider.GetRequiredService<IRuntimeProfilingNodeRegistrationAdapter>(),
            provider.GetRequiredService<RuntimeProfilingSessionFinalizer>(),
            options,
            provider.GetService<TimeProvider>() ?? TimeProvider.System,
            provider.GetService<IBroadcastRegistryStore>(),
            provider.GetService<IBroadcastNodeIdentityProvider>(),
            provider.GetRequiredService<RuntimeProfilingActiveSessionContext>()
        ));
        services.TryAddSingleton<IRuntimeProfilingCollector>(provider =>
            provider.GetRequiredService<RuntimeProfilingCollector>()
        );
        services.TryAddSingleton(
            provider => new RuntimeProfilingCustomMetricListener(
                provider.GetRequiredService<IRuntimeProfilingStore>(),
                provider.GetRequiredService<RuntimeProfilingActiveSessionContext>(),
                provider.GetRequiredService<RuntimeProfilingSegmentContext>(),
                options,
                provider.GetService<TimeProvider>() ?? TimeProvider.System
            )
        );
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, RuntimeProfilingCollectorHostedService>()
        );
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, RuntimeProfilingCustomMetricHostedService>()
        );
        services.TryAddSingleton<IRuntimeProfilingStressService>(provider => new RuntimeProfilingStressService(
            provider.GetService<ILogger<RuntimeProfilingStressService>>(),
            provider.GetService<IRuntimeProfilingMeasurementService>()
        ));
        }

    private static void RegisterShared(IServiceCollection services, ProfilingOptions options)
    {
        services.TryAddSingleton<IProfilingStorageProvider>(provider => new InMemoryProfilingStorageProvider(options, provider.GetService<TimeProvider>()));
        services.TryAddSingleton<IRuntimeProfilingStore>(provider => provider.GetRequiredService<IProfilingStorageProvider>().Runtime);
        services.TryAddSingleton<IOperationProfilingStore>(provider => provider.GetRequiredService<IProfilingStorageProvider>().Operations);
        services.TryAddSingleton<OperationProfilingCompletionQueue>();
        services.TryAddSingleton<IOperationProfilingCompletionSink>(provider => provider.GetRequiredService<OperationProfilingCompletionQueue>());
        services.TryAddSingleton<OperationProfiler>(provider => new(options, provider.GetRequiredService<IProfilingNodeIdentityProvider>(),
            provider.GetRequiredService<IOperationProfilingCompletionSink>(), provider.GetService<TimeProvider>(), provider.GetService<IProfilingSafeErrorPolicy>(),
            () => provider.GetService<RuntimeProfilingActiveSessionContext>()?.Current?.Session,
            provider.GetService<ILogger<OperationProfiler>>(), provider.GetService<ILogger<ProfilingSegmentScope>>()));
        services.TryAddSingleton<IOperationProfiler>(provider => provider.GetRequiredService<OperationProfiler>());
        services.TryAddSingleton<OperationProfilingHealthState>();
        services.TryAddSingleton<IOperationProfilingHealthSource>(provider => provider.GetRequiredService<OperationProfilingHealthState>());
        services.TryAddSingleton<IProfilingNodeIdentityProvider>(provider => new ProfilingNodeIdentityProvider(
            provider.GetService<TimeProvider>() ?? TimeProvider.System, options.NodeDisplayName, options.ApplicationVersion));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ProfilingConfigurationValidationService>());

        services.TryAddSingleton<IRuntimeProfilingControlService>(provider => new RuntimeProfilingControlService(
            options,
            provider.GetService<TimeProvider>() ?? TimeProvider.System,
            provider.GetService<IRuntimeProfilingStore>(),
            provider.GetService<IRuntimeProfilingBroadcastService>(),
            provider.GetService<IRuntimeProfilingNodeRegistrationAdapter>(),
            provider.GetService<BroadcastingOptions>()
        ));
        services.TryAddSingleton<IRuntimeProfilingMeasurementService>(
            provider => new RuntimeProfilingMeasurementService(
                options,
                provider.GetService<IRuntimeProfilingControlService>(),
                provider.GetService<IRuntimeProfilingStore>(),
                provider.GetService<IRuntimeProfilingNodeRegistrationAdapter>(),
                provider.GetService<IBroadcastRegistryStore>(),
                provider.GetService<IBroadcastNodeIdentityProvider>(),
                provider.GetService<RuntimeProfilingActiveSessionContext>(),
                provider.GetService<RuntimeProfilingSegmentContext>(),
                provider.GetService<TimeProvider>() ?? TimeProvider.System
            )
        );
        services.TryAddSingleton<IRuntimeProfilingEvaluationService>(provider => new RuntimeProfilingEvaluator(
            options,
            provider.GetService<IRuntimeProfilingStore>()
        ));
        services.TryAddSingleton<IRuntimeProfilingArchiveService>(provider => new RuntimeProfilingArchiveService(
            options,
            provider.GetService<IRuntimeProfilingStore>(),
            provider.GetService<TimeProvider>() ?? TimeProvider.System
        ));
        services.TryAddSingleton<IRuntimeProfilingPerfettoExportService>(
            provider => new RuntimeProfilingPerfettoExportService(
                options,
                provider.GetService<IRuntimeProfilingStore>()
            )
        );
        services.TryAddSingleton<IRuntimeProfilingQueryService>(provider => new RuntimeProfilingQueryService(
            options,
            provider.GetService<IRuntimeProfilingStore>(),
            provider.GetService<IRuntimeProfilingControlService>(),
            provider.GetService<IRuntimeProfilingEvaluationService>()
        ));

    }
}

internal sealed class ProfilingRegistrationState
{
    public ProfilingOptions Options { get; } = new();
    public bool RuntimeConfigured { get; set; }
    public bool OperationsConfigured { get; set; }
    public Type ExplicitProvider { get; set; }
    public List<ServiceDescriptor> OperationDescriptors { get; } = [];
    public List<ServiceDescriptor> RuntimeDescriptors { get; } = [];
    public List<BroadcastHandlerRegistration> RuntimeHandlers { get; } = [];
}

internal static class ProfilingRegistrationStateExtensions
{
    public static ProfilingRegistrationState GetRegistrationState(this IServiceCollection services) =>
        services.First(d => d.ServiceType == typeof(ProfilingRegistrationState)).ImplementationInstance as ProfilingRegistrationState;
}

internal sealed class ProfilingConfigurationValidationService(ProfilingOptions options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        options.Validate();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
