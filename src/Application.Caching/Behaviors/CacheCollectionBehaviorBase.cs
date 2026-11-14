// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Forwards optional typed operation interception; subclasses override only their relevant operation kinds.</summary>
/// <typeparam name="T">The statically approved collection value type.</typeparam>
/// <example><code>return context.Operation == CacheOperationKind.Get ? ObserveAsync(context, next, cancellationToken) : next(context, cancellationToken);</code></example>
public abstract class CacheCollectionBehaviorBase<T> : ICacheCollectionBehavior
{
    /// <inheritdoc />
    public bool Supports(Type valueType, bool isBinary) => !isBinary && valueType == typeof(T);

    /// <inheritdoc />
    public virtual ValueTask<Result<TResult>> ExecuteAsync<TResult>(CacheOperationContext context, CacheOperationContinuation<TResult> next,
        CancellationToken cancellationToken = default) => next(context, cancellationToken);
}

/// <summary>Forwards optional raw binary interception without taking ownership of caller input or returned handles.</summary>
/// <example><code>return next(context, cancellationToken);</code></example>
public abstract class CacheBinaryCollectionBehaviorBase : ICacheCollectionBehavior
{
    /// <inheritdoc />
    public bool Supports(Type valueType, bool isBinary) => isBinary;

    /// <inheritdoc />
    public virtual ValueTask<Result<TResult>> ExecuteAsync<TResult>(CacheOperationContext context, CacheOperationContinuation<TResult> next,
        CancellationToken cancellationToken = default) => next(context, cancellationToken);
}