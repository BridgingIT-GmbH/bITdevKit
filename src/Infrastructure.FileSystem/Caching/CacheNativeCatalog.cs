// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.FileSystem.Caching;

using System.Security.Cryptography;
using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;

/// <summary>Persists the canonical value contract, bounded dimension schema and provider collection policies.</summary>
/// <example><code>var persisted = CacheNativeContract.From(collectionDefinition); persisted.Validate(identity);</code></example>
public sealed record CacheNativeContract(int Format, CacheEntryIdentity Identity, string ContractId,
    IReadOnlyList<CacheDimensionSchema> Dimensions, CacheCollectionOptions Policies)
{
    /// <summary>Normalizes and validates a collection definition, sorts its dimension schema, and rejects contracts exceeding 64 KiB of JSON.</summary>
    public static CacheNativeContract From(CacheProviderCollection definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var identity = new CacheEntryIdentity(definition.Namespace, definition.Collection, definition.ContractVersion, "catalog");
        if (definition.Dimensions is null || definition.Dimensions.Count > 16 || definition.Dimensions.Any(s => s is null ||
            !Enum.IsDefined(s.Kind) || !Enum.IsDefined(s.Form) || (s.Form == CacheDimensionForm.Interval &&
                (s.AllowNull || s.Kind is not (CacheDimensionKind.Integer or CacheDimensionKind.Timestamp)))))
        {
            throw new CacheTransferException(CacheErrorCode.Validation);
        }

        var dimensions = definition.Dimensions.Select(s => s with { Name = CacheIdentityNormalizer.Name(s.Name) }).OrderBy(s => s.Name, StringComparer.Ordinal).ToArray();
        if (dimensions.Select(d => d.Name).Distinct(StringComparer.Ordinal).Count() != dimensions.Length)
        {
            throw new CacheTransferException(CacheErrorCode.Validation);
        }

        var contract = new CacheNativeContract(1, identity, CacheIdentityNormalizer.Name(definition.ContractId), dimensions, definition.Policies ?? new());
        using var size = new CacheMetadataBuffer();
        JsonSerializer.Serialize(size, contract);
        if (size.Length > 64 * CacheDefaults.KiB) { throw new CacheTransferException(CacheErrorCode.Capacity); }

        return contract;
    }

    /// <summary>Checks the exact catalog identity and revalidates canonical serialized schema and policy content.</summary>
    public void Validate(CacheEntryIdentity requested)
    {
        var expected = new CacheEntryIdentity(requested.Namespace, requested.Collection, requested.ContractVersion, "catalog");
        if (this.Format != 1 || this.Identity != expected || string.IsNullOrEmpty(this.ContractId) || this.Policies is null || this.Dimensions is null)
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }

        try
        {
            var canonical = From(new(this.Identity.Namespace, this.Identity.Collection, this.Identity.ContractVersion, this.ContractId, this.Dimensions, this.Policies));
            if (JsonSerializer.Serialize(canonical) != JsonSerializer.Serialize(this)) { throw new CacheTransferException(CacheErrorCode.Corruption); }
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        {
            throw new CacheTransferException(CacheErrorCode.Corruption);
        }
        catch (CacheTransferException) { throw new CacheTransferException(CacheErrorCode.Corruption); }
    }
}

/// <summary>Connects a logical collection&apos;s schema index to an exact contract-qualified catalog identity.</summary>
public sealed record CacheNativeContractReference(int Format, CacheEntryIdentity Identity);