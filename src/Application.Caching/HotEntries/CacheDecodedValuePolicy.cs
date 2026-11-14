// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Applies the code-registered immutable-sharing and conservative-size contract before retaining a decoded graph.</summary>
/// <example><code>if (CacheDecodedValuePolicy.TryEstimate(contract, value, maximumBytes, out var bytes)) { /* eligible */ }</code></example>
public static class CacheDecodedValuePolicy
{
    /// <summary>Declines unapproved, unknown, invalid, oversized, or failing estimates without failing the successful ordinary read.</summary>
    public static bool TryEstimate<T>(CacheValueContract<T> contract, T value, long maximumBytes, out long bytes)
    {
        bytes = 0;
        if (contract?.SupportsDecodedSharing != true) { return false; }

        try
        {
            var estimate = contract.EstimateDecodedBytes(value);
            if (estimate is not >= 0 || estimate.Value > maximumBytes) { return false; }

            bytes = estimate.Value;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}