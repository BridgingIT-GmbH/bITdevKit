// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.IO.Compression;

/// <summary>Records explicit collection policies without materializing a runtime engine or accessing a store.</summary>
/// <example><code>collection.UseProvider("memory").ExpireAfter(TimeSpan.FromMinutes(5));</code></example>
public sealed class CacheCollectionBuilder : PatchOptionsBuilder<CacheCollectionOptions, CacheCollectionBuilder>
{
    private readonly List<CacheDimensionSchema> dimensions = [];

    /// <summary>Explicitly enables or disables provider access and retention.</summary>
    /// <param name="enabled">Whether collection caching is enabled.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>collection.Enabled(false);</code></example>
    public CacheCollectionBuilder Enabled(bool enabled = true) => this.With(options => options.Enabled, enabled);

    /// <summary>Selects this typed contract's default JSON codec profile.</summary>
    /// <returns>The same builder.</returns>
    /// <example><code>collection.UseJson();</code></example>
    public CacheCollectionBuilder UseJson() => this.UseCodec("json");

    /// <summary>Selects fixed publication-relative expiration and disables sliding renewal.</summary>
    /// <param name="ttl">The finite publication-relative lifetime.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>collection.ExpireAfter(TimeSpan.FromMinutes(5));</code></example>
    public CacheCollectionBuilder ExpireAfter(TimeSpan ttl) => this.Expiration(expiration => expiration.Absolute(ttl));

    /// <summary>Selects shared source work through the complete provider's cross-node authority.</summary>
    /// <returns>The same builder.</returns>
    /// <example><code>collection.ShareLoadsAcrossNodes();</code></example>
    public CacheCollectionBuilder ShareLoadsAcrossNodes() => this.Loading(loading => loading.SharedAcrossNodes());

    /// <summary>Configures finite collection resource ceilings without replacing omitted siblings.</summary>
    /// <param name="configure">Records explicit child policy fields; omitted siblings retain their existing precedence.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>collection.Limits(limits => limits.MaxEntries(1000).MaxPayloadBytes(ByteSize.Megabytes(1)));</code></example>
    public CacheCollectionBuilder Limits(Action<CacheCollectionLimitsBuilder> configure)
    {
        var builder = new CacheCollectionLimitsBuilder();
        configure?.Invoke(builder);
        this.Patch.Merge("Limits", builder.Patch);
        return this;
    }

    /// <summary>Configures source coordination, attempts, and finite execution budgets.</summary>
    /// <param name="configure">Records explicit child policy fields; omitted siblings retain their existing precedence.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>collection.Loading(loading => loading.SourceTimeout(TimeSpan.FromSeconds(20)));</code></example>
    public CacheCollectionBuilder Loading(Action<CacheLoadingBuilder> configure)
    {
        var builder = new CacheLoadingBuilder();
        configure?.Invoke(builder);
        this.Patch.Merge("Loading", builder.Patch);
        return this;
    }

    /// <summary>Configures logical freshness independently of physical cleanup.</summary>
    /// <param name="configure">Records explicit child policy fields; omitted siblings retain their existing precedence.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>collection.Expiration(expiration => expiration.Sliding(TimeSpan.FromMinutes(2), TimeSpan.FromHours(1)));</code></example>
    public CacheCollectionBuilder Expiration(Action<CacheExpirationBuilder> configure)
    {
        var builder = new CacheExpirationBuilder();
        configure?.Invoke(builder);
        this.Patch.Merge("Expiration", builder.Patch);
        return this;
    }

    /// <summary>Opts into one cache-managed compression decision using the selected algorithm and level.</summary>
    /// <param name="algorithm">The cache-managed compression algorithm.</param>
    /// <param name="level">The BCL compression effort level.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>collection.Compression(CacheCompressionAlgorithm.Brotli, CompressionLevel.Fastest);</code></example>
    public CacheCollectionBuilder Compression(CacheCompressionAlgorithm algorithm = CacheCompressionAlgorithm.GZip, CompressionLevel level = CompressionLevel.Fastest) => this.With(options => options.Compression.Enabled, true)
        .With(options => options.Compression.Algorithm, algorithm).With(options => options.Compression.Level, level);

    /// <summary>Explicitly disables cache-managed compression while retaining its configured tuning fields.</summary>
    /// <returns>The same builder.</returns>
    /// <example><code>collection.DisableCompression();</code></example>
    public CacheCollectionBuilder DisableCompression() => this.With(options => options.Compression.Enabled, false);

    /// <summary>Opts into metadata-only retention of confirmed source absence.</summary>
    /// <param name="ttl">An optional marker lifetime; omission preserves the inherited lifetime.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>collection.NegativeCaching(TimeSpan.FromSeconds(20));</code></example>
    public CacheCollectionBuilder NegativeCaching(TimeSpan? ttl = null)
    {
        this.With(options => options.NegativeCaching.Enabled, true);
        if (ttl.HasValue) { this.With(options => options.NegativeCaching.Ttl, ttl.Value); }

        return this;
    }

