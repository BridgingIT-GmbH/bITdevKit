// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
/// Defines key-based cache access, enumeration, expiration, and invalidation operations.
/// </summary>
/// <example>
/// <code>
/// var customer = await cache.GetAsync&lt;Customer&gt;($"customer:{customerId}", cancellationToken);
/// if (customer is null)
/// {
///     customer = await repository.FindAsync(customerId, cancellationToken);
///     await cache.SetAsync($"customer:{customerId}", customer, TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
/// }
/// </code>
/// </example>
public interface ICacheProvider
{
    /// <summary>Gets a cached value.</summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="key">The exact cache key.</param>
    /// <returns>The cached value, or the default value of <typeparamref name="T"/> when the key is absent.</returns>
    T Get<T>(string key);

    /// <summary>Asynchronously gets a cached value.</summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="key">The exact cache key.</param>
    /// <param name="token">A token that can cancel the cache operation.</param>
    /// <returns>The cached value, or the default value of <typeparamref name="T"/> when the key is absent.</returns>
    Task<T> GetAsync<T>(string key, CancellationToken token = default);

    /// <summary>Attempts to get a cached value.</summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="key">The exact cache key.</param>
    /// <param name="value">Receives the cached value when found; otherwise, the default value of <typeparamref name="T"/>.</param>
    /// <returns><see langword="true"/> when the key is present; otherwise, <see langword="false"/>.</returns>
    bool TryGet<T>(string key, out T value);

    /// <summary>Attempts to get a cached value through the provider's asynchronous access path.</summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="key">The exact cache key.</param>
    /// <param name="value">Receives the cached value when found; otherwise, the default value of <typeparamref name="T"/>.</param>
    /// <param name="token">A token that can cancel the cache operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the key is present; otherwise, <see langword="false"/>.</returns>
    Task<bool> TryGetAsync<T>(string key, out T value, CancellationToken token = default);

    /// <summary>Enumerates the keys currently exposed by the cache provider.</summary>
    /// <returns>The cache keys.</returns>
    IEnumerable<string> GetKeys();

    /// <summary>Asynchronously enumerates the keys currently exposed by the cache provider.</summary>
    /// <param name="token">A token that can cancel key enumeration.</param>
    /// <returns>The cache keys.</returns>
    Task<IEnumerable<string>> GetKeysAsync(CancellationToken token = default);

    /// <summary>Removes an entry identified by its exact key.</summary>
    /// <param name="key">The exact cache key to remove.</param>
    void Remove(string key);

    /// <summary>Asynchronously removes an entry identified by its exact key.</summary>
    /// <param name="key">The exact cache key to remove.</param>
    /// <param name="token">A token that can cancel the removal operation.</param>
    /// <returns>A task representing the removal operation.</returns>
    Task RemoveAsync(string key, CancellationToken token = default);

    /// <summary>Removes entries whose keys start with a specified prefix.</summary>
    /// <param name="key">The case-sensitive key prefix selected for removal.</param>
    void RemoveStartsWith(string key);

    /// <summary>Asynchronously removes entries whose keys start with a specified prefix.</summary>
    /// <param name="key">The case-sensitive key prefix selected for removal.</param>
    /// <param name="token">A token that can cancel the removal operation.</param>
    /// <returns>A task representing the removal operation.</returns>
    Task RemoveStartsWithAsync(string key, CancellationToken token = default);

    /// <summary>Creates or replaces a cached value with optional sliding and absolute expiration.</summary>
    /// <typeparam name="T">The type of value to cache.</typeparam>
    /// <param name="key">The exact cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="slidingExpiration">The idle duration after which the entry expires, or <see langword="null"/> to use provider configuration.</param>
    /// <param name="absoluteExpiration">The time at which the entry expires regardless of access, or <see langword="null"/> to use provider configuration.</param>
    void Set<T>(string key, T value, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null);

    /// <summary>Asynchronously creates or replaces a cached value with optional sliding and absolute expiration.</summary>
    /// <typeparam name="T">The type of value to cache.</typeparam>
    /// <param name="key">The exact cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="slidingExpiration">The idle duration after which the entry expires, or <see langword="null"/> to use provider configuration.</param>
    /// <param name="absoluteExpiration">The time at which the entry expires regardless of access, or <see langword="null"/> to use provider configuration.</param>
    /// <param name="cancellationToken">A token that can cancel the cache operation.</param>
    /// <returns>A task representing the cache operation.</returns>
    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null,
        CancellationToken cancellationToken = default);
}
