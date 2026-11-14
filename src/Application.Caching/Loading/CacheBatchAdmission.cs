// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Validates and snapshots an ordered batch before source work or provider access begins.</summary>
/// <example><code>var admitted = CacheBatchAdmission.Snapshot(items, 128, binding.Options);</code></example>
public static class CacheBatchAdmission
{
    /// <summary>Checks the batch bound, null members, equivalent duplicate keys, and optional collection item limits.</summary>
    /// <param name="items">The ordered caller items to copy.</param>
    /// <param name="maximum">The greatest accepted item count.</param>
    /// <param name="options">Optional effective item size and metadata limits.</param>
    /// <returns>The immutable ordered snapshot, or a validation failure before any work is admitted.</returns>
    public static Result<IReadOnlyList<CacheItem>> Snapshot(IReadOnlyList<CacheItem> items, int maximum, CacheCollectionOptions options = null)
    {
        if (items is null || items.Count > maximum)
        {
            return Result<IReadOnlyList<CacheItem>>.Failure(new CacheError(CacheErrorCode.Validation, "The ordered batch exceeds its declared item bound."));
        }

        try
        {
            var copy = CacheBoundedSnapshot.Copy(items, maximum, nameof(items));
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (options is not null)
            {
                foreach (var item in copy)
                {
                    var valid = CacheSizePolicy.ValidateItem(item, options);
                    if (valid.IsFailure)
                    {
                        return Result<IReadOnlyList<CacheItem>>.Failure(valid);
                    }
                }
            }

            if (copy.Any(item => !keys.Add(item.Key)))
            {
                return Result<IReadOnlyList<CacheItem>>.Failure(new CacheError(CacheErrorCode.Validation, "An ordered batch cannot contain equivalent duplicate keys."));
            }

            return Result<IReadOnlyList<CacheItem>>.Success(copy);
        }
        catch (ArgumentException)
        {
            return Result<IReadOnlyList<CacheItem>>.Failure(new CacheError(CacheErrorCode.Validation, "The ordered batch contains invalid items."));
        }
    }
}