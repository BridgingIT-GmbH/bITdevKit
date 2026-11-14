// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Collections;

/// <summary>Rejects unsafe, unregistered, or incompatible final startup combinations before engines or source work exist.</summary>
/// <example><code>CacheCatalogValidator.Validate(catalog);</code></example>
public static class CacheCatalogValidator
{
    /// <summary>Validates the finalized catalog. Failure contains setting paths and aliases, never connection values.</summary>
    public static void Validate(CacheCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _ = CacheIdentityNormalizer.Namespace(catalog.Options.Namespace);
        CheckObject(catalog.Options, "ApplicationCaching");
        Require(catalog.Options.Limits.MaxRefreshCandidates <= 100000, "The process refresh-candidate budget exceeds its finite hard bound.");
        catalog.Options.Diagnostics.Validate();
        foreach (var provider in catalog.Providers.Values)
        {
            CheckObject(provider.NativeOptions, "Providers." + provider.Name);
            var options = provider.NativeOptions;
            Require(options.ChunkSize <= CacheDefaults.MaximumChunkBytes, "Provider chunks exceed the one-MiB hard bound.");
            Require(options.InlinePayloadThreshold <= options.ChunkSize, "Inline payload threshold exceeds the chunk size.");
            Require(options.LeaseRenewalInterval < options.LeaseLifetime, "Ownership renewal must precede expiry.");
            Require(options.MetadataReserveRatio is > 0 and < 1, "Metadata reserve must be a finite ratio between zero and one.");
            Require(options.MetadataCacheBytes <= options.MaxResidentBytes, "Provider metadata cache exceeds its resident limit.");
            Require(options.MetadataFormatVersion == CacheDefaults.FormatVersion, "Unsupported native provider metadata format.");
            Require(options.ShardCount == 256, "Filesystem format one requires 256 lazy shards.");
            Require(options.CheckpointBytes < options.MaximumJournalTailBytes, "Journal checkpoint target must precede the hard tail limit.");
            Require(options.QuotaCreditBytes <= 8 * CacheDefaults.MiB, "Filesystem credit blocks exceed the eight-MiB hard bound.");
            Require(options.AccessKind is "Local" or "Smb" or "Nfs", "Unknown filesystem access profile.");
            Require(options.Durability is "Durable" or "Buffered", "Unknown filesystem durability profile.");
        }

        foreach (var binding in catalog.Collections.Values)
        {
            var options = binding.Options;
            CheckObject(options, "Collections." + binding.Name);
            _ = CacheIdentityNormalizer.ContractVersion(options.ContractVersion);
            Require(binding.Contract.HasCodec(options.Codec), "A collection selects an unregistered codec profile.");
            Require(options.Limits.BatchSize <= options.Limits.MaxBatchSize && options.Limits.MaxBatchSize <= 1024,
                "Batch limits exceed the hard ordered-batch bound.");
            Require(options.Limits.MetadataPageSize <= options.Limits.MaxMetadataPageSize &&
                options.Limits.MaxMetadataPageSize <= 1000 && options.Limits.MaxMetadataPageBytes <= 4 * CacheDefaults.MiB,
                "Metadata paging exceeds its hard entry or byte bound.");
            Require(options.Limits.MaxMetadataPageBytes >= options.Limits.MaxEntryMetadataBytes,
                "A metadata page must be able to hold one admitted entry's metadata.");
            Require(options.Limits.MaxSelectionClauses <= 32 && options.Limits.MaxSelectionDepth <= 8 &&
                options.Limits.MaxMembershipValues <= 256 && options.Limits.MaxQueryCandidates <= 100000,
                "Selection budgets exceed portable hard bounds.");
            Require(options.Limits.MaxDimensions <= 16 && options.Limits.MaxKeyBytes <= 4096 &&
                options.Limits.MaxEntryMetadataBytes <= 64 * CacheDefaults.KiB, "Entry metadata budgets exceed portable hard bounds.");
            Require(options.Limits.MaxEncodedBytes <= options.Limits.MaxStorageBytes, "A single encoded entry exceeds collection storage capacity.");
            Require(options.Streaming.BufferSize <= catalog.Options.Limits.MaxTransientBytes, "A transfer buffer exceeds process transient capacity.");
            Require(options.Refresh.CandidateLimit <= 1000, "A collection refresh page exceeds its finite hard candidate bound.");
            Require(options.Refresh.FailureBackoff <= options.Refresh.MaximumFailureBackoff, "Refresh scheduler backoff bounds are reversed.");
            Require(options.Loading.ObservationMinimumDelay <= options.Loading.ObservationMaximumDelay, "Remote observation delay bounds are reversed.");
            Require(options.Expiration.JitterRatio is >= 0 and < 1, "Expiration jitter must be finite, at least zero, and below one.");
            Require(options.Compression.MinimumSavingRatio is >= 0 and < 1, "Compression saving must be a finite ratio below one.");
            Require(!options.Compression.Enabled || (options.Compression.Algorithm.HasValue && options.Compression.Level.HasValue),
                "Enabled compression requires an explicit algorithm and level.");
            Require(options.HotEntries.Validation == CacheHotEntryValidation.Strict, "Only strict authoritative hot validation is supported.");
            Require(options.HotEntries.MaxEntryBytes <= options.HotEntries.MaxResidentBytes &&
                options.HotEntries.MaxResidentBytes <= options.Limits.MaxResidentBytes, "Hot child limits exceed their resident parent.");
            Require(options.HotEntries.Admission.MinimumAccesses >= 2, "Hot admission requires at least two successful accesses.");
            Require(!options.HotEntries.Enabled || options.HotEntries.Representation == CacheHotEntryRepresentation.Binary ||
                binding.Contract.SupportsDecodedSharing, "Decoded hot retention requires code-approved immutable sharing and conservative accounting.");
            Require(!catalog.Options.Enabled || !options.Enabled || options.Loading.CoordinationScope != CacheCoordinationScope.SharedStore ||
                binding.Provider.Capabilities.SharedStoreCoordination, "Selected provider cannot establish shared-store source authority.");
            if (options.Loading.SourceResilience is not null)
            {
                Require(catalog.Registry.SourceResilience.ContainsKey(CacheIdentityNormalizer.Name(options.Loading.SourceResilience)),
                    "A collection selects an unregistered source resilience alias.");
            }

            ValidateSchemas(options.Dimensions);
            foreach (var item in options.Warmup.Items)
            {
                _ = CacheIdentityNormalizer.Key(item.Key);
                Require(CacheSelectionValidator.ValidateDimensions(item.Dimensions, options.Dimensions).IsSuccess,
                    "A predefined warmup item violates the declared schema.");
            }

            ValidateLoader(catalog, binding, catalog.Options.Enabled && options.Enabled && options.Refresh.Enabled, options.Refresh.Loader);
            ValidateLoader(catalog, binding, catalog.Options.Enabled && options.Enabled && options.Warmup.Enabled, options.Warmup.Loader);
            Require(options.Warmup.Items.Count <= options.Limits.MaxBatchSize, "Configured warm-up items exceed the bounded startup pass.");
            Require(options.Warmup.KeySource is null || catalog.Registry.WarmupSources.ContainsKey(CacheIdentityNormalizer.Name(options.Warmup.KeySource)),
                "A collection selects an unregistered warm-up key source.");
            ValidateBehaviors(catalog, options.Behaviors.CollectionOperations, CacheBehaviorStage.CollectionOperations);
            ValidateBehaviors(catalog, options.Behaviors.SourceExecution, CacheBehaviorStage.SourceExecution);
            ValidateBehaviors(catalog, options.Behaviors.PayloadTransformation, CacheBehaviorStage.PayloadTransformation);
        }
    }

