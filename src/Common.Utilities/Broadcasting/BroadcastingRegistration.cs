// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Describes one statically typed broadcast payload-to-handler registration.</summary>
/// <param name="PayloadType">The registered CLR payload type.</param>
/// <param name="HandlerType">The registered handler implementation type.</param>
/// <param name="TypeName">The stable transport type name.</param>
/// <param name="InvokeAsync">The statically typed handler invocation delegate.</param>
/// <example><code>var registrations = state.Handlers;</code></example>
public sealed record BroadcastHandlerRegistration(
    Type PayloadType,
    Type HandlerType,
    string TypeName,
    Func<IServiceProvider, object, BroadcastContext, CancellationToken, Task> InvokeAsync
);

/// <summary>Stores the shared, thread-safe registration state for one application host.</summary>
/// <example><code>var state = new BroadcastingRegistrationState();</code></example>
public sealed class BroadcastingRegistrationState
{
    private readonly object sync = new();
    private readonly Dictionary<Type, BroadcastHandlerRegistration> handlers = [];
    private Type registryProviderType;
    private Type transportType;

    /// <summary>Gets an immutable snapshot of registered handler mappings.</summary>
    public IReadOnlyCollection<BroadcastHandlerRegistration> Handlers
    {
        get
        {
            lock (this.sync)
            {
                return this.handlers.Values.ToArray();
            }
        }
    }

    /// <summary>Adds one typed handler mapping or accepts an identical repeated registration.</summary>
    public void AddHandler<TBroadcast, THandler>()
        where THandler : class, IBroadcastHandler<TBroadcast>
    {
        var payloadType = typeof(TBroadcast);
        var handlerType = typeof(THandler);
        var typeName = payloadType.FullName;
        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new InvalidOperationException(
                $"Broadcast type '{payloadType}' has no full type name."
            );
        }

        lock (this.sync)
        {
            if (this.handlers.TryGetValue(payloadType, out var existing))
            {
                if (existing.HandlerType != handlerType)
                {
                    throw new InvalidOperationException(
                        $"Broadcast type '{typeName}' is already registered with handler '{existing.HandlerType.FullName}'."
                    );
                }

                return;
            }

            if (
                this.handlers.Values.Any(x =>
                    string.Equals(x.TypeName, typeName, StringComparison.Ordinal)
                )
            )
            {
                throw new InvalidOperationException(
                    $"Broadcast type-name collision detected for '{typeName}'."
                );
            }

            this.handlers.Add(
                payloadType,
                new(
                    payloadType,
                    handlerType,
                    typeName,
                    static (serviceProvider, payload, context, cancellationToken) =>
                        serviceProvider
                            .GetRequiredService<THandler>()
                            .HandleAsync((TBroadcast)payload, context, cancellationToken)
                )
            );
        }
    }

    /// <summary>Selects one explicit registry provider and rejects conflicting selections.</summary>
    public bool SelectRegistryProvider(Type providerType)
    {
        ArgumentNullException.ThrowIfNull(providerType);

        lock (this.sync)
        {
            if (this.registryProviderType is null)
            {
                this.registryProviderType = providerType;
                return true;
            }

            if (this.registryProviderType != providerType)
            {
                throw new InvalidOperationException(
                    $"Broadcasting already uses registry provider '{this.registryProviderType.FullName}'."
                );
            }

            return false;
        }
    }

    /// <summary>Selects one explicit transport and rejects conflicting selections.</summary>
    public bool SelectTransport(Type transportType)
    {
        ArgumentNullException.ThrowIfNull(transportType);

        lock (this.sync)
        {
            if (this.transportType is null)
            {
                this.transportType = transportType;
                return true;
            }

            if (this.transportType != transportType)
            {
                throw new InvalidOperationException(
                    $"Broadcasting already uses transport '{this.transportType.FullName}'."
                );
            }

            return false;
        }
    }
}

/// <summary>
/// Continues fluent configuration of the one host-wide Broadcasting runtime.
/// </summary>
/// <example><code>services.AddBroadcasting().AddHandler&lt;Refresh, RefreshHandler&gt;();</code></example>
public sealed class BroadcastingBuilderContext
{
    private readonly BroadcastingRegistrationState state;

