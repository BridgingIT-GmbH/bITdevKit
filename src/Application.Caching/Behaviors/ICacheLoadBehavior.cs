// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Exposes owned source execution after admission, authority capture, and dependency-scope creation.</summary>
/// <example><code>var source = context.Items[0];</code></example>
public sealed class CacheLoadBehaviorContext
{
    /// <summary>Describes owned source work whose items already hold source admission and authority.</summary>
    /// <param name="binding">The validated collection binding.</param>
    /// <param name="items">The nonempty bounded list of owned item contexts for this execution.</param>
    /// <remarks>Construction describes ownership; it does not acquire source admission, create a scope, or grant publication authority.</remarks>
    /// <example><code>var context = new CacheLoadBehaviorContext(binding, ownedItems);</code></example>
    public CacheLoadBehaviorContext(CacheCollectionBinding binding, IReadOnlyList<CacheLoadContext> items)
    {
        this.Collection = binding.Name;
        this.Provider = binding.Provider.Name;
        this.ValueType = binding.Contract.ValueType;
        this.IsBinary = binding.Contract.IsBinary;
        this.Items = items;
        this.Deadline = items.Min(i => i.Deadline);
    }

    /// <summary>Gets the bounded declared collection alias.</summary>
    public string Collection { get; }

    /// <summary>Gets the bounded complete-provider alias, without native settings.</summary>
    public string Provider { get; }

    /// <summary>Gets the statically approved value type.</summary>
    public Type ValueType { get; }

    /// <summary>Gets whether this work produces raw binary payloads.</summary>
    public bool IsBinary { get; }

    /// <summary>Gets only this execution's owned keys and their captured owner items/scoped services.</summary>
    public IReadOnlyList<CacheLoadContext> Items { get; }

    /// <summary>Gets the original fixed source deadline. Behaviors cannot extend it.</summary>
    public DateTimeOffset Deadline { get; }

    /// <summary>Gets or sets the runtime-owned continuation guard shared by this source behavior invocation.</summary>
    public object Invocation { get; set; }
}

/// <summary>Continues one owned work unit. Retry policies belong inside its mandatory source-attempt budget.</summary>
/// <typeparam name="TResult">The engine's source-work receipt.</typeparam>
/// <param name="context">The supplied owned execution context.</param>
/// <param name="cancellationToken">The owned execution token.</param>
/// <returns>The actual owned work outcome, independently of each caller's wait.</returns>
public delegate ValueTask<Result<TResult>> CacheLoadContinuation<TResult>(CacheLoadBehaviorContext context, CancellationToken cancellationToken);

/// <summary>Observes or explicitly rejects owned work once, independently of followers and individual source retry attempts.</summary>
/// <example><code>return await next(context, cancellationToken);</code></example>
public interface ICacheLoadBehavior
{
    /// <summary>Checks contract compatibility once during chain construction.</summary>
    /// <param name="valueType">The collection's statically registered value type.</param>
    /// <param name="isBinary">Whether the collection declares raw binary payloads.</param>
    /// <returns>True when this behavior can participate in the declared contract.</returns>
    bool Supports(Type valueType, bool isBinary) => true;

    /// <summary>Executes inside source admission/scope/authority. It cannot repeat publication or fabricate a successful source completion.</summary>
    /// <typeparam name="TResult">The mandatory work's response type.</typeparam>
    /// <param name="context">The original engine-supplied context for this invocation.</param>
    /// <param name="next">The remaining mandatory chain, which may be invoked at most once with the original context.</param>
    /// <param name="cancellationToken">The original bounded invocation cancellation token.</param>
    /// <returns>The actual continued result or an explicit rejection before work starts.</returns>
    /// <example><code>return await next(context, cancellationToken);</code></example>
    ValueTask<Result<TResult>> ExecuteAsync<TResult>(CacheLoadBehaviorContext context, CacheLoadContinuation<TResult> next,
        CancellationToken cancellationToken = default);
}