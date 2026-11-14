// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Declares complete named in-memory cache providers with finite default capacities.</summary>
/// <example><code>services.AddApplicationCaching().WithInMemoryProvider();</code></example>
public static class InMemoryCacheRegistrationExtensions
{
    /// <summary>Declares a named in-memory provider without creating its runtime store.</summary>
    /// <param name="builder">The shared advanced-cache startup builder.</param>
    /// <param name="name">The unique provider alias selected by collections; defaults to memory.</param>
    /// <param name="configure">Optional typed native settings below configuration overrides.</param>
    /// <returns>The same startup builder for additional collections and providers.</returns>
    /// <remarks>Defaults to 128 MiB resident and storage capacity, 100000 entries, and 16 MiB metadata. Lower resident capacity scales implicit metadata to at most one eighth of that ceiling. Explicit metadata is validated as supplied. This method enables neither compression nor collection hot-entry retention.</remarks>
    /// <exception cref="ArgumentNullException">The startup builder is null.</exception>
    /// <exception cref="InvalidOperationException">The startup catalog is finalized.</exception>
    /// <exception cref="ArgumentException">The provider alias is invalid or already declared.</exception>
    /// <example><code>builder.WithInMemoryProvider("lookups", provider =&gt; provider.Capacity(ByteSize.Megabytes(8)).MaxEntries(1000));</code></example>
    public static ApplicationCachingBuilderContext WithInMemoryProvider(this ApplicationCachingBuilderContext builder,
        string name = "memory", Action<InMemoryCacheProviderOptionsBuilder> configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.IsFinalized) { throw new InvalidOperationException("The cache catalog is finalized for this host lifetime."); }

        var options = new InMemoryCacheProviderOptionsBuilder();
        configure?.Invoke(options);
        var settings = options.Patch.Apply(new CacheProviderOptions(), "Fluent", new Dictionary<string, string>());
        var metadataBytes = Math.Min(16 * CacheDefaults.MiB, Math.Max(1, settings.MaxResidentBytes / 8));
        return builder.WithProvider(name, provider =>
        {
            provider.Patch.Merge(null, options.Patch);
            provider.With(value => value.Kind, "InMemory");
        }, _ => new CacheProviderOptions
        {
            MaxResidentBytes = 128 * CacheDefaults.MiB,
            MaxStorageBytes = 128 * CacheDefaults.MiB,
            MaxEntries = 100000,
            MetadataCacheBytes = metadataBytes
        });
    }
}