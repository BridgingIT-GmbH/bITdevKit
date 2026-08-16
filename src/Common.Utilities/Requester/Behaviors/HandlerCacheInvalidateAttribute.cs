// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Marks a request handler to invalidate cache entries whose keys start with a specified prefix after execution.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class HandlerCacheInvalidateAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="HandlerCacheInvalidateAttribute"/> class.
    /// </summary>
    /// <param name="key">The cache-key prefix to invalidate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null or empty.</exception>
    public HandlerCacheInvalidateAttribute(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key), "Cache key cannot be null or empty.");
        }

        this.Key = key;
    }

    /// <summary>Gets the cache-key prefix to invalidate.</summary>
    public string Key { get; }
}