    /// <summary>Explicitly disables retention of confirmed source absence.</summary>
    /// <returns>The same builder.</returns>
    /// <example><code>collection.DisableNegativeCaching();</code></example>
    public CacheCollectionBuilder DisableNegativeCaching() => this.With(options => options.NegativeCaching.Enabled, false);

    /// <summary>Selects bounded pressure eviction or rejection of new storage.</summary>
    /// <param name="mode">The finite storage pressure policy.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>collection.Eviction(CacheEvictionMode.Reject);</code></example>
    public CacheCollectionBuilder Eviction(CacheEvictionMode mode) => this.With(options => options.Eviction, mode);

    /// <summary>Configures transfer buffers, inactivity, and occupied handle lifetimes.</summary>
    /// <param name="configure">Records explicit child policy fields; omitted siblings retain their existing precedence.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>collection.Streaming(streaming => streaming.IdleTimeout(TimeSpan.FromSeconds(15)));</code></example>
    public CacheCollectionBuilder Streaming(Action<CacheStreamingBuilder> configure)
    {
        var builder = new CacheStreamingBuilder();
        configure?.Invoke(builder);
        this.Patch.Merge("Streaming", builder.Patch);
        return this;
    }

    /// <summary>Selects explicit ordered optional behavior stage lists.</summary>
    /// <param name="configure">Records explicit child policy fields; omitted siblings retain their existing precedence.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>collection.Behaviors(behaviors => behaviors.CollectionOperations("logging"));</code></example>
    public CacheCollectionBuilder Behaviors(Action<CacheBehaviorBuilder> configure)
    {
        var builder = new CacheBehaviorBuilder();
        configure?.Invoke(builder);
        this.Patch.Merge("Behaviors", builder.Patch);
        return this;
    }

    /// <summary>Opts into bounded warm-up and records only explicitly configured child fields.</summary>
    /// <param name="configure">Records explicit child policy fields; omitted siblings retain their existing precedence.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>collection.Warmup(warmup => warmup.UseLoader("catalog-source").Items(new CacheItem("current")));</code></example>
    public CacheCollectionBuilder Warmup(Action<CacheWarmupBuilder> configure)
    {
        var builder = new CacheWarmupBuilder();
        configure?.Invoke(builder);
        this.With(options => options.Warmup.Enabled, true);
        this.Patch.Merge("Warmup", builder.Patch);
        return this;
    }

    /// <summary>Opts into refresh through a trusted source and explicit child settings.</summary>
    /// <param name="configure">The refresh settings to merge without replacing omitted siblings.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>collection.Refresh(refresh => refresh.UseLoader("catalog-source").Ahead(TimeSpan.FromSeconds(30)));</code></example>
    public CacheCollectionBuilder Refresh(Action<CacheRefreshBuilder> configure)
    {
        var builder = new CacheRefreshBuilder();
        configure?.Invoke(builder);
        this.With(options => options.Refresh.Enabled, true);
        this.Patch.Merge("Refresh", builder.Patch);
        return this;
    }

    /// <summary>Selects a code-registered contract alias.</summary>
    /// <param name="alias">The trusted contract alias, compared without case.</param>
    /// <returns>The same builder.</returns>
    /// <remarks>Omit this selection when the collection can use its default JSON contract or one unambiguous registered contract.</remarks>
    /// <example><code>collection.UseContract("product-card");</code></example>
    public CacheCollectionBuilder UseContract(string alias) => this.With(x => x.Contract, CacheIdentityNormalizer.Name(alias));

    /// <summary>Selects one complete named provider.</summary>
    /// <param name="name">The code-registered or configured provider name, compared without case.</param>
    /// <returns>The same builder.</returns>
    /// <remarks>Provider selection does not enable cross-node source sharing. Select ShareLoadsAcrossNodes separately when required.</remarks>
    /// <example><code>collection.UseProvider("catalog-db").ShareLoadsAcrossNodes();</code></example>
    public CacheCollectionBuilder UseProvider(string name) => this.With(x => x.Provider, CacheIdentityNormalizer.Name(name));

    /// <summary>Selects an explicitly registered contract representation version.</summary>
    public CacheCollectionBuilder WithContractVersion(string version) => this.With(x => x.ContractVersion, CacheIdentityNormalizer.ContractVersion(version));

    /// <summary>Selects a trusted codec profile alias.</summary>
    /// <param name="alias">The codec profile registered within the selected value contract.</param>
    /// <returns>The same builder.</returns>
    /// <remarks>This selects the writer. Persisted codec format and version select the registered reader for existing entries.</remarks>
    /// <example><code>collection.UseCodec("delta-series");</code></example>
    public CacheCollectionBuilder UseCodec(string alias) => this.With(x => x.Codec, CacheIdentityNormalizer.Name(alias));

    /// <summary>Sets authoritative sliding renewal plus a finite absolute age cap.</summary>
    public CacheCollectionBuilder WithSlidingExpiration(TimeSpan idle, TimeSpan absoluteCap) => this
        .With(x => x.Expiration.SlidingEnabled, true)
        .With(x => x.Expiration.SlidingIdleTimeout, idle)
        .With(x => x.Expiration.AbsoluteCap, absoluteCap);