    private static void ValidateSchemas(IReadOnlyList<CacheDimensionSchema> schemas)
    {
        Require(schemas.Count <= 16, "A dimension schema exceeds the entry count bound.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var schema in schemas)
        {
            Require(schema is not null && names.Add(CacheIdentityNormalizer.Name(schema.Name)), "Dimension schema names must be unique.");
            Require(Enum.IsDefined(schema.Kind) && Enum.IsDefined(schema.Form), "A dimension schema uses an unsupported kind or form.");
            Require(schema.Form != CacheDimensionForm.Interval ||
                (schema.Kind is CacheDimensionKind.Integer or CacheDimensionKind.Timestamp && !schema.AllowNull),
                "Intervals require non-null Integer or Timestamp endpoints.");
        }
    }

    private static void ValidateLoader(CacheCatalog catalog, CacheCollectionBinding binding, bool enabled, string alias)
    {
        if (!enabled)
        {
            return;
        }

        Require(alias is not null && catalog.Registry.Loaders.TryGetValue(CacheIdentityNormalizer.Name(alias), out var loader) &&
            loader.ValueType == binding.Contract.ValueType && loader.IsBinary == binding.Contract.IsBinary,
            "Enabled refresh/warmup requires a compatible code-registered source alias.");
        Require(!binding.Contract.IsBinary || binding.Options.Enabled, "Disabled binary collections cannot enable source loading.");
    }

    private static void ValidateBehaviors(CacheCatalog catalog, IReadOnlyList<string> aliases, CacheBehaviorStage stage)
    {
        Require(aliases is not null && aliases.Count <= 64, "A behavior stage exceeds its finite alias bound.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in aliases)
        {
            var name = CacheIdentityNormalizer.Name(alias);
            Require(names.Add(name) && catalog.Registry.Behaviors.ContainsKey((stage, name)), "A behavior stage contains a duplicate or unknown alias.");
        }
    }

    private static void CheckObject(object options, string path)
    {
        Require(options is not null, "A required cache option group is null.");
        foreach (var property in options.GetType().GetProperties())
        {
            var value = property.GetValue(options);
            var member = path + "." + property.Name;
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (type.IsEnum)
            {
                Require(value is null || Enum.IsDefined(type, value), member + " contains an unsupported enum value.");
            }
            else if (value is TimeSpan duration)
            {
                var zeroAllowed = property.Name is "StartupDelay" or "RetentionGrace" or "LockAcquireTimeout" or "RetryDelay";
                Require(duration >= TimeSpan.Zero && (zeroAllowed || duration > TimeSpan.Zero) && duration != Timeout.InfiniteTimeSpan &&
                    duration < TimeSpan.FromDays(3650), member + " requires a finite supported duration.");
            }
            else if (value is double ratio)
            {
                Require(double.IsFinite(ratio) && ratio >= 0, member + " requires a finite nonnegative ratio.");
            }
            else if (value is long integer)
            {
                Require(integer > 0 || (integer == 0 && property.Name == "ThresholdBytes"), member + " requires a positive finite limit.");
            }
            else if (value is int count)
            {
                Require(count > 0, member + " requires a positive finite limit.");
            }
            else if (value is not null && value is not string && value is not IEnumerable &&
                value.GetType().Namespace == typeof(CacheCollectionOptions).Namespace)
            {
                CheckObject(value, member);
            }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new ArgumentException(message);
        }
    }
}