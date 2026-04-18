// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures cache expiration defaults for <see cref="CacheDocumentStoreClientBehavior{T}" />.
/// </summary>
public class CacheDocumentStoreClientBehaviorOptions
{
    /// <summary>
    /// Gets or sets the sliding-expiration window applied to cached query results.
    /// </summary>
    public TimeSpan? SlidingExpiration { get; set; }

    /// <summary>
    /// Gets or sets the absolute expiration timestamp applied to cached query results.
    /// </summary>
    public DateTimeOffset? AbsoluteExpiration { get; set; }
}