    /// <summary>Selects source coordination scope without changing the source strategy.</summary>
    public CacheCollectionBuilder WithCoordination(CacheCoordinationScope scope) => this.With(x => x.Loading.CoordinationScope, scope);

    /// <summary>Selects shared or explicit independent source execution.</summary>
    public CacheCollectionBuilder WithLoadingStrategy(CacheLoadingStrategy strategy) => this.With(x => x.Loading.Strategy, strategy);

    /// <summary>Declares a supported scalar dimension. Signed integer aliases normalize to Int64.</summary>
    /// <example><code>collection.WithScalarDimension&lt;string&gt;("region");</code></example>
    public CacheCollectionBuilder WithScalarDimension<T>(string name, bool allowNull = false) =>
        this.WithDimension(new(name, Kind(typeof(T)), CacheDimensionForm.Scalar, allowNull));

    /// <summary>Groups typed scalar, set, and interval dependency schema declarations for this collection.</summary>
    /// <param name="configure">Optional declarations to append in order. Null or an empty callback preserves existing declarations.</param>
    /// <returns>The same collection builder.</returns>
    /// <remarks>
    /// Repeated callbacks append to this collection's declarations, including declarations added through individual helpers.
    /// Duplicate names are rejected using invariant normalization. Configuration can replace the complete schema list.
    /// The finalized catalog validates the sixteen-dimension bound and the supported interval kinds.
    /// </remarks>
    /// <exception cref="ArgumentException">A declaration has an invalid name, an unsupported scalar type, or a duplicate normalized name.</exception>
    /// <example><code>
    /// collection.Dimensions(d => d
    ///     .Set&lt;string&gt;("tags")
    ///     .Scalar&lt;string&gt;("region")
    ///     .Interval&lt;DateTimeOffset&gt;("coverage"));
    /// </code></example>
    public CacheCollectionBuilder Dimensions(Action<CacheDimensionsBuilder> configure)
    {
        configure?.Invoke(new(this));
        return this;
    }

    /// <summary>Declares a supported same-kind membership set.</summary>
    public CacheCollectionBuilder WithSetDimension<T>(string name, bool allowNull = false) =>
        this.WithDimension(new(name, Kind(typeof(T)), CacheDimensionForm.Set, allowNull));

    /// <summary>Declares a half-open Integer or Timestamp interval.</summary>
    public CacheCollectionBuilder WithIntervalDimension<T>(string name) =>
        this.WithDimension(new(name, Kind(typeof(T)), CacheDimensionForm.Interval));

    /// <summary>Declares one schema entry. Duplicate normalized names fail immediately.</summary>
    public CacheCollectionBuilder WithDimension(CacheDimensionSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var name = CacheIdentityNormalizer.Name(schema.Name);
        if (this.dimensions.Any(d => StringComparer.OrdinalIgnoreCase.Equals(d.Name, name)))
        {
            throw new ArgumentException("A dimension was declared more than once.", nameof(schema));
        }

        this.dimensions.Add(schema with { Name = name });
        return this.With(x => x.Dimensions, this.dimensions.ToArray());
    }

    /// <summary>Explicitly enables hot retention and records only configured child fields.</summary>
    public CacheCollectionBuilder WithHotEntries(Action<CacheHotEntryBuilder> configure)
    {
        var builder = new CacheHotEntryBuilder();
        configure?.Invoke(builder);
        this.With(x => x.HotEntries.Enabled, true);
        this.Patch.Merge("HotEntries", builder.Patch);
        return this;
    }

    /// <summary>Sets neutral collection integration settings; the integration must be registered separately.</summary>
    public CacheCollectionBuilder WithExtension(string integration, string setting, string value)
    {
        this.extensions.TryAdd(CacheIdentityNormalizer.Name(integration), new(StringComparer.OrdinalIgnoreCase));
        this.extensions[CacheIdentityNormalizer.Name(integration)][setting] = value;
        return this.With(x => x.Extensions, this.extensions.ToDictionary(p => p.Key,
            p => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(p.Value, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase));
    }

    private readonly Dictionary<string, Dictionary<string, string>> extensions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Maps one explicitly declared CLR dimension type to its supported portable scalar kind, rejecting unsupported types.</summary>
    public static CacheDimensionKind Kind(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type == typeof(string) ? CacheDimensionKind.String :
            type == typeof(bool) ? CacheDimensionKind.Boolean :
            type == typeof(sbyte) || type == typeof(short) || type == typeof(int) || type == typeof(long) ? CacheDimensionKind.Integer :
            type == typeof(Guid) ? CacheDimensionKind.Identifier :
            type == typeof(DateTimeOffset) ? CacheDimensionKind.Timestamp :
            throw new ArgumentException("Use only String, Boolean, signed Int64 aliases, Guid, or DateTimeOffset dimensions.");
    }
}