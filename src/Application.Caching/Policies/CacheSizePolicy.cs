// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Text;
using System.Text.Encodings.Web;

/// <summary>Validates owner-controlled metadata and entry limits without consuming payload input or cloning typed values.</summary>
/// <example><code>var dimensions = CacheSizePolicy.MergeDimensions(item, descriptor, options);</code></example>
public static class CacheSizePolicy
{
    /// <summary>Validates known owner/follower dependencies before provider access or source ownership.</summary>
    public static Result ValidateItem(CacheItem item, CacheCollectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Dimensions.Count > options.Limits.MaxDimensions || !KnownMetadataFits(item, options.Limits.MaxEntryMetadataBytes))
        {
            return Result.Failure(new CacheError(CacheErrorCode.Validation, "Known dependencies exceed the collection's entry metadata bound."));
        }

        return CacheSelectionValidator.ValidateDimensions(item.Dimensions, options.Dimensions);
    }

    /// <summary>Estimates the retained item graph, including dimension records, membership arrays, strings and identity copies.</summary>
    /// <param name="item">The validated immutable owner item whose references the source flight retains.</param>
    /// <returns>A conservative resident-byte estimate, independent of the encoded metadata limit.</returns>
    /// <example><code>var bytes = Math.Max(ByteSize.Kilobytes(128), CacheSizePolicy.EstimateItemResidentBytes(item));</code></example>
    public static long EstimateItemResidentBytes(CacheItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return checked(2048 + 3 * StringResidentBytes(item.Key) + DimensionsResidentBytes(item.Dimensions));
    }

    /// <summary>Estimates retained metadata and hot-index bookkeeping without treating a small payload as a small metadata graph.</summary>
    /// <param name="metadata">The immutable acquired publication metadata retained by a hot entry.</param>
    /// <returns>A conservative charge for fixed objects, identity/display strings and all dimension members.</returns>
    /// <example><code>var metadataBytes = CacheSizePolicy.EstimateMetadataResidentBytes(acquired.Metadata);</code></example>
    public static long EstimateMetadataResidentBytes(CacheEntryMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return checked(1024 + EstimateIdentityResidentBytes(metadata.Identity, metadata.Version) +
            StringResidentBytes(metadata.DisplayKey) + StringResidentBytes(metadata.ContractId) +
            StringResidentBytes(metadata.CodecId) + StringResidentBytes(metadata.Compression) + StringResidentBytes(metadata.ContentType) +
            StringResidentBytes(metadata.PayloadHash) + StringResidentBytes(metadata.StoredHash) + DimensionsResidentBytes(metadata.Dimensions));
    }

    /// <summary>Estimates bounded tracking/coordination bookkeeping and every identity/version string it retains.</summary>
    /// <param name="identity">The immutable identity retained by a dictionary entry or promotion.</param>
    /// <param name="version">The acquired version token, or default when no version is retained.</param>
    /// <returns>A conservative resident-byte estimate that includes namespace, collection, contract, key and token.</returns>
    /// <example><code>var trackingBytes = CacheSizePolicy.EstimateIdentityResidentBytes(metadata.Identity, metadata.Version);</code></example>
    public static long EstimateIdentityResidentBytes(CacheEntryIdentity identity, CacheEntryVersion version = default) =>
        checked(512 + StringResidentBytes(identity.Namespace) + StringResidentBytes(identity.Collection) +
            StringResidentBytes(identity.ContractVersion) + StringResidentBytes(identity.Key) + StringResidentBytes(version.Value));

    /// <summary>Merges known owner dependencies with late source dependencies. Followers cannot replace this snapshot.</summary>
    public static Result<IReadOnlyList<CacheDimension>> MergeDimensions(CacheItem item, CacheEntryDescriptor descriptor,
        CacheCollectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(options);
        descriptor ??= new();
        if (descriptor.Dimensions is null || descriptor.Dimensions.Count > options.Limits.MaxDimensions ||
            item.Dimensions.Count > options.Limits.MaxDimensions)
        {
            return Result<IReadOnlyList<CacheDimension>>.Failure(new CacheError(CacheErrorCode.Validation, "Entry dependency count exceeds its bound."));
        }

        var merged = new Dictionary<string, CacheDimension>(StringComparer.OrdinalIgnoreCase);
        foreach (var dimension in item.Dimensions.Concat(descriptor.Dimensions))
        {
            if (dimension is null)
            {
                return Result<IReadOnlyList<CacheDimension>>.Failure(new CacheError(CacheErrorCode.Validation, "Entry dependencies cannot contain null members."));
            }

            if (merged.TryGetValue(dimension.Name, out var previous))
            {
                if (previous.Kind != dimension.Kind || previous.Form != dimension.Form ||
                    (previous.Form == CacheDimensionForm.Set ? !previous.Values.ToHashSet().SetEquals(dimension.Values) :
                        !previous.Values.SequenceEqual(dimension.Values)))
                {
                    return Result<IReadOnlyList<CacheDimension>>.Failure(new CacheError(CacheErrorCode.Contract,
                        "Late source metadata conflicts with the captured owner's dependency."));
                }
            }
            else
            {
                merged.Add(dimension.Name, dimension);
            }
        }

        if (merged.Count > options.Limits.MaxDimensions)
        {
            return Result<IReadOnlyList<CacheDimension>>.Failure(new CacheError(CacheErrorCode.Validation, "Merged dependencies exceed the entry bound."));
        }

        var dimensions = Array.AsReadOnly(merged.Values.ToArray());
        var validation = CacheSelectionValidator.ValidateDimensions(dimensions, options.Dimensions);
        if (validation.IsFailure)
        {
            return Result<IReadOnlyList<CacheDimension>>.Failure(validation);
        }

        return Result<IReadOnlyList<CacheDimension>>.Success(dimensions);
    }

    /// <summary>Validates declared graph/item observations and retention priority before staging.</summary>
    public static Result ValidateDescriptor(CacheEntryDescriptor descriptor, CacheCollectionOptions options)
    {
        descriptor ??= new();
        if (descriptor.DecodedSizeBytes is < 0 || descriptor.DecodedSizeBytes > options.Limits.MaxDecodedBytes ||
            descriptor.ItemCount is < 0 || !Enum.IsDefined(descriptor.Priority))
        {
            return Result.Failure(new CacheError(CacheErrorCode.Contract, "Invalid entry size, item-count, or priority observation."));
        }

        return Result.Success();
    }

    /// <summary>Gets the format-one stored-byte ceiling with bounded envelope/frame overhead.</summary>
    public static long MaximumStoredBytes(CacheCollectionOptions options) => checked(options.Limits.MaxEncodedBytes +
        2 * options.Limits.MaxEntryMetadataBytes + (options.Limits.MaxEncodedBytes / (64 * CacheDefaults.KiB) + 1) * 40 + 16);

    private static bool KnownMetadataFits(CacheItem item, long maximum)
    {
        // Count bounded JSON strings without allocating a serialized copy of an oversized caller graph.
        var bytes = 64 + JsonStringBytes(item.Key);
        foreach (var dimension in item.Dimensions)
        {
            if (dimension is null) { return false; }

            bytes += 96 + JsonStringBytes(dimension.Name);
            foreach (var value in dimension.Values)
            {
                bytes += 64 + JsonStringBytes(value.CanonicalValue);
                if (bytes > maximum) { return false; }
            }

            if (bytes > maximum) { return false; }
        }

        return bytes <= maximum;
    }

    private static long JsonStringBytes(string value)
    {
        if (value is null) { return 4; }

        var bytes = 2L;
        foreach (var rune in value.EnumerateRunes())
        {
            bytes += JavaScriptEncoder.Default.WillEncode(rune.Value) ? 6 * rune.Utf16SequenceLength : rune.Utf8SequenceLength;
        }

        return bytes;
    }

    private static long StringResidentBytes(string value) => value is null ? 0 : 32 + value.Length * 2L;

    private static long DimensionsResidentBytes(IReadOnlyList<CacheDimension> dimensions)
    {
        var bytes = 64L + (dimensions?.Count ?? 0) * 8L;
        foreach (var dimension in dimensions ?? [])
        {
            bytes = checked(bytes + 192 + StringResidentBytes(dimension.Name) + dimension.Values.Count * 8L);
            foreach (var value in dimension.Values) { bytes = checked(bytes + 96 + StringResidentBytes(value.CanonicalValue)); }
        }

        return bytes;
    }
}