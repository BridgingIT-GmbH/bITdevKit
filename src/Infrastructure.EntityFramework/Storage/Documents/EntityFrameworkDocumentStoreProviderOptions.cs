// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Defines runtime options for <see cref="EntityFrameworkDocumentStoreProvider{TContext}" />.
/// </summary>
/// <example>
/// <code>
/// services.AddEntityFrameworkDocumentStoreClient&lt;Person, AppDbContext&gt;(
///     configure: options =>
///     {
///         options.LeaseDuration = TimeSpan.FromSeconds(15);
///         options.RetryCount = 5;
///         options.RetryDelay = TimeSpan.FromMilliseconds(100);
///     });
/// </code>
/// </example>
public class EntityFrameworkDocumentStoreProviderOptions
{
    /// <summary>
    /// Gets or sets the maximum time a mutation lease stays valid for a single writer.
    /// </summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the number of replay-safe mutation retries attempted after transient contention.
    /// </summary>
    public int RetryCount { get; set; } = 5;

    /// <summary>
    /// Gets or sets the base delay between retry attempts.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets or sets the logger factory used by the provider.
    /// </summary>
    public ILoggerFactory LoggerFactory { get; set; }

    internal ILogger CreateLogger<T>() =>
        (this.LoggerFactory ?? NullLoggerFactory.Instance).CreateLogger<T>();
}
