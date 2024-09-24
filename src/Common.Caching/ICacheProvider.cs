// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public interface ICacheProvider
{
    T Get<T>(string key);

    Task<T> GetAsync<T>(string key, CancellationToken token = default);

    bool TryGet<T>(string key, out T value);

    Task<bool> TryGetAsync<T>(string key, out T value, CancellationToken token = default);

    IEnumerable<string> GetKeys();

    Task<IEnumerable<string>> GetKeysAsync(CancellationToken token = default);

    void Remove(string key);

    Task RemoveAsync(string key, CancellationToken token = default);

    void RemoveStartsWith(string key);

    Task RemoveStartsWithAsync(string key, CancellationToken token = default);

    void Set<T>(string key, T value, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null);

    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null,
        CancellationToken cancellationToken = default);
}