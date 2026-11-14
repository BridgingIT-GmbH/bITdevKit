// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Reads or computes a repeatable typed value using a code-registered background source.</summary>
/// <typeparam name="T">The collection value contract.</typeparam>
/// <remarks>No business mutation is permitted. Refresh/retry/restart can repeat this loader.</remarks>
/// <example><code>var result = await loader.LoadAsync(context, cancellationToken);</code></example>
public interface ICacheRefreshLoader<T>
{
    /// <summary>Returns a read-only value or explicitly confirmed absence from the trusted source.</summary>
    ValueTask<Result<CacheLoadResult<T>>> LoadAsync(CacheLoadContext context, CancellationToken cancellationToken = default);
}