// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Utilities.Composition;

using System.Reflection;
using System.Runtime.ExceptionServices;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Common.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public interface ICompositeBuilder<TService, TComposite>
    where TService : class
    where TComposite : class, TService
{
    /// <summary>
    /// Replaces existing registrations when the composite is registered.
    /// </summary>
    /// <returns>The builder.</returns>
    ICompositeBuilder<TService, TComposite> ReplaceExisting();

    /// <summary>
    /// Only registers the composite when the service contract is not already registered.
    /// </summary>
    /// <returns>The builder.</returns>
    ICompositeBuilder<TService, TComposite> TryRegister();

    /// <summary>
    /// Appends an additional registration for the service contract.
    /// </summary>
    /// <returns>The builder.</returns>
    ICompositeBuilder<TService, TComposite> AddAdditional();

    /// <summary>
    /// Registers the composite as a singleton.
    /// </summary>
    /// <returns>The root composition builder.</returns>
    ICompositionBuilder RegisterSingleton();

    /// <summary>
    /// Registers the composite as scoped.
    /// </summary>
    /// <returns>The root composition builder.</returns>
    ICompositionBuilder RegisterScoped();

    /// <summary>
    /// Registers the composite as transient.
    /// </summary>
    /// <returns>The root composition builder.</returns>
    ICompositionBuilder RegisterTransient();
}

/// <summary>
/// Represents a composite child configuration builder.
/// </summary>
/// <typeparam name="TService">The service contract.</typeparam>
public interface ICompositeChildrenBuilder<TService>
    where TService : class
{
    /// <summary>
    /// Adds a child implementation to the composite.
    /// </summary>
    /// <typeparam name="TImplementation">The child implementation.</typeparam>
    /// <returns>The child builder.</returns>
    ICompositeChildrenBuilder<TService> With<TImplementation>()
        where TImplementation : class, TService;
}

/// <summary>
/// Represents a chain handler.
/// </summary>
/// <typeparam name="TContext">The chain context.</typeparam>

internal sealed class CompositeBuilder<TService, TComposite>(CompositionBuilder root)
    : ICompositeBuilder<TService, TComposite>, ICompositeChildrenBuilder<TService>
    where TService : class
    where TComposite : class, TService
{
    private readonly List<Type> children = [];
    private CompositionRegistrationMode mode = CompositionRegistrationMode.ReplaceExisting;

    public ICompositeChildrenBuilder<TService> With<TImplementation>()
        where TImplementation : class, TService
    {
        CompositionValidation.ValidateCreatable(typeof(TImplementation), "Composite child");
        this.children.Add(typeof(TImplementation));
        return this;
    }

    public ICompositeBuilder<TService, TComposite> ReplaceExisting()
    {
        this.mode = CompositionRegistrationMode.ReplaceExisting;
        return this;
    }

    public ICompositeBuilder<TService, TComposite> TryRegister()
    {
        this.mode = CompositionRegistrationMode.TryRegister;
        return this;
    }

    public ICompositeBuilder<TService, TComposite> AddAdditional()
    {
        this.mode = CompositionRegistrationMode.AddAdditional;
        return this;
    }

    public ICompositionBuilder RegisterSingleton() => this.Register(ServiceLifetime.Singleton);

    public ICompositionBuilder RegisterScoped() => this.Register(ServiceLifetime.Scoped);

    public ICompositionBuilder RegisterTransient() => this.Register(ServiceLifetime.Transient);

    private ICompositionBuilder Register(ServiceLifetime lifetime)
    {
        root.ApplyRegistration(
            new ServiceDescriptor(
                typeof(TService),
                sp =>
                {
                    var childInstances = this.children.Select(type => (TService)CompositionRuntime.ResolveOrCreate(type, sp)).ToList();
                    return CompositionRuntime.CreateWrapper(typeof(TComposite), typeof(TService), childInstances, "Composite", sp);
                },
                lifetime),
            this.mode);

        return root;
    }
}
