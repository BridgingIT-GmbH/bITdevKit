// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Identity;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Composes native permission persistence with the existing Identity evaluators and policies.</summary>
public static class LiteDbIdentityBuilderExtensions
{
    /// <summary>Registers native permission storage and the configured entity authorization services.</summary>
    /// <param name="services">The host services.</param>
    /// <param name="configure">Existing entity, hierarchy, evaluator, and default-permission declarations.</param>
    /// <param name="storage">The selected native database and namespace.</param>
    /// <returns>The host services.</returns>
    /// <example><code>services.AddLiteDbEntityPermissions(options => options.AddEntity&lt;Ledger&gt;(Permission.Read));</code></example>
    public static IServiceCollection AddLiteDbEntityPermissions(this IServiceCollection services, Action<EntityPermissionOptions> configure = null, Action<LiteDbFeatureOptions> storage = null)
    {
        var settings = new EntityPermissionOptions();
        configure?.Invoke(settings);
        var native = services.RegisterLiteDbFeature("permissions", ["grants"], storage);
        var registration = LiteDbDatabaseRegistry.GetOrAdd(services).Get(native.DatabaseName);
        services.AddSingleton(settings);
        services.AddScoped(sp => new LiteDbEntityPermissionProvider(new(sp.GetRequiredService<IServiceScopeFactory>(), native), settings,
            type => registration.GetCollection(type), sp.GetRequiredKeyedService<LiteDbDatabaseSession>(native.DatabaseName), sp, sp.GetService<ICacheProvider>()));
        services.Replace(ServiceDescriptor.Scoped<IEntityPermissionProvider>(sp => sp.GetRequiredService<LiteDbEntityPermissionProvider>()));
        foreach (var entity in settings.EntityConfigurations)
        {
            var evaluator = settings.EvaluatorConfigurations.TryGetValue(entity.EntityType, out var custom) ? custom : typeof(EntityPermissionEvaluator<>).MakeGenericType(entity.EntityType);
            var contract = typeof(IEntityPermissionEvaluator<>).MakeGenericType(entity.EntityType);
            if (settings.EvaluatorConfigurations.ContainsKey(entity.EntityType)) { services.AddScoped(contract, evaluator); }
            else
            {
                services.AddScoped(contract, sp => ActivatorUtilities.CreateInstance(sp, evaluator,
                    new EvaluationCache(sp.GetService<ICacheProvider>(), sp.GetRequiredKeyedService<LiteDbDatabaseSession>(native.DatabaseName))));
            }

            services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IAuthorizationHandler), typeof(EntityPermissionAuthorizationRequirementHandler<>).MakeGenericType(entity.EntityType)));
        }

        foreach (var provider in settings.DefaultPermissionProviders) services.AddScoped(provider.InterfaceType, provider.ImplementationType);
        if (settings.EnableCaching)
        {
            services.TryAddSingleton<ICacheProvider>(_ => new PermissionCache());
        }

        services.AddAuthorization(options =>
        {
            foreach (var entity in settings.EntityConfigurations.Where(entity => entity.Permissions?.Length > 0)) options.AddEntityPermissionPolicy(entity.EntityType, entity.Permissions);
        });
        return services;
    }

    /// <summary>Selects native entity permissions through the existing authorization builder.</summary>
    /// <param name="context">The existing authorization builder.</param>
    /// <param name="configure">Existing entity and hierarchy declarations.</param>
    /// <param name="storage">Optional native database and namespace settings.</param>
    /// <returns>The same authorization builder.</returns>
    /// <example><code>authorization.WithLiteDbEntityPermissions(options => options.AddEntity&lt;Ledger&gt;(Permission.Read));</code></example>
    public static AuthorizationOptionsBuilder WithLiteDbEntityPermissions(this AuthorizationOptionsBuilder context, Action<EntityPermissionOptions> configure = null, Action<LiteDbFeatureOptions> storage = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddLiteDbEntityPermissions(configure, storage);
        return context;
    }

    // Uncommitted entity/grant state must never enter the shared evaluator cache.
    private sealed class EvaluationCache(ICacheProvider inner, LiteDbDatabaseSession session) : ICacheProvider
    {
        public T Get<T>(string key) => this.TryGet<T>(key, out var value) ? value : default;
        public Task<T> GetAsync<T>(string key, CancellationToken token = default) { token.ThrowIfCancellationRequested(); return Task.FromResult(this.Get<T>(key)); }

        public bool TryGet<T>(string key, out T value)
        {
            if (!session.HasTransaction && inner is not null) { return inner.TryGet(key, out value); }

            value = default; return false;
        }

        public Task<bool> TryGetAsync<T>(string key, out T value, CancellationToken token = default) { token.ThrowIfCancellationRequested(); return Task.FromResult(this.TryGet(key, out value)); }

        public IEnumerable<string> GetKeys() => inner?.GetKeys() ?? [];
        public Task<IEnumerable<string>> GetKeysAsync(CancellationToken token = default) { token.ThrowIfCancellationRequested(); return Task.FromResult(this.GetKeys()); }

        public void Remove(string key) => inner?.Remove(key);
        public Task RemoveAsync(string key, CancellationToken token = default) => inner?.RemoveAsync(key, token) ?? Task.CompletedTask;
        public void RemoveStartsWith(string key) => inner?.RemoveStartsWith(key);
        public Task RemoveStartsWithAsync(string key, CancellationToken token = default) => inner?.RemoveStartsWithAsync(key, token) ?? Task.CompletedTask;
        public void Set<T>(string key, T value, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null)
        { if (!session.HasTransaction) { inner?.Set(key, value, slidingExpiration, absoluteExpiration); } }

        public Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); this.Set(key, value, slidingExpiration, absoluteExpiration); return Task.CompletedTask; }
    }

    // The permission evaluator uses synchronous cache access. This fallback uses the public
    // MemoryCache.Keys API so prefix eviction does not depend on framework-private fields.
    private sealed class PermissionCache : ICacheProvider, IDisposable
    {
        private readonly MemoryCache cache = new(new MemoryCacheOptions());
        public T Get<T>(string key) => this.cache.Get<T>(key);
        public Task<T> GetAsync<T>(string key, CancellationToken token = default) { token.ThrowIfCancellationRequested(); return Task.FromResult(this.Get<T>(key)); }

        public bool TryGet<T>(string key, out T value) => this.cache.TryGetValue(key, out value);
        public Task<bool> TryGetAsync<T>(string key, out T value, CancellationToken token = default) { token.ThrowIfCancellationRequested(); return Task.FromResult(this.TryGet(key, out value)); }

        public IEnumerable<string> GetKeys() => this.cache.Keys.OfType<string>().ToArray();
        public Task<IEnumerable<string>> GetKeysAsync(CancellationToken token = default) { token.ThrowIfCancellationRequested(); return Task.FromResult(this.GetKeys()); }

        public void Remove(string key) => this.cache.Remove(key);
        public Task RemoveAsync(string key, CancellationToken token = default) { token.ThrowIfCancellationRequested(); this.Remove(key); return Task.CompletedTask; }

        public void RemoveStartsWith(string key)
        {
            if (string.IsNullOrEmpty(key)) { return; }

            foreach (var existing in this.GetKeys().Where(value => value.StartsWith(key, StringComparison.Ordinal))) { this.Remove(existing); }
        }

        public Task RemoveStartsWithAsync(string key, CancellationToken token = default) { token.ThrowIfCancellationRequested(); this.RemoveStartsWith(key); return Task.CompletedTask; }

        public void Set<T>(string key, T value, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null) =>
            this.cache.Set(key, value, new MemoryCacheEntryOptions { SlidingExpiration = slidingExpiration, AbsoluteExpiration = absoluteExpiration });
        public Task SetAsync<T>(string key, T value, TimeSpan? slidingExpiration = null, DateTimeOffset? absoluteExpiration = null, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); this.Set(key, value, slidingExpiration, absoluteExpiration); return Task.CompletedTask; }

        public void Dispose() => this.cache.Dispose();
    }
}
