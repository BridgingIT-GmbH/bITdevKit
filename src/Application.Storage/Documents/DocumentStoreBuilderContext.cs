﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Configuration;
using Scrutor;

public class DocumentStoreBuilderContext<T>(
    IServiceCollection services,
    ServiceLifetime lifetime = ServiceLifetime.Scoped,
    IConfiguration configuration = null)
    where T : class, new()
{
    private readonly List<Action<IServiceCollection>> behaviors = [];
    private ServiceDescriptor clientDescriptor;

    public IServiceCollection Services { get; } = services;

    public ServiceLifetime Lifetime { get; } = lifetime;

    public IConfiguration Configuration { get; } = configuration;

    public DocumentStoreBuilderContext<T> WithBehavior<TBehavior>()
        where TBehavior : class, IDocumentStoreClient<T>
    {
        this.behaviors.Add(s => s.Decorate<IDocumentStoreClient<T>, TBehavior>());
        this.RegisterBehaviors();

        return this;
    }

    public DocumentStoreBuilderContext<T> WithBehavior<TBehavior>(Func<IDocumentStoreClient<T>, TBehavior> behavior)
        where TBehavior : notnull, IDocumentStoreClient<T>
    {
        EnsureArg.IsNotNull(behavior, nameof(behavior));

        this.behaviors.Add(s => s.Decorate<IDocumentStoreClient<T>>((service, _) => behavior(service)));
        this.RegisterBehaviors();

        return this;
    }

    public DocumentStoreBuilderContext<T> WithBehavior<TBehavior>(
        Func<IDocumentStoreClient<T>, IServiceProvider, TBehavior> behavior)
        where TBehavior : notnull, IDocumentStoreClient<T>
    {
        EnsureArg.IsNotNull(behavior, nameof(behavior));

        this.behaviors.Add(s => s.Decorate<IDocumentStoreClient<T>>((inner, sp) => behavior(inner, sp)));
        this.RegisterBehaviors();

        return this;
    }

    /// <summary>
    ///     Registers all recorded behaviors (decorators). Before registering all existing behavior registrations are removed.
    ///     This needs to be done to apply the registrations in reverse order.
    /// </summary>
    private IServiceCollection RegisterBehaviors()
    {
        // reset the repo registration to the original implementation, as scrutor changes the implementation
        this.clientDescriptor ??= this.Services.Find<IDocumentStoreClient<T>>();
        if (this.clientDescriptor is null)
        {
            throw new Exception(
                $"Cannot register behaviors for {typeof(IDocumentStoreClient<T>).PrettyName()} as it has not been registerd.");
        }

        var descriptorIndex = this.Services.IndexOf<IDocumentStoreClient<T>>();
        if (descriptorIndex != -1)
        {
            this.Services[descriptorIndex] = this.clientDescriptor;
        }
        else
        {
            return this.Services;
        }

        foreach (var descriptor in this.Services.Where(s =>
                         s.ServiceType is DecoratedType &&
                         s.ServiceType.ImplementsInterface(typeof(IDocumentStoreClient<T>)))
                     ?.ToList())
        {
            this.Services.Remove(descriptor); // remove the registered behavior
        }

        // register all behaviors in reverse order (first...last)
        foreach (var behavior in this.behaviors.AsEnumerable().Reverse())
        {
            behavior.Invoke(this.Services);
        }

        return this.Services;
    }
}