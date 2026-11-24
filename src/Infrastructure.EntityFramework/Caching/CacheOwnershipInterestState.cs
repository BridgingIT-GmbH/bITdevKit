// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using System.Text;
using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;

/// <summary>Encodes finite per-flight observation groups as portable ASCII JSON with exact ordinal identifier comparisons.</summary>
/// <remarks>The ordinary JSON encoder escapes Unicode, so every supported native engine stores the same bounded representation without collation-dependent child lookups.</remarks>
/// <example><code>var interests = CacheOwnershipInterestState.Read(flight.InterestsJson);</code></example>
public static class CacheOwnershipInterestState
{
    /// <summary>The maximum encoded per-flight JSON size, in ASCII bytes and characters.</summary>
    public const int MaximumJsonBytes = 256 * 1024;

    /// <summary>The hard maximum observation groups retained by one source flight.</summary>
    public const int MaximumInterests = 1024;

    /// <summary>The unchanged conservative persisted-metadata charge for each admitted observation group.</summary>
    public const int InterestMetadataBytes = 1536;

    /// <summary>Decodes bounded persisted state, rejecting malformed, duplicate or oversized children as corruption.</summary>
    /// <param name="json">The bounded persisted ASCII JSON array.</param>
    /// <returns>A finite mutable list owned by the current bounded native call.</returns>
    /// <exception cref="CacheRelationalException">The retained child state is corrupt or exceeds the format bounds.</exception>
    public static List<CacheOwnershipInterest> Read(string json)
    {
        try
        {
            if (json is null || json.Length > MaximumJsonBytes || json.Any(character => character > 127)) { throw new JsonException(); }

            var interests = JsonSerializer.Deserialize<List<CacheOwnershipInterest>>(json) ?? throw new JsonException();
            if (interests.Count > MaximumInterests) { throw new JsonException(); }

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var interest in interests)
            {
                if (interest is null || !names.Add(interest.InterestId) || interest.DeadlineTicks <= 0 || interest.DeadlineTicks > DateTimeOffset.MaxValue.UtcTicks)
                {
                    throw new JsonException();
                }

                _ = CacheIdentityNormalizer.Name(interest.InterestId);
            }

            return interests;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            throw new CacheRelationalException(CacheErrorCode.Corruption, "The persisted source-flight interests exceed the bounded JSON format.");
        }
    }

    /// <summary>Encodes an admitted finite child list without truncating or silently dropping observations.</summary>
    /// <param name="interests">The children already validated by the native ownership boundary.</param>
    /// <returns>Portable ASCII JSON within the format ceiling.</returns>
    /// <exception cref="CacheRelationalException">The requested child count or encoded state exceeds finite capacity.</exception>
    public static string Write(IReadOnlyCollection<CacheOwnershipInterest> interests)
    {
        if (interests.Count > MaximumInterests)
        {
            throw new CacheRelationalException(CacheErrorCode.Capacity, "The finite source-flight observation-group capacity is exhausted.");
        }

        var json = JsonSerializer.Serialize(interests);
        if (Encoding.UTF8.GetByteCount(json) > MaximumJsonBytes)
        {
            throw new CacheRelationalException(CacheErrorCode.Capacity, "The finite source-flight interest JSON byte capacity is exhausted.");
        }

        return json;
    }
}