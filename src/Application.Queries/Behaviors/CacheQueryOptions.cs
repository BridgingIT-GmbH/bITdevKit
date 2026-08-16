// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;

/// <summary>
/// Defines operations for i cache query.
/// </summary>
public interface ICacheQuery
{
    /// <summary>
    /// Gets the options.
    /// </summary>
    CacheQueryOptions Options { get; }
}

/// <summary>
/// Configures cache query.
/// </summary>
public class CacheQueryOptions
{
    /// <summary>
    /// Gets or sets the key.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the sliding expiration.
    /// </summary>
    public TimeSpan? SlidingExpiration { get; set; }

    /// <summary>
    /// Gets or sets the absolute expiration.
    /// </summary>
    public DateTimeOffset? AbsoluteExpiration { get; set; }
}
