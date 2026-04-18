// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

/// <summary>
/// Configures the document-store-backed <see cref="ICacheProvider" /> implementation.
/// </summary>
public class DocumentStoreCacheProviderConfiguration
{
    /// <summary>
    /// Gets or sets the default sliding-expiration window applied when callers do not specify one explicitly.
    /// </summary>
    public TimeSpan? SlidingExpiration { get; set; }

    /// <summary>
    /// Gets or sets the default absolute-expiration timestamp applied when callers do not specify one explicitly.
    /// </summary>
    public DateTimeOffset? AbsoluteExpiration { get; set; }

    /// <summary>
    /// Gets or sets an optional provider-specific connection string value sourced from configuration.
    /// </summary>
    public string ConnectionString { get; set; }
}
