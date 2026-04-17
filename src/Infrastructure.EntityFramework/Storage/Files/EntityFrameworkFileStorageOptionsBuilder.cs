// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

/// <summary>
/// Builds <see cref="EntityFrameworkFileStorageOptions" /> instances for the Entity Framework file storage provider.
/// </summary>
/// <example>
/// <code>
/// var options = new EntityFrameworkFileStorageOptionsBuilder()
///     .LeaseDuration(TimeSpan.FromSeconds(30))
///     .RetryCount(3)
///     .RetryBackoff(TimeSpan.FromMilliseconds(250))
///     .PageSize(100)
///     .MaximumBufferedContentSize(4 * 1024 * 1024)
///     .Build();
/// </code>
/// </example>
public class EntityFrameworkFileStorageOptionsBuilder
    : OptionsBuilderBase<EntityFrameworkFileStorageOptions, EntityFrameworkFileStorageOptionsBuilder>
{
    /// <summary>
    /// Applies values from a configuration object to the current builder.
    /// </summary>
    /// <param name="configuration">The configuration values to copy.</param>
    /// <returns>The current builder instance.</returns>
    /// <example>
    /// <code>
    /// var configuration = new EntityFrameworkFileStorageConfiguration
    /// {
    ///     LeaseDuration = TimeSpan.FromSeconds(45),
    ///     RetryCount = 5
    /// };
    ///
    /// var options = new EntityFrameworkFileStorageOptionsBuilder()
    ///     .Apply(configuration)
    ///     .Build();
    /// </code>
    /// </example>
    public EntityFrameworkFileStorageOptionsBuilder Apply(EntityFrameworkFileStorageConfiguration configuration)
    {
        if (configuration is null)
        {
            return this;
        }

        this.Target.LeaseDuration = configuration.LeaseDuration;
        this.Target.RetryCount = configuration.RetryCount;
        this.Target.RetryBackoff = configuration.RetryBackoff;
        this.Target.PageSize = configuration.PageSize;
        this.Target.MaximumBufferedContentSize = configuration.MaximumBufferedContentSize;

        return this;
    }

    /// <summary>
    /// Sets the duration of a row lease used during filesystem mutations.
    /// </summary>
    /// <param name="value">The lease duration.</param>
    /// <returns>The current builder instance.</returns>
    /// <example>
    /// <code>
    /// var options = new EntityFrameworkFileStorageOptionsBuilder()
    ///     .LeaseDuration(TimeSpan.FromSeconds(30))
    ///     .Build();
    /// </code>
    /// </example>
    public EntityFrameworkFileStorageOptionsBuilder LeaseDuration(TimeSpan value)
    {
        this.Target.LeaseDuration = value;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of total mutation attempts for replay-safe transient failures, including the first attempt.
    /// </summary>
    /// <param name="value">The retry count.</param>
    /// <returns>The current builder instance.</returns>
    /// <example>
    /// <code>
    /// var options = new EntityFrameworkFileStorageOptionsBuilder()
    ///     .RetryCount(3)
    ///     .Build();
    /// </code>
    /// </example>
    public EntityFrameworkFileStorageOptionsBuilder RetryCount(int value)
    {
        this.Target.RetryCount = value;
        return this;
    }

    /// <summary>
    /// Sets the base backoff applied between retry attempts.
    /// </summary>
    /// <param name="value">The retry backoff.</param>
    /// <returns>The current builder instance.</returns>
    /// <example>
    /// <code>
    /// var options = new EntityFrameworkFileStorageOptionsBuilder()
    ///     .RetryBackoff(TimeSpan.FromMilliseconds(250))
    ///     .Build();
    /// </code>
    /// </example>
    public EntityFrameworkFileStorageOptionsBuilder RetryBackoff(TimeSpan value)
    {
        this.Target.RetryBackoff = value;
        return this;
    }

    /// <summary>
    /// Sets the default page size used for paged listings.
    /// </summary>
    /// <param name="value">The page size.</param>
    /// <returns>The current builder instance.</returns>
    /// <example>
    /// <code>
    /// var options = new EntityFrameworkFileStorageOptionsBuilder()
    ///     .PageSize(200)
    ///     .Build();
    /// </code>
    /// </example>
    public EntityFrameworkFileStorageOptionsBuilder PageSize(int value)
    {
        this.Target.PageSize = value;
        return this;
    }

    /// <summary>
    /// Sets the optional maximum number of bytes that may be buffered before a write is rejected.
    /// </summary>
    /// <param name="value">The maximum buffered content size in bytes, or <see langword="null" /> for no limit.</param>
    /// <returns>The current builder instance.</returns>
    /// <example>
    /// <code>
    /// var options = new EntityFrameworkFileStorageOptionsBuilder()
    ///     .MaximumBufferedContentSize(4 * 1024 * 1024)
    ///     .Build();
    /// </code>
    /// </example>
    public EntityFrameworkFileStorageOptionsBuilder MaximumBufferedContentSize(long? value)
    {
        this.Target.MaximumBufferedContentSize = value;
        return this;
    }
}
