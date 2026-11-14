// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Collections.Concurrent;

/// <summary>Owns advanced-cache process resource ceilings. Provider and collection children share these exact parents.</summary>
/// <example><code>var resources = services.GetRequiredService&lt;CacheRuntimeResources&gt;();</code></example>
public sealed class CacheRuntimeResources
{
    private readonly CacheCatalog catalog;
    private readonly ConcurrentDictionary<string, ResourceBudget> providerResident = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CacheCollectionResources> collections = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates all finite process parents without allocating their advertised capacity.</summary>
    public CacheRuntimeResources(CacheCatalog catalog, TimeProvider timeProvider)
    {
        this.catalog = catalog;
        var limits = catalog.Options.Limits;
        this.Resident = new("resident", limits.MaxResidentBytes);
        this.Transient = new("transient", limits.MaxTransientBytes);
        this.HotResident = new("hot", catalog.Options.HotEntryBudget.MaxResidentBytes);
        this.Sources = new("sources", limits.MaxActiveSources, limits.MaxQueuedSources, timeProvider: timeProvider);
        this.ProviderIO = new("provider-io", limits.MaxActiveProviderOperations, limits.MaxQueuedProviderOperations, timeProvider: timeProvider);
        this.MetadataQueries = new("metadata-queries", limits.MaxMetadataQueries, limits.MaxQueuedProviderOperations, timeProvider: timeProvider);
        this.Transfers = new("transfers", limits.MaxTransfers, limits.MaxQueuedProviderOperations, timeProvider: timeProvider);
        this.DecodePromotions = new("decode-promotions", limits.MaxDecodePromotions, limits.MaxQueuedProviderOperations, timeProvider: timeProvider);
        this.RefreshSources = new("refresh-sources", limits.MaxRefreshSources, limits.MaxQueuedSources, timeProvider: timeProvider);
    }

    /// <summary>Gets the process resident capacity parent.</summary>
    public ResourceBudget Resident { get; }

    /// <summary>Gets transient reservations before allocation.</summary>
    public ResourceBudget Transient { get; }

    /// <summary>Gets the hot sublimit. Representations also retain their ordinary resident charge.</summary>
    public ResourceBudget HotResident { get; }

    /// <summary>Gets source execution admission.</summary>
    public CacheAdmission Sources { get; }

    /// <summary>Gets occupied provider I/O admission, excluding idle source/remote observation waits.</summary>
    public CacheAdmission ProviderIO { get; }

    /// <summary>Gets metadata query admission.</summary>
    public CacheAdmission MetadataQueries { get; }

    /// <summary>Gets occupied payload transfer admission.</summary>
    public CacheAdmission Transfers { get; }

    /// <summary>Gets typed decoding/promotion admission.</summary>
    public CacheAdmission DecodePromotions { get; }

    /// <summary>Gets background source sub-admission.</summary>
    public CacheAdmission RefreshSources { get; }

    /// <summary>Gets a declared provider's resident child, including native metadata and associated local retained representations.</summary>
    public ResourceBudget ForProvider(string name) => this.providerResident.GetOrAdd(CacheIdentityNormalizer.Name(name),
        n => new(n, this.catalog.Providers[n].Options.MaxResidentBytes, this.Resident));

    /// <summary>Gets predeclared collection children. Unknown names cannot create runtime collections.</summary>
    public CacheCollectionResources ForCollection(string name)
    {
        name = CacheIdentityNormalizer.Name(name);
        if (!this.catalog.Collections.ContainsKey(name))
        {
            throw new ArgumentException("The collection was not declared at startup.", nameof(name));
        }

        return this.collections.GetOrAdd(name, n => new(this.catalog.Collections[n], this));
    }
}

/// <summary>Owns finite collection children within their process/provider parents.</summary>
/// <example><code>var admission = resources.ForCollection("cards").Sources;</code></example>
public sealed class CacheCollectionResources
{
    /// <summary>Creates this declared collection&apos;s child memory and admission accounts within the exact shared process/provider parents.</summary>
    public CacheCollectionResources(CacheCollectionBinding binding, CacheRuntimeResources process)
    {
        var limits = binding.Options.Limits;
        this.Resident = new(binding.Name, limits.MaxResidentBytes, process.ForProvider(binding.Provider.Name));
        this.HotResident = new(binding.Name, binding.Options.HotEntries.MaxResidentBytes, process.HotResident);
        this.Sources = new(binding.Name, limits.MaxActiveSources, limits.MaxQueuedSources, process.Sources);
        this.ProviderIO = new(binding.Name, limits.MaxActiveProviderOperations, limits.MaxQueuedProviderOperations, process.ProviderIO);
        this.MetadataQueries = new(binding.Name, limits.MaxMetadataQueries, limits.MaxQueuedProviderOperations, process.MetadataQueries);
        this.Transfers = new(binding.Name, limits.MaxTransfers, limits.MaxQueuedProviderOperations, process.Transfers);
        this.RefreshSources = new(binding.Name, limits.MaxRefreshSources, limits.MaxQueuedSources, process.RefreshSources);
    }

    /// <summary>Gets collection resident charges, including provider and process ceilings.</summary>
    public ResourceBudget Resident { get; }

    /// <summary>Gets the collection/process hot sublimit. The ordinary resident charge remains separate and counted once.</summary>
    public ResourceBudget HotResident { get; }

    /// <summary>Gets collection/process source admission.</summary>
    public CacheAdmission Sources { get; }

    /// <summary>Gets collection/process occupied provider I/O admission.</summary>
    public CacheAdmission ProviderIO { get; }

    /// <summary>Gets collection/process metadata query admission.</summary>
    public CacheAdmission MetadataQueries { get; }

    /// <summary>Gets collection/process occupied transfer admission.</summary>
    public CacheAdmission Transfers { get; }

    /// <summary>Gets collection/process background source sub-admission.</summary>
    public CacheAdmission RefreshSources { get; }
}