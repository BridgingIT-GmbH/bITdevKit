// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Configures default expiration values for the in-memory cache provider.
/// </summary>
public class InMemoryCacheProviderConfiguration
{
    /// <summary>
    ///     Gets or sets the default sliding expiration applied when an operation does not specify one.
    /// </summary>
    public TimeSpan? SlidingExpiration { get; set; }

    /// <summary>
    ///     Gets or sets the default absolute expiration applied when an operation does not specify one.
    /// </summary>
    public DateTimeOffset? AbsoluteExpiration { get; set; }
}
