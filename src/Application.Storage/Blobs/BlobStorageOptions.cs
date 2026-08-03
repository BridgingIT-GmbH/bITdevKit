// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Configures top-level blob-storage client registration.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage(options => options.Enabled(true).UseLifetime(ServiceLifetime.Scoped));
/// </code>
/// </example>
public sealed class BlobStorageOptions
{
    /// <summary>
    /// Gets a value indicating whether blob-storage client registration is enabled.
    /// </summary>
    /// <example>
    /// <code>
    /// if (options.IsEnabled)
    /// {
    ///     // Register blob-store clients.
    /// }
    /// </code>
    /// </example>
    public bool IsEnabled { get; private set; } = true;

    /// <summary>
    /// Gets the default service lifetime for clients registered through the top-level builder.
    /// </summary>
    /// <example>
    /// <code>
    /// var lifetime = options.Lifetime;
    /// </code>
    /// </example>
    public ServiceLifetime Lifetime { get; private set; } = ServiceLifetime.Scoped;

    /// <summary>
    /// Gets the hosted retention sweeper options.
    /// </summary>
    /// <example>
    /// <code>
    /// options.Retention.BatchSize = 500;
    /// </code>
    /// </example>
    public StorageRetentionOptions Retention { get; } = new();

    /// <summary>
    /// Enables or disables blob-storage client registration.
    /// </summary>
    /// <param name="enabled">A value indicating whether registration is enabled.</param>
    /// <returns>The current options instance.</returns>
    /// <example>
    /// <code>
    /// options.Enabled(false);
    /// </code>
    /// </example>
    public BlobStorageOptions Enabled(bool enabled = true)
    {
        this.IsEnabled = enabled;

        return this;
    }

    /// <summary>
    /// Sets the default service lifetime for clients registered through the top-level builder.
    /// </summary>
    /// <param name="lifetime">The service lifetime to use.</param>
    /// <returns>The current options instance.</returns>
    /// <example>
    /// <code>
    /// options.UseLifetime(ServiceLifetime.Singleton);
    /// </code>
    /// </example>
    public BlobStorageOptions UseLifetime(ServiceLifetime lifetime)
    {
        this.Lifetime = lifetime;

        return this;
    }

    /// <summary>
    /// Configures the hosted blob-retention sweeper.
    /// </summary>
    /// <param name="configure">The retention options callback.</param>
    /// <returns>The current options instance.</returns>
    /// <example>
    /// <code>
    /// options.WithRetention(retention => retention.SweepInterval = TimeSpan.FromHours(1));
    /// </code>
    /// </example>
    public BlobStorageOptions WithRetention(Action<StorageRetentionOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        configure(this.Retention);

        return this;
    }
}
