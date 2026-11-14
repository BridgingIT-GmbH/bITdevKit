// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Provides only the captured misses owned by this batch. Hits and foreign flights are excluded.</summary>
/// <example><code>var keys = context.Items.Select(item => item.Item.Key);</code></example>
public sealed record CacheBatchLoadContext(
    IReadOnlyList<CacheLoadContext> Items, IServiceProvider Services, DateTimeOffset Deadline);

/// <summary>Identifies one ordered-independent typed batch completion. The engine restores original caller order.</summary>
/// <typeparam name="T">The declared read-only value contract.</typeparam>
/// <param name="Key">One of the batch's owned exact keys.</param>
/// <param name="Result">Its source value, confirmed absence, or explicit source failure.</param>
/// <example><code>yield return new CacheBatchLoadResult&lt;ProductCard&gt;(key, Result&lt;CacheLoadResult&lt;ProductCard&gt;&gt;.Success(new(card)));</code></example>
public sealed record CacheBatchLoadResult<T>(string Key, Result<CacheLoadResult<T>> Result);

/// <summary>Executes a repeatable bounded batch read/computation and yields independent owned-key completions promptly.</summary>
/// <typeparam name="T">The declared value contract.</typeparam>
/// <param name="context">Only captured owner misses and the cache-owned scope/deadline.</param>
/// <param name="cancellationToken">The owned batch-work token, independent of any one caller's wait.</param>
/// <returns>One completion per owned key. Omitted keys receive explicit Incomplete failures.</returns>
public delegate IAsyncEnumerable<CacheBatchLoadResult<T>> CacheBatchLoadDelegate<T>(
    CacheBatchLoadContext context, CancellationToken cancellationToken);

/// <summary>Provides owned binary misses and bounded on-demand cache-owned staging destinations.</summary>
/// <example><code>var destination = await context.OpenWriteAsync(key, cancellationToken);</code></example>
public sealed class CacheBinaryBatchLoadContext
{
    private readonly Func<string, CancellationToken, ValueTask<Result<Stream>>> open;

    /// <summary>Binds admitted binary batch items to their owned staging streams and per-item result reporting callback.</summary>
    public CacheBinaryBatchLoadContext(IReadOnlyList<CacheLoadContext> items, IServiceProvider services,
        DateTimeOffset deadline, Func<string, CancellationToken, ValueTask<Result<Stream>>> open)
    {
        this.Items = items;
        this.Services = services;
        this.Deadline = deadline;
        this.open = open;
    }

    /// <summary>Gets only misses owned by this batch, with captured owner dependencies.</summary>
    public IReadOnlyList<CacheLoadContext> Items { get; }

    /// <summary>Gets cache-owned scoped dependencies. Do not capture a departing request's scope.</summary>
    public IServiceProvider Services { get; }

    /// <summary>Gets the original fixed owned deadline, including retries and publication.</summary>
    public DateTimeOffset Deadline { get; }

    /// <summary>Acquires one owned key's bounded forward-only destination. Leave it open and yield completion after writing.</summary>
    public ValueTask<Result<Stream>> OpenWriteAsync(string key, CancellationToken cancellationToken = default) =>
        this.open(key, cancellationToken);
}

/// <summary>Identifies one completed binary source payload or explicit absence. Delivery still requires confirmed publication.</summary>
/// <param name="Key">One of the batch's owned exact keys.</param>
/// <param name="Result">The source descriptor/absence or explicit failure.</param>
/// <example><code>yield return new(key, Result&lt;CacheBinaryLoadResult&gt;.Success(new()));</code></example>
public sealed record CacheBinaryBatchLoadResult(string Key, Result<CacheBinaryLoadResult> Result);

/// <summary>Executes repeatable binary batch source work, using only destinations admitted by the cache-owned context.</summary>
/// <param name="context">Owned keys, scoped dependencies, and bounded destination acquisition.</param>
/// <param name="cancellationToken">The owned work cancellation token.</param>
/// <returns>Independent completions. Unknown/duplicate keys are contract failures and omitted keys are incomplete.</returns>
public delegate IAsyncEnumerable<CacheBinaryBatchLoadResult> CacheBinaryBatchLoadDelegate(
    CacheBinaryBatchLoadContext context, CancellationToken cancellationToken);