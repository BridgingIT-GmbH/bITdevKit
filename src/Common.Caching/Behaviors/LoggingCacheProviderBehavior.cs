// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.Behaviors;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public partial class LoggingCacheProviderBehavior : ICacheProvider
{
    private readonly ICacheProvider inner;

    public LoggingCacheProviderBehavior(
        ILoggerFactory loggerFactory,
        ICacheProvider inner)
    {
        EnsureArg.IsNotNull(inner, nameof(inner));

        this.Logger = loggerFactory?.CreateLogger<LoggingCacheProviderBehavior>() ?? NullLoggerFactory.Instance.CreateLogger<LoggingCacheProviderBehavior>();
        this.inner = inner;
    }

    protected ILogger<LoggingCacheProviderBehavior> Logger { get; }

    public T Get<T>(string key)
    {
        throw new NotImplementedException();
    }

    public Task<T> GetAsync<T>(string key, CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<string> GetKeys()
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<string>> GetKeysAsync(CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    public void Remove(string key)
    {
        throw new NotImplementedException();
    }

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    public void RemoveStartsWith(string key)
    {
        throw new NotImplementedException();
    }

    public Task RemoveStartsWithAsync(string key, CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    public void Set<T>(string key, T value, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null)
    {
        throw new NotImplementedException();
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public bool TryGet<T>(string key, out T value)
    {
        throw new NotImplementedException();
    }

    public Task<bool> TryGetAsync<T>(string key, out T value, CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "inprocess cache add (key={CacheKey})")]
        public static partial void LogCacheAdd(ILogger logger, string cacheKey);

        [LoggerMessage(1, LogLevel.Information, "inprocess cache 'remove' (key={CacheKey})")]
        public static partial void LogCacheRemove(ILogger logger, string cacheKey);
    }
}
