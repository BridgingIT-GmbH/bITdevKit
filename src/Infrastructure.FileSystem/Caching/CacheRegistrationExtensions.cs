// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the complete native filesystem cache kind independently of filesystem storage and simple Common.Caching.</summary>
/// <example><code>services.AddApplicationCaching().WithFileSystemProvider("reports", provider =&gt; provider.Root(absoluteRoot));</code></example>
public static class CacheRegistrationExtensions
{
    /// <summary>Registers trusted filesystem provider factories; catalog/handle construction performs no native I/O.</summary>
    /// <param name="builder">The advanced-cache composition builder.</param>
    /// <returns>The builder for collection/provider settings.</returns>
    /// <example><code>builder.WithFileSystemProvider(); // Register the native kind for configuration-defined providers.</code></example>
    public static ApplicationCachingBuilderContext WithFileSystemProvider(this ApplicationCachingBuilderContext builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.IsFinalized)
        {
            throw new InvalidOperationException("The cache catalog is finalized for this host lifetime.");
        }

        if (builder.Components.Kinds.TryGetValue("FileSystem", out var registered) && registered.Factory == CreateProvider &&
            registered.CapabilityResolver == FileSystemCacheStoreProvider.CapabilitiesFor)
        {
            return builder;
        }

        return builder.WithProviderKind("FileSystem", FileSystemCacheStoreProvider.CapabilitiesFor, CreateProvider);
    }

    /// <summary>Declares a named complete filesystem cache provider using native fluent options.</summary>
    /// <param name="builder">The advanced-cache composition builder.</param>
    /// <param name="name">The provider alias selected by collections.</param>
    /// <param name="configure">Optional fluent settings below the host configuration overlay.</param>
    /// <returns>The same composition builder for additional providers and collections.</returns>
    /// <remarks>Registers the native kind once. Configuration can supply the root; declaration and catalog construction perform no filesystem I/O.</remarks>
    /// <example><code>builder.WithFileSystemProvider("reports", provider =&gt; provider.Root(absoluteRoot).AccessKind(FileSystemStorageAccessKind.Local));</code></example>
    public static ApplicationCachingBuilderContext WithFileSystemProvider(this ApplicationCachingBuilderContext builder,
        string name, Action<FileSystemCacheProviderOptionsBuilder> configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.WithFileSystemProvider();
        return builder.WithProvider(name, provider =>
        {
            var options = new FileSystemCacheProviderOptionsBuilder();
            configure?.Invoke(options);
            provider.Patch.Merge(null, options.Patch);
            provider.With(value => value.Kind, "FileSystem");
        });
    }

    private static ICacheStoreProvider CreateProvider(IServiceProvider services, string name, CacheProviderOptions options) =>
        new FileSystemCacheStoreProvider(name, options, services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<CacheRuntimeResources>(), services.GetRequiredService<CacheCatalog>().Options.Diagnostics);
}