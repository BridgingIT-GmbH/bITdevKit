// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Reports a successful read-only source value or explicitly confirmed source absence.</summary>
/// <typeparam name="T">The value contract.</typeparam>
/// <remarks>A null or empty value is a positive source result when permitted by the contract. Only <see cref="NotFound" /> declares absence. This source result does not itself confirm cache publication.</remarks>
/// <example><code>return CacheLoadResult&lt;ProductCard&gt;.NotFound();</code></example>
public sealed record CacheLoadResult<T>
{
    /// <summary>Creates a successful source value, including a permitted null/empty value.</summary>
    /// <param name="value">The source value. Callers must treat the value as read-only because it may be shared without cloning.</param>
    /// <param name="descriptor">Source-produced dependencies and bounded overrides. Null creates a default descriptor.</param>
    /// <example><code>var loaded = new CacheLoadResult&lt;ProductCard&gt;(card);</code></example>
    public CacheLoadResult(T value, CacheEntryDescriptor descriptor = null)
    {
        this.Value = value;
        this.Descriptor = descriptor ?? new();
    }

    /// <summary>Gets the read-only value; no value is implied by confirmed absence.</summary>
    /// <value>The positive source result, or the default value when <see cref="IsNotFound" /> is true.</value>
    /// <example><code>if (!loaded.IsNotFound) { UseReadOnly(loaded.Value); }</code></example>
    public T Value { get; }

    /// <summary>Gets source-produced metadata and bounded overrides.</summary>
    /// <value>The non-null descriptor whose dependencies are merged with the captured owner's item before publication.</value>
    /// <example><code>var dependencies = loaded.Descriptor.Dimensions;</code></example>
    public CacheEntryDescriptor Descriptor { get; }

    /// <summary>Gets whether the source explicitly confirmed absence.</summary>
    /// <value>True only for an explicit absence result, independently of whether <see cref="Value" /> is null or empty.</value>
    /// <example><code>if (loaded.IsNotFound) { HandleMissingProduct(); }</code></example>
    public bool IsNotFound { get; private init; }

    /// <summary>Creates a confirmed-absence outcome. It is retained only when negative caching is enabled.</summary>
    /// <param name="descriptor">Optional source metadata and bounded negative-entry lifetime overrides. Null uses low eviction priority.</param>
    /// <returns>A source result marked as absent, without a positive payload value.</returns>
    /// <remarks>Use source failures for unavailable or failed reads. Failure to reach the source does not establish that an item is absent.</remarks>
    /// <example><code>return CacheLoadResult&lt;ProductCard&gt;.NotFound(new() { AbsoluteTtl = TimeSpan.FromSeconds(10) });</code></example>
    public static CacheLoadResult<T> NotFound(CacheEntryDescriptor descriptor = null) =>
        new(default, descriptor ?? new() { Priority = CacheEvictionPriority.Low }) { IsNotFound = true };
}

/// <summary>Executes a repeatable source read/computation; never use it for business mutations.</summary>
/// <typeparam name="T">The value contract.</typeparam>
/// <param name="context">The cache-owned scope, identity, and captured owner item.</param>
/// <param name="cancellationToken">The owned execution token, distinct from any one caller's wait.</param>
/// <returns>A successful value/absence or explicit source failure.</returns>
/// <remarks>
/// Resolve scoped dependencies through <see cref="CacheLoadContext.Services" /> instead of capturing a request scope that can end while shared work continues.
/// A delegate may run more than once under the configured retry policy, or may never run when its caller joins another owner.
/// Return explicit absence only after a successful source read confirms it. Do not start nested cache source loads, refreshes, or bypass work from a loader.
/// </remarks>
/// <example><code>
/// CacheLoadDelegate&lt;ProductCard&gt; loader = async (context, token) =&gt;
/// {
///     var source = (IProductSource)context.Services.GetService(typeof(IProductSource));
///     var card = await source.FindAsync(context.Item.Key, token);
///     return card is null
///         ? CacheLoadResult&lt;ProductCard&gt;.NotFound()
///         : new CacheLoadResult&lt;ProductCard&gt;(card);
/// };
/// </code></example>
public delegate ValueTask<Result<CacheLoadResult<T>>> CacheLoadDelegate<T>(CacheLoadContext context, CancellationToken cancellationToken);

/// <summary>Writes a repeatable binary source into the bounded cache-owned destination.</summary>
/// <param name="context">The cache-owned source context.</param>
/// <param name="destination">The engine-owned destination; loaders must leave it open.</param>
/// <param name="cancellationToken">The owned execution cancellation token.</param>
/// <returns>A descriptor; payload delivery still requires confirmed publication.</returns>
/// <remarks>
/// Write the complete logical payload to the supplied bounded destination using the owned token. Never retain, close, or dispose that destination.
/// Use cache-owned scoped services and repeatable source reads because the engine may retry the loader. A joining caller's delegate may never run.
/// Zero bytes are a positive empty payload. Return <see cref="CacheBinaryLoadResult.NotFound" /> only for confirmed source absence.
/// Binary payload handles are opened independently only after storage publication is confirmed.
/// </remarks>
/// <example><code>
/// CacheBinaryLoadDelegate loader = async (context, destination, token) =&gt;
/// {
///     var source = (IReportSource)context.Services.GetService(typeof(IReportSource));
///     await source.CopyToAsync(context.Item.Key, destination, token);
///     return new CacheBinaryLoadResult();
/// };
/// </code></example>
public delegate ValueTask<Result<CacheBinaryLoadResult>> CacheBinaryLoadDelegate(
    CacheLoadContext context, Stream destination, CancellationToken cancellationToken);