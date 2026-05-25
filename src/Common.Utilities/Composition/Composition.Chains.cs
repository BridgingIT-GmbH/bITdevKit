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

public interface IChainHandler<TContext>
{
    /// <summary>
    /// Handles the current context and optionally forwards to the next handler.
    /// </summary>
    /// <param name="context">The chain context.</param>
    /// <param name="next">The next delegate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The chain result.</returns>
    ValueTask<ChainResult> HandleAsync(
        TContext context,
        ChainExecutionDelegate<TContext> next,
        CancellationToken cancellationToken);
}

/// <summary>
/// Represents the delegate used to invoke the next chain handler.
/// </summary>
/// <typeparam name="TContext">The chain context.</typeparam>
/// <param name="context">The context.</param>
/// <param name="cancellationToken">The cancellation token.</param>
/// <returns>The chain result.</returns>
public delegate ValueTask<ChainResult> ChainExecutionDelegate<TContext>(
    TContext context,
    CancellationToken cancellationToken);

/// <summary>
/// Represents the outcome of chain execution.
/// </summary>
public sealed class ChainResult
{
    /// <summary>
    /// Gets a value indicating whether the chain handled the context.
    /// </summary>
    public bool Handled { get; init; }

    /// <summary>
    /// Gets the result payload for the chain execution.
    /// </summary>
    public Result Result { get; init; } = Result.Success();
}

/// <summary>
/// Represents a chain executor.
/// </summary>
/// <typeparam name="TContext">The chain context.</typeparam>
public interface IChainExecutor<TContext>
{
    /// <summary>
    /// Executes the chain for the specified context.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The chain result.</returns>
    /// <example>
    /// <code>
    /// var executor = provider.GetRequiredService&lt;IChainExecutor&lt;FileImportContext&gt;&gt;();
    /// var result = await executor.ExecuteAsync(new FileImportContext { FileName = "orders.csv", Content = stream });
    /// </code>
    /// </example>
    ValueTask<ChainResult> ExecuteAsync(
        TContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a chain registration builder.
/// </summary>
/// <typeparam name="THandler">The handler contract.</typeparam>
/// <typeparam name="TContext">The chain context.</typeparam>
public interface IChainBuilder<THandler, TContext>
    where THandler : class, IChainHandler<TContext>
{
    /// <summary>
    /// Adds a handler implementation to the chain.
    /// </summary>
    /// <typeparam name="TImplementation">The handler implementation.</typeparam>
    /// <returns>The builder.</returns>
    IChainBuilder<THandler, TContext> With<TImplementation>()
        where TImplementation : class, THandler;

    /// <summary>
    /// Registers the chain executor as a singleton.
    /// </summary>
    /// <returns>The root composition builder.</returns>
    ICompositionBuilder RegisterSingleton();

    /// <summary>
    /// Registers the chain executor as scoped.
    /// </summary>
    /// <returns>The root composition builder.</returns>
    ICompositionBuilder RegisterScoped();

    /// <summary>
    /// Registers the chain executor as transient.
    /// </summary>
    /// <returns>The root composition builder.</returns>
    ICompositionBuilder RegisterTransient();
}

/// <summary>
/// Represents a runtime context-based factory.
/// </summary>
/// <typeparam name="TService">The created service type.</typeparam>
/// <typeparam name="TContext">The runtime context type.</typeparam>

internal sealed class ChainDefinition<THandler, TContext>
    where THandler : class, IChainHandler<TContext>
{
    public IList<Type> Handlers { get; } = new List<Type>();
}

internal sealed class ChainBuilder<THandler, TContext>(
    CompositionBuilder root,
    ChainDefinition<THandler, TContext> definition)
    : IChainBuilder<THandler, TContext>
    where THandler : class, IChainHandler<TContext>
{
    private bool registered;

    public IChainBuilder<THandler, TContext> With<TImplementation>()
        where TImplementation : class, THandler
    {
        CompositionValidation.ValidateCreatable(typeof(TImplementation), "Chain handler");
        definition.Handlers.Add(typeof(TImplementation));
        return this;
    }

    public ICompositionBuilder RegisterSingleton()
    {
        this.Register(ServiceLifetime.Singleton);
        return root;
    }

    public ICompositionBuilder RegisterScoped()
    {
        this.Register(ServiceLifetime.Scoped);
        return root;
    }

    public ICompositionBuilder RegisterTransient()
    {
        this.Register(ServiceLifetime.Transient);
        return root;
    }

    public void EnsureDefaultRegistration()
    {
        if (!this.registered)
        {
            this.Register(ServiceLifetime.Transient);
        }
    }

    private void Register(ServiceLifetime lifetime)
    {
        root.ApplyRegistration(
            typeof(IChainExecutor<TContext>),
            lifetime,
            sp => new ChainExecutor<THandler, TContext>(sp, sp.GetRequiredService<ChainDefinition<THandler, TContext>>()),
            CompositionRegistrationMode.ReplaceExisting);
        this.registered = true;
    }
}

internal sealed class ChainExecutor<THandler, TContext>(
    IServiceProvider services,
    ChainDefinition<THandler, TContext> definition) : IChainExecutor<TContext>
    where THandler : class, IChainHandler<TContext>
{
    public ValueTask<ChainResult> ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
    {
        return ExecuteAtAsync(0, context, cancellationToken);
    }

    private ValueTask<ChainResult> ExecuteAtAsync(int index, TContext context, CancellationToken cancellationToken)
    {
        if (index >= definition.Handlers.Count)
        {
            return ValueTask.FromResult(new ChainResult { Handled = false, Result = Result.Success() });
        }

        var handler = (THandler)CompositionRuntime.ResolveOrCreate(definition.Handlers[index], services);
        return handler.HandleAsync(
            context,
            (nextContext, ct) => ExecuteAtAsync(index + 1, nextContext, ct),
            cancellationToken);
    }
}
