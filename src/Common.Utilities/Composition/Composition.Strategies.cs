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

public interface IStrategyResolver<TStrategy>
    where TStrategy : class
{
    /// <summary>
    /// Resolves a strategy by key.
    /// </summary>
    /// <param name="key">The strategy key.</param>
    /// <returns>The resolved strategy.</returns>
    TStrategy Resolve(string key);

    /// <summary>
    /// Tries to resolve a strategy by key.
    /// </summary>
    /// <param name="key">The strategy key.</param>
    /// <param name="strategy">The resolved strategy when available.</param>
    /// <returns><c>true</c> when the strategy exists; otherwise <c>false</c>.</returns>
    bool TryResolve(string key, out TStrategy strategy);

    /// <summary>
    /// Resolves the configured default strategy.
    /// </summary>
    /// <returns>The default strategy.</returns>
    TStrategy ResolveDefault();

    /// <summary>
    /// Gets the registered strategy keys.
    /// </summary>
    IReadOnlyCollection<string> Keys { get; }
}

/// <summary>
/// Represents a keyed strategy builder.
/// </summary>
/// <typeparam name="TStrategy">The strategy contract.</typeparam>
public interface IStrategyBuilder<TStrategy>
    where TStrategy : class
{
    /// <summary>
    /// Adds a strategy implementation for the specified key.
    /// </summary>
    /// <typeparam name="TImplementation">The strategy implementation.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The builder.</returns>
    IStrategyBuilder<TStrategy> Add<TImplementation>(string key)
        where TImplementation : class, TStrategy;

    /// <summary>
    /// Configures the default strategy key.
    /// </summary>
    /// <param name="key">The default key.</param>
    /// <returns>The builder.</returns>
    IStrategyBuilder<TStrategy> WithDefault(string key);
}

/// <summary>
/// Represents a composite registration builder.
/// </summary>
/// <typeparam name="TService">The service contract.</typeparam>
/// <typeparam name="TComposite">The composite implementation.</typeparam>

internal sealed class StrategyDefinition<TStrategy>
    where TStrategy : class
{
    public IDictionary<string, Type> Mappings { get; } =
        new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

    public string DefaultKey { get; set; }
}

internal sealed class StrategyBuilder<TStrategy>(StrategyDefinition<TStrategy> definition) : IStrategyBuilder<TStrategy>
    where TStrategy : class
{
    public IStrategyBuilder<TStrategy> Add<TImplementation>(string key)
        where TImplementation : class, TStrategy
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        CompositionValidation.ValidateCreatable(typeof(TImplementation), "Strategy");

        if (definition.Mappings.ContainsKey(key))
        {
            throw new InvalidOperationException($"Strategy key '{key}' is already registered for {typeof(TStrategy).Name}.");
        }

        definition.Mappings[key] = typeof(TImplementation);
        return this;
    }

    public IStrategyBuilder<TStrategy> WithDefault(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        definition.DefaultKey = key;
        return this;
    }
}

internal sealed class StrategyResolver<TStrategy>(
    IServiceProvider services,
    StrategyDefinition<TStrategy> definition) : IStrategyResolver<TStrategy>
    where TStrategy : class
{
    public IReadOnlyCollection<string> Keys => definition.Mappings.Keys.ToArray();

    public TStrategy Resolve(string key)
    {
        if (!this.TryResolve(key, out var strategy))
        {
            throw new InvalidOperationException($"No strategy registered for key '{key}' and contract {typeof(TStrategy).Name}.");
        }

        return strategy;
    }

    public bool TryResolve(string key, out TStrategy strategy)
    {
        strategy = null;

        if (string.IsNullOrWhiteSpace(key) || !definition.Mappings.TryGetValue(key, out var implementationType))
        {
            return false;
        }

        strategy = (TStrategy)CompositionRuntime.ResolveOrCreate(implementationType, services);
        return true;
    }

    public TStrategy ResolveDefault()
    {
        if (string.IsNullOrWhiteSpace(definition.DefaultKey))
        {
            throw new InvalidOperationException($"No default strategy key is configured for {typeof(TStrategy).Name}.");
        }

        if (!definition.Mappings.ContainsKey(definition.DefaultKey))
        {
            throw new InvalidOperationException(
                $"Default strategy key '{definition.DefaultKey}' is not registered for {typeof(TStrategy).Name}.");
        }

        return this.Resolve(definition.DefaultKey);
    }
}
