// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Collections.Frozen;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Collects trusted code registrations and finalizes presence-aware configuration exactly once.</summary>
/// <example><code>services.AddApplicationCaching(cache => cache.WithCollection&lt;ProductCard&gt;("cards"));</code></example>
public sealed class ApplicationCachingBuilderContext
{
    private readonly List<(IConfiguration Configuration, string Section)> configurationSources = [];
    private readonly CacheApplicationOptionsBuilder global = new();
    private readonly Dictionary<string, (Type Type, bool Binary, CacheCollectionBuilder Builder, Func<string, CacheValueContract> DefaultContract)> collections = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CacheProviderBuilder> providers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Func<IServiceCollection, CacheProviderOptions>> providerDefaults = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> integrationDefaults = new(StringComparer.OrdinalIgnoreCase);
    private CacheCatalog catalog;
    private readonly Lock catalogLock = new();

    /// <summary>Creates a trusted startup registration context over host services and an optional presence-aware configuration overlay.</summary>
    public ApplicationCachingBuilderContext(IServiceCollection services, IConfiguration configuration)
    {
        this.Services = services;
        this.WithConfiguration(configuration);
    }

    /// <summary>Gets the host service collection for optional infrastructure/presentation extensions.</summary>
    public IServiceCollection Services { get; }

    /// <summary>Gets trusted code registrations; configuration never supplies type names or factories.</summary>
    public CacheComponentRegistry Components { get; } = new();

    /// <summary>Gets whether the shared host catalog has been finalized and further registrations are rejected.</summary>
    /// <example><code>var finalized = builder.IsFinalized;</code></example>
    public bool IsFinalized => this.catalog is not null;

    /// <summary>Adds a presence-aware configuration overlay to the shared startup catalog.</summary>
    /// <param name="configuration">An optional module or host configuration source, read at finalization.</param>
    /// <returns>The same startup builder.</returns>
    /// <remarks>Later sources override earlier scalar values and replace complete ordered lists. Collection-specific settings take precedence over all configured defaults.</remarks>
    /// <example><code>builder.WithConfiguration(configuration.GetSection("Modules:Catalog:Caching"));</code></example>
    public ApplicationCachingBuilderContext WithConfiguration(IConfiguration configuration)
    {
        this.EnsureMutable();
        if (configuration is not null)
        {
            this.configurationSources.Add((configuration, null));
        }

        return this;
    }

    /// <summary>Selects a host configuration section to read when the shared catalog is finalized.</summary>
    /// <param name="sectionName">A colon-separated or dotted configuration path.</param>
    /// <returns>The same startup builder.</returns>
    /// <example><code>builder.WithConfigurationSection("Modules:Catalog:Caching");</code></example>
    public ApplicationCachingBuilderContext WithConfigurationSection(string sectionName)
    {
        this.EnsureMutable();
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);
        var path = sectionName.Replace('.', ':');
        if (!this.configurationSources.Any(source => StringComparer.OrdinalIgnoreCase.Equals(source.Section, path)))
        {
            this.configurationSources.Add((null, path));
        }

