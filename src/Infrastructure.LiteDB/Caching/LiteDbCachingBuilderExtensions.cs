// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Security.Cryptography;
using System.Text;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.LiteDb;

/// <summary>Registers complete native Application Caching stores independently from Common Caching.</summary>
public static class LiteDbCachingBuilderExtensions
{
    /// <summary>Declares a named complete Application Caching provider with isolated native metadata and chunk collections.</summary>
    /// <param name="builder">The shared Application Caching startup builder.</param>
    /// <param name="name">The provider alias selected by collections.</param>
    /// <param name="configure">Optional finite cache storage settings.</param>
    /// <param name="storage">Optional named database and collection namespace.</param>
    /// <returns>The same builder for further collection registration.</returns>
    /// <example><code>services.AddApplicationCaching().WithLiteDbProvider("catalog");</code></example>
    public static ApplicationCachingBuilderContext WithLiteDbProvider(this ApplicationCachingBuilderContext builder, string name = "litedb",
        Action<CacheProviderBuilder> configure = null, Action<LiteDbFeatureOptions> storage = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.IsFinalized) { throw new InvalidOperationException("The Application Caching catalog is finalized."); }

        name = CacheIdentityNormalizer.Name(name);
        var registration = builder.Services.LastOrDefault(d => d.ServiceType == typeof(Registration))?.ImplementationInstance as Registration;
        if (registration is null)
        {
            registration = new(builder);
            builder.Services.AddSingleton(registration);
            builder.WithProviderKind("LiteDB", LiteDbCacheStoreProvider.DefaultCapabilities, (sp, alias, options) =>
                new LiteDbCacheStoreProvider(sp.GetRequiredService<IServiceScopeFactory>(), registration.Providers[alias], alias, options,
                    sp.GetService<TimeProvider>(), sp.GetService<CacheRuntimeResources>(), sp.GetService<CacheCatalog>()?.Options.Diagnostics));
        }

        if (!ReferenceEquals(registration.Builder, builder) || registration.Providers.ContainsKey(name))
        { throw new InvalidOperationException("A native Application Caching provider alias must be declared once on the shared startup builder."); }

        var prefix = "appcache_" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..16];
        var native = builder.Services.RegisterLiteDbFeature(prefix, ["state", "catalog", "entries", "payloads", "chunks", "keys", "fences", "operations", "receipts"], storage);
        registration.Providers.Add(name, native);
        builder.WithProvider(name, provider => { provider.With(o => o.Kind, "LiteDB"); configure?.Invoke(provider); });
        return builder;
    }

    private sealed class Registration(ApplicationCachingBuilderContext builder)
    {
        public ApplicationCachingBuilderContext Builder { get; } = builder;
        public Dictionary<string, LiteDbFeatureOptions> Providers { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
