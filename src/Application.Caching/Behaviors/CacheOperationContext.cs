// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Identifies one public collection call without copying payloads or exposing connection settings.</summary>
/// <example><code>var operation = context.Operation;</code></example>
public sealed class CacheOperationContext
{
    /// <summary>Describes a call against one validated collection without granting provider authority.</summary>
    /// <param name="binding">The effective collection binding.</param>
    /// <param name="operation">The public operation being observed.</param>
    /// <param name="identity">The exact normalized identity, or null for collection-wide and batch operations.</param>
    /// <example><code>var context = new CacheOperationContext(binding, CacheOperationKind.Clear);</code></example>
    public CacheOperationContext(CacheCollectionBinding binding, CacheOperationKind operation, CacheEntryIdentity? identity = null)
    {
        this.Collection = binding.Name;
        this.Provider = binding.Provider.Name;
        this.ValueType = binding.Contract.ValueType;
        this.IsBinary = binding.Contract.IsBinary;
        this.Operation = operation;
        this.Identity = identity;
    }

    /// <summary>Gets the bounded declared collection alias, suitable for bounded metric labels.</summary>
    public string Collection { get; }

    /// <summary>Gets the bounded declared provider alias, without its native settings.</summary>
    public string Provider { get; }

    /// <summary>Gets the statically registered value type. It is never resolved from persisted CLR names.</summary>
    public Type ValueType { get; }

    /// <summary>Gets whether this collection declares raw binary data.</summary>
    public bool IsBinary { get; }

    /// <summary>Gets the public operation, independently of source execution and payload transfer completion.</summary>
    public CacheOperationKind Operation { get; }

    /// <summary>Gets the exact identity when applicable. Observers must not use keys as metric labels or log them by default.</summary>
    public CacheEntryIdentity? Identity { get; }

    /// <summary>Gets or sets the runtime-owned continuation guard shared by this operation&apos;s behavior stages.</summary>
    public object Invocation { get; set; }
}

/// <summary>Identifies public cache operations for optional observers and explicit rejection policies.</summary>
public enum CacheOperationKind
{
    /// <summary>Typed direct lookup.</summary>
    Get,

    /// <summary>Logical payload acquisition; completion is observed separately.</summary>
    OpenRead,

    /// <summary>Read-through source loading or joining.</summary>
    GetOrLoad,

    /// <summary>Ordered partial batch read-through.</summary>
    GetOrLoadMany,

    /// <summary>Conditional source refresh.</summary>
    Refresh,

    /// <summary>Direct typed/binary publication.</summary>
    Set,

    /// <summary>Explicit validated encoded publication.</summary>
    SetEncoded,

    /// <summary>Exact key invalidation.</summary>
    Remove,

    /// <summary>Authoritative sliding renewal.</summary>
    Touch,

    /// <summary>Metadata-only invalidation preview.</summary>
    PreviewInvalidation,

    /// <summary>Selected logical invalidation.</summary>
    Invalidate,

    /// <summary>Explicit logical collection clear.</summary>
    Clear
}

/// <summary>Continues one optional operation chain. Invoke at most once, with the supplied context and cancellation.</summary>
/// <typeparam name="TResult">The actual operation response type.</typeparam>
/// <param name="context">The original immutable operation context.</param>
/// <param name="cancellationToken">Caller cancellation.</param>
/// <returns>The mandatory engine's result.</returns>
public delegate ValueTask<Result<TResult>> CacheOperationContinuation<TResult>(CacheOperationContext context, CancellationToken cancellationToken);

/// <summary>Observes or explicitly rejects a public call. It cannot fabricate hits, replay mutations, or hide the mandatory engine's result.</summary>
/// <example><code>return await next(context, cancellationToken);</code></example>
public interface ICacheCollectionBehavior
{
    /// <summary>Checks compatibility once while the collection's effective chain is built.</summary>
    /// <param name="valueType">The collection's statically registered value type.</param>
    /// <param name="isBinary">Whether the collection declares raw binary payloads.</param>
    /// <returns>True when this behavior can participate in the declared contract.</returns>
    bool Supports(Type valueType, bool isBinary) => true;

    /// <summary>Observes a call or returns an explicit failure before continuation. Confirmed engine outcomes survive observer faults.</summary>
    /// <typeparam name="TResult">The mandatory work's response type.</typeparam>
    /// <param name="context">The original engine-supplied context for this invocation.</param>
    /// <param name="next">The remaining mandatory chain, which may be invoked at most once with the original context.</param>
    /// <param name="cancellationToken">The original bounded invocation cancellation token.</param>
    /// <returns>The actual continued result or an explicit rejection before work starts.</returns>
    /// <example><code>return await next(context, cancellationToken);</code></example>
    ValueTask<Result<TResult>> ExecuteAsync<TResult>(CacheOperationContext context, CacheOperationContinuation<TResult> next,
        CancellationToken cancellationToken = default);
}