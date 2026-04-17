// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

/// <summary>
/// Provides configuration-bound settings for the Entity Framework file storage provider.
/// </summary>
/// <example>
/// <code>
/// var configuration = new EntityFrameworkFileStorageConfiguration
/// {
///     LeaseDuration = TimeSpan.FromSeconds(30),
///     RetryCount = 3,
///     RetryBackoff = TimeSpan.FromMilliseconds(250),
///     PageSize = 100,
///     MaximumBufferedContentSize = 4 * 1024 * 1024
/// };
/// </code>
/// </example>
public class EntityFrameworkFileStorageConfiguration
{
    /// <summary>
    /// Gets or sets the duration of a row lease used during filesystem mutations.
    /// </summary>
    /// <example>
    /// <code>
    /// configuration.LeaseDuration = TimeSpan.FromSeconds(30);
    /// </code>
    /// </example>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum number of total mutation attempts for replay-safe transient failures, including the first attempt.
    /// </summary>
    /// <example>
    /// <code>
    /// configuration.RetryCount = 3;
    /// </code>
    /// </example>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base backoff applied between retry attempts.
    /// </summary>
    /// <example>
    /// <code>
    /// configuration.RetryBackoff = TimeSpan.FromMilliseconds(250);
    /// </code>
    /// </example>
    public TimeSpan RetryBackoff { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Gets or sets the default page size used for paged listings.
    /// </summary>
    /// <example>
    /// <code>
    /// configuration.PageSize = 200;
    /// </code>
    /// </example>
    public int PageSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the optional maximum number of bytes that may be buffered before a write is rejected.
    /// </summary>
    /// <example>
    /// <code>
    /// configuration.MaximumBufferedContentSize = 4 * 1024 * 1024;
    /// </code>
    /// </example>
    public long? MaximumBufferedContentSize { get; set; }
}