    /// <summary>Creates a fluent context over one shared registration state.</summary>
    /// <param name="services">The service collection being configured.</param>
    /// <param name="options">The shared mutable options.</param>
    /// <param name="state">The shared registration state.</param>
    public BroadcastingBuilderContext(
        IServiceCollection services,
        BroadcastingOptions options,
        BroadcastingRegistrationState state
    )
    {
        this.Services = services;
        this.Options = options;
        this.state = state;
    }

    /// <summary>Gets the service collection being configured.</summary>
    public IServiceCollection Services { get; }

    /// <summary>Gets the shared mutable options instance.</summary>
    public BroadcastingOptions Options { get; }

    /// <summary>Registers exactly one typed handler for a broadcast type.</summary>
    public BroadcastingBuilderContext AddHandler<TBroadcast, THandler>()
        where THandler : class, IBroadcastHandler<TBroadcast>
    {
        this.state.AddHandler<TBroadcast, THandler>();
        this.Services.TryAddScoped<THandler>();
        return this;
    }

    /// <summary>
    /// Selects an explicit registry provider and returns whether it replaces the implicit fallback.
    /// </summary>
    public bool SelectRegistryProvider(Type providerType) =>
        this.state.SelectRegistryProvider(providerType);

    /// <summary>
    /// Selects an explicit transport and returns whether it replaces the implicit fallback.
    /// </summary>
    public bool SelectTransport(Type transportType) => this.state.SelectTransport(transportType);

    /// <summary>Selects and registers one explicit registry provider, replacing only the implicit fallback.</summary>
    public BroadcastingBuilderContext UseRegistryProvider(Type providerType)
    {
        ArgumentNullException.ThrowIfNull(providerType);
        if (!typeof(IBroadcastRegistryStore).IsAssignableFrom(providerType))
        {
            throw new ArgumentException(
                $"Registry provider '{providerType.FullName}' must implement {nameof(IBroadcastRegistryStore)}.",
                nameof(providerType)
            );
        }

        this.state.SelectRegistryProvider(providerType);
        this.ReplaceImplicitProvider<IBroadcastRegistryStore, InMemoryBroadcastRegistryStore>(
            providerType
        );
        return this;
    }

    /// <summary>Selects and registers one explicit transport, replacing only the implicit fallback.</summary>
    public BroadcastingBuilderContext UseTransport(Type transportType)
    {
        ArgumentNullException.ThrowIfNull(transportType);
        if (!typeof(IBroadcastTransport).IsAssignableFrom(transportType))
        {
            throw new ArgumentException(
                $"Transport '{transportType.FullName}' must implement {nameof(IBroadcastTransport)}.",
                nameof(transportType)
            );
        }

        this.state.SelectTransport(transportType);
        this.ReplaceImplicitProvider<IBroadcastTransport, LocalOnlyBroadcastTransport>(
            transportType
        );
        return this;
    }

    private void ReplaceImplicitProvider<TService, TFallback>(Type providerType)
        where TService : class
        where TFallback : class, TService
    {
        var conflictingDescriptor = this.Services.FirstOrDefault(descriptor =>
            descriptor.ServiceType == typeof(TService)
            && descriptor.ImplementationType is not null
            && descriptor.ImplementationType != typeof(TFallback)
            && descriptor.ImplementationType != providerType
        );
        if (conflictingDescriptor is not null)
        {
            throw new InvalidOperationException(
                $"Broadcasting already has provider '{conflictingDescriptor.ImplementationType.FullName}'."
            );
        }

        foreach (
            var descriptor in this
                .Services.Where(descriptor =>
                    descriptor.ServiceType == typeof(TService)
                    && descriptor.ImplementationType == typeof(TFallback)
                )
                .ToArray()
        )
        {
            this.Services.Remove(descriptor);
        }

        if (
            !this.Services.Any(descriptor =>
                descriptor.ServiceType == typeof(TService)
                && descriptor.ImplementationType == providerType
            )
        )
        {
            this.Services.AddSingleton(typeof(TService), providerType);
        }
    }
}
