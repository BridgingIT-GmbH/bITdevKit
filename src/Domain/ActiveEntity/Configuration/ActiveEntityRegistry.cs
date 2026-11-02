// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Records the entity types that were configured for ActiveEntity so that tooling such as dashboards can discover them.
/// </summary>
/// <remarks>
/// The registry is filled automatically when an ActiveEntity configurator is created, no extra developer code is needed.
/// </remarks>
/// <example>
/// <code>
/// var registry = serviceProvider.GetService&lt;ActiveEntityRegistry&gt;();
/// foreach (var descriptor in registry?.Descriptors ?? [])
/// {
///     Console.WriteLine(descriptor.EntityType.Name);
/// }
/// </code>
/// </example>
public sealed class ActiveEntityRegistry
{
    private readonly object gate = new();
    private readonly Dictionary<Type, ActiveEntityDescriptor> descriptors = [];

    /// <summary>
    /// Gets a snapshot of the registered descriptors.
    /// </summary>
    public IReadOnlyCollection<ActiveEntityDescriptor> Descriptors
    {
        get
        {
            lock (this.gate)
            {
                return this.descriptors.Values.ToList();
            }
        }
    }

    /// <summary>
    /// Returns the registry registered in the service collection or adds a new one.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The shared registry instance.</returns>
    /// <example>
    /// <code>
    /// var registry = ActiveEntityRegistry.GetOrAdd(services);
    /// </code>
    /// </example>
    public static ActiveEntityRegistry GetOrAdd(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var existing = services.LastOrDefault(d => d.ServiceType == typeof(ActiveEntityRegistry));
        if (existing?.ImplementationInstance is ActiveEntityRegistry registry)
        {
            return registry;
        }

        registry = new ActiveEntityRegistry();
        services.AddSingleton(registry);
        return registry;
    }

    /// <summary>
    /// Determines whether the entity type is registered.
    /// </summary>
    /// <param name="entityType">The entity type.</param>
    /// <returns><c>true</c> when the type is registered.</returns>
    /// <example>
    /// <code>
    /// var known = registry.Contains(typeof(Customer));
    /// </code>
    /// </example>
    public bool Contains(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        lock (this.gate)
        {
            return this.descriptors.ContainsKey(entityType);
        }
    }

    /// <summary>
    /// Registers a descriptor. Registering the same entity type again replaces the earlier descriptor.
    /// </summary>
    /// <param name="descriptor">The descriptor to register.</param>
    /// <example>
    /// <code>
    /// registry.Register(new ActiveEntityDescriptor(typeof(Customer), typeof(Guid), false));
    /// </code>
    /// </example>
    public void Register(ActiveEntityDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        lock (this.gate)
        {
            this.descriptors[descriptor.EntityType] = descriptor;
        }
    }
}
