// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license


using System.Reflection;
using System.Runtime.ExceptionServices;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Common.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BridgingIT.DevKit.Common.Utilities.Composition;
public interface IAdapterSourceBuilder<TSource>
    where TSource : class
{
    /// <summary>
    /// Targets a service contract for the adapter.
    /// </summary>
    /// <typeparam name="TTarget">The target service contract.</typeparam>
    /// <returns>An adapter target builder.</returns>
    IAdapterTargetBuilder<TSource, TTarget> To<TTarget>()
        where TTarget : class;
}

/// <summary>
/// Represents the second stage of adapter registration.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TTarget">The target contract.</typeparam>
public interface IAdapterTargetBuilder<TSource, TTarget>
    where TSource : class
    where TTarget : class
{
    /// <summary>
    /// Uses the specified adapter implementation.
    /// </summary>
    /// <typeparam name="TAdapter">The adapter type.</typeparam>
    /// <returns>An adapter registration builder.</returns>
    IAdapterRegistrationBuilder<TTarget> Using<TAdapter>()
        where TAdapter : class, TTarget;
}

/// <summary>
/// Represents the final adapter registration stage.
/// </summary>
/// <typeparam name="TTarget">The target contract.</typeparam>
public interface IAdapterRegistrationBuilder<TTarget>
    where TTarget : class
{
    /// <summary>
    /// Replaces existing registrations when the adapter is registered.
    /// </summary>
    /// <returns>The builder.</returns>
    IAdapterRegistrationBuilder<TTarget> ReplaceExisting();

    /// <summary>
    /// Only registers the adapter when the target contract is not already registered.
    /// </summary>
    /// <returns>The builder.</returns>
    IAdapterRegistrationBuilder<TTarget> TryRegister();

    /// <summary>
    /// Appends an additional registration for the target contract.
    /// </summary>
    /// <returns>The builder.</returns>
    IAdapterRegistrationBuilder<TTarget> AddAdditional();

    /// <summary>
    /// Registers the adapter as a singleton.
    /// </summary>
    /// <returns>The root composition builder.</returns>
    ICompositionBuilder RegisterSingleton();

    /// <summary>
    /// Registers the adapter as scoped.
    /// </summary>
    /// <returns>The root composition builder.</returns>
    ICompositionBuilder RegisterScoped();

    /// <summary>
    /// Registers the adapter as transient.
    /// </summary>
    /// <returns>The root composition builder.</returns>
    ICompositionBuilder RegisterTransient();
}

/// <summary>
/// Adapts source instances to target contracts.
/// </summary>
public interface IAdapterFactory
{
    /// <summary>
    /// Adapts a source instance to a target contract.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="source">The source instance.</param>
    /// <returns>The adapted target instance.</returns>
    /// <example>
    /// <code>
    /// var adapterFactory = provider.GetRequiredService&lt;IAdapterFactory&gt;();
    /// var sender = adapterFactory.Adapt&lt;ThirdPartyEmailClient, INotificationSender&gt;(client);
    /// </code>
    /// </example>
    TTarget Adapt<TSource, TTarget>(TSource source)
        where TSource : class
        where TTarget : class;
}

/// <summary>
/// Represents a keyed strategy resolver.
/// </summary>
/// <typeparam name="TStrategy">The strategy contract.</typeparam>

internal sealed class AdapterSourceBuilder<TSource>(CompositionBuilder root, CompositionRegistrationState state) : IAdapterSourceBuilder<TSource>
    where TSource : class
{
    public IAdapterTargetBuilder<TSource, TTarget> To<TTarget>()
        where TTarget : class
    {
        return new AdapterTargetBuilder<TSource, TTarget>(root, state);
    }
}

internal sealed class AdapterTargetBuilder<TSource, TTarget>(CompositionBuilder root, CompositionRegistrationState state)
    : IAdapterTargetBuilder<TSource, TTarget>
    where TSource : class
    where TTarget : class
{
    public IAdapterRegistrationBuilder<TTarget> Using<TAdapter>()
        where TAdapter : class, TTarget
    {
        CompositionValidation.ValidateAssignable(typeof(TAdapter), typeof(TTarget), "Adapter");
        state.AdapterMappings[(typeof(TSource), typeof(TTarget))] = typeof(TAdapter);
        return new AdapterRegistrationBuilder<TSource, TTarget, TAdapter>(root);
    }
}

internal sealed class AdapterRegistrationBuilder<TSource, TTarget, TAdapter>(CompositionBuilder root)
    : IAdapterRegistrationBuilder<TTarget>
    where TSource : class
    where TTarget : class
    where TAdapter : class, TTarget
{
    private CompositionRegistrationMode mode = CompositionRegistrationMode.ReplaceExisting;

    public IAdapterRegistrationBuilder<TTarget> ReplaceExisting()
    {
        this.mode = CompositionRegistrationMode.ReplaceExisting;
        return this;
    }

    public IAdapterRegistrationBuilder<TTarget> TryRegister()
    {
        this.mode = CompositionRegistrationMode.TryRegister;
        return this;
    }

    public IAdapterRegistrationBuilder<TTarget> AddAdditional()
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
                typeof(TTarget),
                sp =>
                {
                    var source = CompositionRuntime.ResolveOrCreate(typeof(TSource), sp);
                    return CompositionRuntime.CreateWrapper(typeof(TAdapter), typeof(TTarget), source, "Adapter", sp);
                },
                lifetime),
            this.mode);

        return root;
    }
}

internal sealed class AdapterFactory(CompositionRegistrationState state, IServiceProvider services) : IAdapterFactory
{
    public TTarget Adapt<TSource, TTarget>(TSource source)
        where TSource : class
        where TTarget : class
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!state.AdapterMappings.TryGetValue((typeof(TSource), typeof(TTarget)), out var adapterType))
        {
            throw new InvalidOperationException(
                $"No adapter registration exists for source {typeof(TSource).Name} and target {typeof(TTarget).Name}.");
        }

        return (TTarget)CompositionRuntime.CreateWrapper(adapterType, typeof(TTarget), source, "Adapter", services);
    }
}