        return this;
    }

    /// <summary>Sets the host-wide cache isolation namespace.</summary>
    /// <example><code>services.AddApplicationCaching(cache => cache.Namespace("catalog"));</code></example>
    public ApplicationCachingBuilderContext Namespace(string value) => this.WithOptions(options => options.Namespace(value));

    /// <summary>Configures bounded diagnostic observations and optional history.</summary>
    /// <example><code>cache.Diagnostics(diagnostics => diagnostics.HistoryRetention(TimeSpan.FromHours(1)));</code></example>
    public ApplicationCachingBuilderContext Diagnostics(Action<CacheDiagnosticsBuilder> configure) => this.WithOptions(options => options.Diagnostics(configure));

    /// <summary>Configures finite process-wide admission and memory budgets.</summary>
    /// <example><code>cache.Limits(limits => limits.MaxActiveSources(16));</code></example>
    public ApplicationCachingBuilderContext Limits(Action<CacheProcessLimitsBuilder> configure) => this.WithOptions(options => options.Limits(configure));

    /// <summary>Enables or disables advanced cache storage for this host.</summary>
    /// <example><code>cache.Enabled(false);</code></example>
    public ApplicationCachingBuilderContext Enabled(bool enabled = true) => this.WithOptions(options => options.Enabled(enabled));

    /// <summary>Configures host-wide collection defaults below named collection policies.</summary>
    /// <param name="configure">Records explicit default fields without replacing previously configured siblings.</param>
    /// <returns>The same startup builder.</returns>
    /// <exception cref="InvalidOperationException">The shared startup catalog has already been finalized.</exception>
    /// <example><code>cache.Defaults(collection => collection.ExpireAfter(TimeSpan.FromMinutes(10)));</code></example>
    public ApplicationCachingBuilderContext Defaults(Action<CacheCollectionBuilder> configure)
    {
        this.EnsureMutable();
        var builder = new CacheCollectionBuilder();
        builder.Patch.Merge(null, this.global.Patch.Extract("Defaults"));
        configure?.Invoke(builder);
        this.global.Patch.Merge("Defaults", builder.Patch);
        return this;
    }

    /// <summary>Configures cache-owned worker schedules without activating collection refresh or warm-up.</summary>
    /// <example><code>cache.BackgroundServices(background => background.StartupDelay(TimeSpan.Zero));</code></example>
    public ApplicationCachingBuilderContext BackgroundServices(Action<CacheBackgroundServiceBuilder> configure) => this.WithOptions(options => options.BackgroundServices(configure));

    /// <summary>Configures the shared resident sub-budget for explicitly enabled hot entries.</summary>
    /// <example><code>cache.HotEntryBudget(hot => hot.MaxResidentBytes(ByteSize.Megabytes(32)));</code></example>
    public ApplicationCachingBuilderContext HotEntryBudget(Action<CacheHotEntryBudgetBuilder> configure) => this.WithOptions(options => options.HotEntryBudget(configure));

    /// <summary>Records explicit global process policies.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Advanced)]
    public ApplicationCachingBuilderContext WithOptions(Action<CacheApplicationOptionsBuilder> configure)
    {
        this.EnsureMutable();
        configure?.Invoke(this.global);
        return this;
    }

    /// <summary>Registers one trusted typed contract alias and JSON profile, independently of collection declaration order.</summary>
    /// <typeparam name="T">The one CLR value type represented by this contract.</typeparam>
    /// <param name="alias">The unique stable contract alias selected by collections.</param>
    /// <param name="configure">Optional codec, JSON convention, and decoded-size declarations.</param>
    /// <returns>The same startup builder.</returns>
    /// <remarks>Ordinary JSON collections need no explicit contract. Register one to customize serialization, approve immutable sharing, or share an explicit contract across collections.</remarks>
    /// <exception cref="ArgumentException">The alias is invalid or already registered.</exception>
    /// <exception cref="InvalidOperationException">The shared startup catalog has already been finalized.</exception>
    /// <example><code>cache.WithValueContract&lt;ProductCard&gt;("cards", contract => contract.WithJsonOptions(System.Text.Json.JsonSerializerOptions.Web));</code></example>
    public ApplicationCachingBuilderContext WithValueContract<T>(string alias, Action<CacheValueContractBuilder<T>> configure = null)
    {
        this.EnsureMutable();
        var builder = new CacheValueContractBuilder<T>();
        configure?.Invoke(builder);
        if (!this.Components.Contracts.TryAdd(CacheIdentityNormalizer.Name(alias), builder.Build(alias)))
        {
            throw new ArgumentException("A value-contract alias was registered twice.", nameof(alias));
        }

        return this;
    }

    /// <summary>Registers a declared raw binary format with optional bounded single-pass validator.</summary>
    public ApplicationCachingBuilderContext WithBinaryContract(string alias, string formatId = "binary", int formatVersion = 1,
        Func<Stream, CancellationToken, ValueTask<Result>> validator = null)
    {
        this.EnsureMutable();
        if (!this.Components.Contracts.TryAdd(CacheIdentityNormalizer.Name(alias), new CacheBinaryValueContract(alias, formatId, formatVersion, validator)))
        {
            throw new ArgumentException("A value-contract alias was registered twice.", nameof(alias));
        }

        return this;
    }

    /// <summary>Declares a typed collection with a default JSON contract, or infers one uniquely matching registered custom contract.</summary>
    /// <typeparam name="T">The collection's one CLR value type.</typeparam>
    /// <param name="name">The globally unique collection name within the host, compared without case.</param>
    /// <param name="configure">Optional explicit collection policies below configuration overrides.</param>
    /// <returns>The same startup builder.</returns>
    /// <remarks>The default contract uses the collection name as its stable alias. Multiple matching custom contracts require UseContract.</remarks>
    /// <example><code>cache.WithCollection&lt;ProductCard&gt;("cards", collection => collection.ExpireAfter(TimeSpan.FromMinutes(5)));</code></example>
    public ApplicationCachingBuilderContext WithCollection<T>(string name, Action<CacheCollectionBuilder> configure = null) =>
        this.AddCollection(name, typeof(T), false, configure, alias => new CacheValueContractBuilder<T>().Build(alias));

    /// <summary>Declares a raw binary startup collection.</summary>
    public ApplicationCachingBuilderContext WithBinaryCollection(string name, Action<CacheCollectionBuilder> configure = null) =>
        this.AddCollection(name, typeof(Stream), true, configure, alias => new CacheBinaryValueContract(alias, "binary", 1, null));

    /// <summary>Registers a trusted complete provider kind and truthful capabilities.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Advanced)]
    public ApplicationCachingBuilderContext WithProviderKind(string alias, CacheProviderCapabilities capabilities,
        Func<IServiceProvider, string, CacheProviderOptions, ICacheStoreProvider> factory)
    {
        this.EnsureMutable();
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(factory);
        if (!this.Components.Kinds.TryAdd(CacheIdentityNormalizer.Name(alias), new(alias, capabilities, factory)))
        {
            throw new ArgumentException("A provider kind was registered twice.", nameof(alias));
        }

        return this;
    }

    /// <summary>Registers a trusted complete provider kind whose truthful capabilities depend on finalized engine/access settings.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Advanced)]
    public ApplicationCachingBuilderContext WithProviderKind(string alias, Func<CacheProviderOptions, CacheProviderCapabilities> capabilities,
        Func<IServiceProvider, string, CacheProviderOptions, ICacheStoreProvider> factory)
    {
        this.EnsureMutable();
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(factory);
        if (!this.Components.Kinds.TryAdd(CacheIdentityNormalizer.Name(alias), new(alias, null, factory, capabilities)))
        {
            throw new ArgumentException("A provider kind was registered twice.", nameof(alias));
        }

        return this;
    }

    /// <summary>Declares one complete named provider. Configuration can override explicit native settings.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Advanced)]
    public ApplicationCachingBuilderContext WithProvider(string name, Action<CacheProviderBuilder> configure)
        => this.WithProvider(name, configure, null);

    /// <summary>Declares a named provider with native defaults resolved from trusted registration metadata after module composition.</summary>
    /// <param name="name">The unique provider alias selected by collections.</param>
    /// <param name="configure">Explicit fluent settings above native defaults.</param>
    /// <param name="defaults">A code-owned metadata-only defaults factory, below fluent and configuration settings.</param>
    /// <returns>The same startup builder.</returns>
    /// <example><code>builder.WithProvider("custom", configure, services => nativeDefaults);</code></example>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Advanced)]
    public ApplicationCachingBuilderContext WithProvider(string name, Action<CacheProviderBuilder> configure,
        Func<IServiceCollection, CacheProviderOptions> defaults)
    {
        this.EnsureMutable();
        var builder = new CacheProviderBuilder();
        configure?.Invoke(builder);
        if (!this.providers.TryAdd(CacheIdentityNormalizer.Name(name), builder))
        {
            throw new ArgumentException("A named provider was registered twice.", nameof(name));
        }

        if (defaults is not null) { this.providerDefaults.Add(CacheIdentityNormalizer.Name(name), defaults); }

        return this;
    }

    /// <summary>Registers a trusted scoped typed refresh/warmup source alias.</summary>
    public ApplicationCachingBuilderContext WithRefreshLoader<T, TLoader>(string alias, string databaseReadyAlias = null) where TLoader : class, ICacheRefreshLoader<T>
    {
        this.EnsureMutable();
        this.Services.TryAddScoped<TLoader>();
        this.AddLoader(new(CacheIdentityNormalizer.Name(alias), typeof(T), false, sp => sp.GetRequiredService<TLoader>(),
            async (cache, binding, item, refresh, version, token) =>
            {
                var collection = cache.GetCollection<T>(binding.Name);
                CacheLoadDelegate<T> loader = (context, ct) => context.Services.GetRequiredService<TLoader>().LoadAsync(context, ct);
                var options = new CacheAccessOptions(Background: true, ExpectedVersion: version);
                var loaded = refresh ? await collection.RefreshAsync(item, loader, options, token).ConfigureAwait(false) :
                    await collection.GetOrLoadAsync(item, loader, options, token).ConfigureAwait(false);
                return loaded.IsSuccess ? Result<CacheBackgroundLoadOutcome>.Success(new(loaded.Value.StorageOutcome, loaded.Value.Version, loaded.Value.IsNotFound)) :
                    Result<CacheBackgroundLoadOutcome>.Failure(loaded);
            }, databaseReadyAlias));
        return this;
    }

    /// <summary>Registers a trusted scoped binary refresh/warmup source alias.</summary>
    public ApplicationCachingBuilderContext WithBinaryRefreshLoader<TLoader>(string alias, string databaseReadyAlias = null) where TLoader : class, ICacheBinaryRefreshLoader
    {
        this.EnsureMutable();
        this.Services.TryAddScoped<TLoader>();
        this.AddLoader(new(CacheIdentityNormalizer.Name(alias), typeof(Stream), true, sp => sp.GetRequiredService<TLoader>(),
            async (cache, binding, item, refresh, version, token) =>
            {
                var collection = cache.GetBinaryCollection(binding.Name);
                CacheBinaryLoadDelegate loader = (context, stream, ct) => context.Services.GetRequiredService<TLoader>().LoadAsync(context, stream, ct);
                var options = new CacheAccessOptions(Background: true, ExpectedVersion: version);
                var loaded = refresh ? await collection.RefreshAsync(item, loader, options, token).ConfigureAwait(false) :
                    await collection.GetOrLoadAsync(item, loader, options, token).ConfigureAwait(false);
                if (loaded.IsFailure) { return Result<CacheBackgroundLoadOutcome>.Failure(loaded); }

                if (loaded.Value.Value is not null) { await loaded.Value.Value.DisposeAsync().ConfigureAwait(false); }

                return Result<CacheBackgroundLoadOutcome>.Success(new(loaded.Value.StorageOutcome, loaded.Value.Version, loaded.Value.IsNotFound));
            }, databaseReadyAlias));
        return this;
    }

    /// <summary>Registers a trusted scoped asynchronous warm-up key source; configuration selects only its stable alias.</summary>
    /// <typeparam name="TSource">The code-approved repeatable key source.</typeparam>
    /// <param name="alias">The bounded stable key-source alias.</param>
    /// <returns>The same startup builder.</returns>
    /// <example><code>builder.WithWarmupSource&lt;CatalogWarmupSource&gt;("catalog-seeds");</code></example>
    public ApplicationCachingBuilderContext WithWarmupSource<TSource>(string alias) where TSource : class, ICacheWarmupSource
    {
        this.EnsureMutable();
        this.Services.TryAddScoped<TSource>();
        if (this.Components.WarmupSources.Count >= 64 || !this.Components.WarmupSources.TryAdd(CacheIdentityNormalizer.Name(alias), sp => sp.GetRequiredService<TSource>()))
        {
            throw new ArgumentException("Warm-up source aliases must be unique within the bounded startup registry.", nameof(alias));
        }

        return this;
    }

    /// <summary>Registers a trusted optional behavior alias. Stage interface compatibility is validated when chains are finalized.</summary>
    public ApplicationCachingBuilderContext WithBehavior<TBehavior>(string alias, CacheBehaviorStage stage) where TBehavior : class
    {
        this.EnsureMutable();
        this.Services.TryAddTransient<TBehavior>();
        if (!this.Components.Behaviors.TryAdd((stage, CacheIdentityNormalizer.Name(alias)), sp => sp.GetRequiredService<TBehavior>()))
        {
            throw new ArgumentException("A behavior alias was registered twice for this stage.", nameof(alias));
        }

        return this;
    }

    /// <summary>Registers one trusted persisted transform/reader. Configuration cannot activate types from stored input.</summary>
    /// <example><code>caching.WithPayloadTransform("signature", services => new SignatureCacheTransform());</code></example>
    public ApplicationCachingBuilderContext WithPayloadTransform(string alias, Func<IServiceProvider, ICachePayloadTransform> factory)
    {
        this.EnsureMutable();
        ArgumentNullException.ThrowIfNull(factory);
        if (this.Components.PayloadTransforms.Count >= 64 || !this.Components.PayloadTransforms.TryAdd(CacheIdentityNormalizer.Name(alias), factory))
        {
            throw new ArgumentException("A payload transform alias must be unique within the bounded startup registry.", nameof(alias));
        }

        return this;
    }

    /// <summary>Registers a shared streaming transform through the cache's bounded trusted-transform registry.</summary>
    /// <param name="alias">The unique cache configuration alias for this trusted implementation.</param>
    /// <param name="factory">The code-owned factory for a shared streaming transform; existing cache implementations are used directly.</param>
    /// <returns>This builder for subsequent trusted registrations.</returns>
    /// <remarks>The cache approves formats and parameters, reserves workspace, and owns its persisted envelopes independently of the shared transform.</remarks>
    /// <example><code>Func&lt;IServiceProvider, IStreamingPayloadTransform&gt; factory = services => new SignatureTransform();
    /// caching.WithPayloadTransform("signature", factory);</code></example>
    public ApplicationCachingBuilderContext WithPayloadTransform(string alias, Func<IServiceProvider, IStreamingPayloadTransform> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        Func<IServiceProvider, ICachePayloadTransform> adapted = services =>
        {
            var transform = factory(services);
            return transform is null ? null : transform as ICachePayloadTransform ?? new CachePayloadTransformAdapter(transform);
        };
        return this.WithPayloadTransform(alias, adapted);
    }

    /// <summary>Registers an existing sequential source resilience pipeline shared by a trusted dependency alias.</summary>
    public ApplicationCachingBuilderContext WithSourceResilience(string alias, Func<IServiceProvider, Polly.ResiliencePipeline> factory)
    {
        this.EnsureMutable();
        ArgumentNullException.ThrowIfNull(factory);
        if (!this.Components.SourceResilience.TryAdd(CacheIdentityNormalizer.Name(alias), services => factory(services)))
        {
            throw new ArgumentException("A source dependency policy alias was registered twice.", nameof(alias));
        }

        return this;
    }

    /// <summary>Registers an optional integration capability and neutral code defaults. Each presentation transport opts in independently.</summary>
    public ApplicationCachingBuilderContext WithIntegration(string alias, IReadOnlyDictionary<string, string> defaults)
    {
        this.EnsureMutable();
        alias = CacheIdentityNormalizer.Name(alias);
        if (!this.Components.Integrations.Add(alias))
        {
            throw new ArgumentException("An integration capability was registered twice.", nameof(alias));
        }

        this.integrationDefaults[alias] = new(defaults ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
        return this;
    }

    /// <summary>Finalizes and validates an immutable catalog once. No store, loader, codec, or runtime CLR activation occurs here.</summary>
    public CacheCatalog BuildCatalog()
        => this.BuildCatalog(null);

    /// <summary>Finalizes the catalog with the host configuration used by conventional section registrations.</summary>
    /// <param name="hostConfiguration">The host root configuration, or null for code-only composition.</param>
    /// <returns>The immutable catalog for this host lifetime.</returns>
    /// <example><code>var catalog = builder.BuildCatalog(configuration);</code></example>
    public CacheCatalog BuildCatalog(IConfiguration hostConfiguration)
    {
        lock (this.catalogLock)
        {
            hostConfiguration ??= this.Services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(IConfiguration) && !descriptor.IsKeyedService)
                ?.ImplementationInstance as IConfiguration;
            var configurations = this.configurationSources.Select(source => source.Configuration ?? hostConfiguration?.GetSection(source.Section))
                .Where(configuration => configuration is not null).ToArray();
            return this.BuildCatalogCore(configurations);
        }
    }

    private CacheCatalog BuildCatalogCore(IReadOnlyList<IConfiguration> configurations)
    {
        if (this.catalog is not null)
        {
            return this.catalog;
        }

        var rootKeys = new HashSet<string>(["Enabled", "Namespace", "Limits", "Defaults", "BackgroundServices", "HotEntryBudget", "Diagnostics", "Collections", "Providers"],
            StringComparer.OrdinalIgnoreCase);
        foreach (var configuration in configurations)
        {
            foreach (var child in configuration.GetChildren())
            {
                if (!rootKeys.Contains(child.Key) && !this.Components.Integrations.Contains(CacheIdentityNormalizer.Name(child.Key)))
                {
                    throw new ArgumentException($"Unknown or unavailable cache configuration capability '{child.Key}'.");
                }
            }
        }

        var applicationProvenance = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var options = this.global.Patch.Apply(new ApplicationCachingOptions(), "Fluent", applicationProvenance);
        foreach (var configuration in configurations)
        {
            options = OptionsConfigurationOverlay.Apply(options, Filter(configuration, c => rootKeys.Contains(c.Key) &&
                !c.Key.Equals("Collections", StringComparison.OrdinalIgnoreCase) && !c.Key.Equals("Providers", StringComparison.OrdinalIgnoreCase)),
                applicationProvenance);
        }

        options = options with
        {
            Diagnostics = options.Diagnostics with
            {
                ChartWindow = applicationProvenance.ContainsKey("Diagnostics.ChartWindow") ? options.Diagnostics.ChartWindow :
                    TimeSpan.FromTicks(Math.Min(options.Diagnostics.ChartWindow.Ticks, options.Diagnostics.HistoryRetention.Ticks)),
                SamplingInterval = applicationProvenance.ContainsKey("Diagnostics.SamplingInterval") ? options.Diagnostics.SamplingInterval :
                    TimeSpan.FromTicks(Math.Min(options.Diagnostics.SamplingInterval.Ticks, options.Diagnostics.HistoryRetention.Ticks))
            },
            HotEntryBudget = applicationProvenance.ContainsKey("HotEntryBudget.MaxResidentBytes") ? options.HotEntryBudget :
                options.HotEntryBudget with { MaxResidentBytes = Math.Min(options.HotEntryBudget.MaxResidentBytes, options.Limits.MaxResidentBytes) }
        };

        var nativeProviders = this.BuildProviders(configurations);
        var names = this.collections.Keys.Concat(configurations.SelectMany(c => c.GetSection("Collections").GetChildren())
            .Select(c => CacheIdentityNormalizer.Name(c.Key))).Distinct(StringComparer.OrdinalIgnoreCase);
        var impliedContracts = new Dictionary<string, CacheValueContract>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, declaration) in this.collections)
        {
            var sections = configurations.SelectMany(configuration => configuration.GetSection("Collections").GetChildren())
                .Where(section => StringComparer.OrdinalIgnoreCase.Equals(section.Key, name)).ToArray();
            var selected = this.MergeCollection(new(), declaration.Builder, sections, configurations, new Dictionary<string, string>());
            var useDefault = selected.Contract is null ?
                !this.Components.Contracts.Values.Any(contract => contract.ValueType == declaration.Type && contract.IsBinary == declaration.Binary) :
                StringComparer.OrdinalIgnoreCase.Equals(selected.Contract, name);
            if (useDefault && !this.Components.Contracts.ContainsKey(name))
            {
                impliedContracts.Add(name, declaration.DefaultContract(name));
            }
        }

        var bindings = new List<CacheCollectionBinding>();
        foreach (var name in names)
        {
            this.collections.TryGetValue(name, out var declaration);
            var sections = configurations.SelectMany(c => c.GetSection("Collections").GetChildren())
                .Where(c => StringComparer.OrdinalIgnoreCase.Equals(c.Key, name)).ToArray();
            var provenance = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var effective = this.MergeCollection(new(), declaration.Builder, sections, configurations, provenance);
            var providerName = CacheIdentityNormalizer.Name(effective.Provider);
            if (!nativeProviders.TryGetValue(providerName, out var provider))
            {
                throw new ArgumentException($"Unknown cache provider '{providerName}'.");
            }

            if (provider.Capabilities.Persistent)
            {
                effective = this.MergeCollection(new() { Limits = new() { MaxStorageBytes = 10 * CacheDefaults.GiB, MaxEntries = 1000000,
                    MaxEncodedBytes = 256 * CacheDefaults.MiB, MaxLogicalBytes = 256 * CacheDefaults.MiB } }, declaration.Builder, sections, configurations, provenance);
            }

            effective = FitCollectionDefaults(effective, provider.NativeOptions, options, provenance);

            var contractAlias = effective.Contract;
            if (contractAlias is null && declaration.Type is not null)
            {
                var matching = this.Components.Contracts.Values.Where(c => c.ValueType == declaration.Type && c.IsBinary == declaration.Binary).ToArray();
                if (matching.Length == 1) { contractAlias = matching[0].Alias; }
                else if (matching.Length == 0 && impliedContracts.TryGetValue(name, out var implied))
                {
                    contractAlias = implied.Alias;
                }
            }

            CacheValueContract contract = null;
            if (contractAlias is null || (!this.Components.Contracts.TryGetValue(CacheIdentityNormalizer.Name(contractAlias), out contract) &&
                !impliedContracts.TryGetValue(CacheIdentityNormalizer.Name(contractAlias), out contract)))
            {
                throw new ArgumentException($"Collection '{name}' needs one registered value-contract alias.");
            }

            if (declaration.Type is not null && (contract.ValueType != declaration.Type || contract.IsBinary != declaration.Binary))
            {
                throw new ArgumentException($"Collection '{name}' selects an incompatible registered contract.");
            }

            if (contract.IsBinary)
            {
                if (!StringComparer.OrdinalIgnoreCase.Equals(effective.Codec, "json") &&
                    !StringComparer.OrdinalIgnoreCase.Equals(effective.Codec, "binary"))
                {
                    throw new ArgumentException("A raw binary contract cannot select an object codec profile.");
                }

                effective = effective with { Codec = "binary" };
            }

            if (effective.Compression.Enabled && effective.Behaviors.PayloadTransformation is { } payloads &&
                !payloads.Contains("compression", StringComparer.OrdinalIgnoreCase))
            {
                effective = effective with { Behaviors = effective.Behaviors with { PayloadTransformation = [.. payloads, "COMPRESSION"] } };
                provenance["Behaviors.PayloadTransformation"] = "RequiredByCompression";
            }

            effective = Freeze(effective with { Contract = contract.Alias, Provider = providerName });
            bindings.Add(new(name, effective, contract, provider, provenance.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase)));
        }

        var integrations = this.integrationDefaults.ToDictionary(p => p.Key,
            p => configurations.SelectMany(c => c.GetChildren()).Where(c => StringComparer.OrdinalIgnoreCase.Equals(c.Key, p.Key))
                .Aggregate((IReadOnlyDictionary<string, string>)p.Value.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase), OverlayIntegration),
            StringComparer.OrdinalIgnoreCase).ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        var result = new CacheCatalog(options with { Defaults = Freeze(options.Defaults) }, bindings, nativeProviders.Values,
            this.Components.Freeze(impliedContracts.Values), integrations);
        CacheCatalogValidator.Validate(result);
        this.catalog = result;
        return result;
    }

    private Dictionary<string, CacheProviderBinding> BuildProviders(IReadOnlyList<IConfiguration> configurations)
    {
        var bindings = new Dictionary<string, CacheProviderBinding>(StringComparer.OrdinalIgnoreCase);
        var configured = configurations.SelectMany(c => c.GetSection("Providers").GetChildren()).ToArray();
        var names = this.providers.Keys.Concat(configured.Select(c => CacheIdentityNormalizer.Name(c.Key)))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (names.Length == 0)
        {
            names = ["MEMORY"];
        }

        foreach (var name in names)
        {
            var options = this.providerDefaults.TryGetValue(name, out var defaults) ? defaults(this.Services) :
                this.providers.Count == 0 && configured.Length == 0 ? new CacheProviderOptions { MaxStorageBytes = 128 * CacheDefaults.MiB, MaxEntries = 100000 } : new CacheProviderOptions();
            var provenance = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (this.providers.TryGetValue(name, out var builder))
            {
                options = builder.Patch.Apply(options, "Fluent", provenance);
            }

            foreach (var section in configured.Where(c => StringComparer.OrdinalIgnoreCase.Equals(c.Key, name)))
            {
                options = OptionsConfigurationOverlay.Apply(options, section, provenance);
            }

            if (StringComparer.OrdinalIgnoreCase.Equals(options.Kind, "InMemory"))
            {
                var memoryDefaults = this.providerDefaults.ContainsKey(name);
                options = options with
                {
                    MaxStorageBytes = provenance.ContainsKey(nameof(CacheProviderOptions.MaxStorageBytes)) ? options.MaxStorageBytes :
                        Math.Min(options.MaxStorageBytes, memoryDefaults ? options.MaxResidentBytes : Math.Min(128 * CacheDefaults.MiB, options.MaxResidentBytes)),
                    MaxEntries = provenance.ContainsKey(nameof(CacheProviderOptions.MaxEntries)) || memoryDefaults ? options.MaxEntries : 100000
                };
            }

            if (!provenance.ContainsKey(nameof(CacheProviderOptions.MetadataCacheBytes)))
            {
                options = options with { MetadataCacheBytes = Math.Min(options.MetadataCacheBytes, Math.Max(1, options.MaxResidentBytes / 8)) };
            }

            if (!this.Components.Kinds.TryGetValue(CacheIdentityNormalizer.Name(options.Kind), out var kind))
            {
                throw new ArgumentException($"Unknown or unavailable complete provider kind '{options.Kind}'.");
            }

            bindings.Add(name, new(name, options, kind));
        }

        return bindings;
    }

    private static CacheCollectionOptions FitCollectionDefaults(CacheCollectionOptions options, CacheProviderOptions provider,
        ApplicationCachingOptions application, IReadOnlyDictionary<string, string> provenance)
    {
        var resident = Inherit(options.Limits.MaxResidentBytes, Math.Min(provider.MaxResidentBytes, application.Limits.MaxResidentBytes), "Limits.MaxResidentBytes");
        var storage = Inherit(options.Limits.MaxStorageBytes, provider.MaxStorageBytes, "Limits.MaxStorageBytes");
        var hotResident = Inherit(options.HotEntries.MaxResidentBytes, resident, "HotEntries.MaxResidentBytes");
        return options with
        {
            Limits = options.Limits with
            {
                MaxResidentBytes = resident,
                MaxStorageBytes = storage,
                MaxEntries = Inherit(options.Limits.MaxEntries, provider.MaxEntries, "Limits.MaxEntries"),
                MaxEncodedBytes = Inherit(options.Limits.MaxEncodedBytes, storage, "Limits.MaxEncodedBytes")
            },
            HotEntries = options.HotEntries with
            {
                MaxResidentBytes = hotResident,
                MaxEntryBytes = Inherit(options.HotEntries.MaxEntryBytes, hotResident, "HotEntries.MaxEntryBytes")
            },
            Streaming = options.Streaming with
            {
                BufferSize = (int)Inherit(options.Streaming.BufferSize, application.Limits.MaxTransientBytes, "Streaming.BufferSize")
            }
        };

        long Inherit(long value, long parent, string path) => provenance.ContainsKey(path) ? value : Math.Min(value, parent);
    }

    private CacheCollectionOptions MergeCollection(CacheCollectionOptions options, CacheCollectionBuilder builder,
        IReadOnlyList<IConfigurationSection> sections, IReadOnlyList<IConfiguration> configurations, Dictionary<string, string> provenance)
    {
        this.global.Patch.Extract("Defaults").Apply(options, "GlobalFluent", provenance);
        var globalBehaviors = options.Behaviors with { };
        builder?.Patch.Apply(options, "CollectionFluent", provenance);
        if (builder is not null)
        {
            var behavior = options.Behaviors;
            options = options with
            {
                Behaviors = behavior with
                {
                    CollectionOperations = builder.Patch.Contains("Behaviors.CollectionOperations") ?
                        CombineFluentBehaviors(globalBehaviors.CollectionOperations, behavior.CollectionOperations) : behavior.CollectionOperations,
                    SourceExecution = builder.Patch.Contains("Behaviors.SourceExecution") ?
                        CombineFluentBehaviors(globalBehaviors.SourceExecution, behavior.SourceExecution) : behavior.SourceExecution,
                    PayloadTransformation = builder.Patch.Contains("Behaviors.PayloadTransformation") ?
                        CombineFluentBehaviors(globalBehaviors.PayloadTransformation, behavior.PayloadTransformation) : behavior.PayloadTransformation
                }
            };
        }

        foreach (var configuration in configurations)
        {
            options = OptionsConfigurationOverlay.Apply(options, configuration.GetSection("Defaults"), provenance);
        }

        foreach (var section in sections)
        {
            options = OptionsConfigurationOverlay.Apply(options, Filter(section, c => !this.Components.Integrations.Contains(CacheIdentityNormalizer.Name(c.Key))), provenance);
            var extensions = options.Extensions.ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
            foreach (var integration in section.GetChildren().Where(c => this.Components.Integrations.Contains(CacheIdentityNormalizer.Name(c.Key))))
            {
                extensions.TryGetValue(integration.Key, out var existing);
                extensions[integration.Key] = OverlayIntegration(existing ?? new Dictionary<string, string>(), integration);
                provenance["Extensions." + integration.Key] = "Configuration";
            }

            options = options with { Extensions = extensions };
        }

        return options;
    }

    private static IReadOnlyList<string> CombineFluentBehaviors(IReadOnlyList<string> global, IReadOnlyList<string> collection)
    {
        if (global is null || collection is null)
        {
            return null; // Final validation applies after any explicit configured replacement.
        }

        var comparer = StringComparer.OrdinalIgnoreCase;
        // Re-selecting the same globally registered alias specializes one behavior. Duplicates
        // inside either explicit list remain invalid rather than being silently repaired.
        return global.Distinct(comparer).Count() != global.Count || collection.Distinct(comparer).Count() != collection.Count ?
            global.Concat(collection).ToArray() : global.Concat(collection.Where(alias => !global.Contains(alias, comparer))).ToArray();
    }

    private ApplicationCachingBuilderContext AddCollection(string name, Type type, bool binary, Action<CacheCollectionBuilder> configure,
        Func<string, CacheValueContract> defaultContract)
    {
        this.EnsureMutable();
        var builder = new CacheCollectionBuilder();
        configure?.Invoke(builder);
        if (!this.collections.TryAdd(CacheIdentityNormalizer.Name(name), (type, binary, builder, defaultContract)))
        {
            throw new ArgumentException("A collection name was declared twice.", nameof(name));
        }

        return this;
    }

    private void AddLoader(CacheLoaderRegistration loader)
    {
        if (!this.Components.Loaders.TryAdd(loader.Alias, loader))
        {
            throw new ArgumentException("A loader alias was registered twice.");
        }
    }

    private void EnsureMutable()
    {
        if (this.catalog is not null)
        {
            throw new InvalidOperationException("The cache catalog is finalized for this host lifetime.");
        }
    }

    private static CacheCollectionOptions Freeze(CacheCollectionOptions options) => options with
    {
        Dimensions = Array.AsReadOnly(options.Dimensions.ToArray()),
        Behaviors = options.Behaviors with
        {
            CollectionOperations = Array.AsReadOnly(options.Behaviors.CollectionOperations.ToArray()),
            SourceExecution = Array.AsReadOnly(options.Behaviors.SourceExecution.ToArray()),
            PayloadTransformation = Array.AsReadOnly(options.Behaviors.PayloadTransformation.ToArray())
        },
        Warmup = options.Warmup with { Items = Array.AsReadOnly(options.Warmup.Items.ToArray()) },
        Extensions = options.Extensions.ToFrozenDictionary(p => CacheIdentityNormalizer.Name(p.Key),
            p => (IReadOnlyDictionary<string, string>)p.Value.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase)
    };

    private static IReadOnlyDictionary<string, string> OverlayIntegration(IReadOnlyDictionary<string, string> defaults, IConfigurationSection configuration)
    {
        var result = new Dictionary<string, string>(defaults, StringComparer.OrdinalIgnoreCase);
        if (configuration is not null)
        {
            foreach (var (path, value) in configuration.GetChildren().SelectMany(c => Flatten(c, c.Key)))
            {
                result[path] = value;
            }
        }

        return result.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static IConfiguration Filter(IConfiguration configuration, Func<IConfigurationSection, bool> include) =>
        new ConfigurationBuilder().AddInMemoryCollection(configuration.GetChildren().Where(include).SelectMany(c => Flatten(c, c.Key))).Build();

    private static IEnumerable<KeyValuePair<string, string>> Flatten(IConfigurationSection section, string path)
    {
        yield return new(path, section.Value);
        foreach (var child in section.GetChildren())
        {
            foreach (var item in Flatten(child, path.Length == 0 ? child.Key : path + ":" + child.Key))
            {
                yield return item;
            }
        }
    }
}