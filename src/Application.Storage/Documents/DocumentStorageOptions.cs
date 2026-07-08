// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Configures top-level document-storage client registration.
/// </summary>
/// <example>
/// <code>
/// services.AddDocumentStorage(options => options.Enabled(true).UseLifetime(ServiceLifetime.Scoped));
/// </code>
/// </example>
public sealed class DocumentStorageOptions
{
    /// <summary>
    /// Gets a value indicating whether document-storage client registration is enabled.
    /// </summary>
    /// <example>
    /// <code>
    /// if (options.IsEnabled)
    /// {
    ///     // Register document-store clients.
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
    /// Enables or disables document-storage client registration.
    /// </summary>
    /// <param name="enabled">A value indicating whether registration is enabled.</param>
    /// <returns>The current options instance.</returns>
    /// <example>
    /// <code>
    /// options.Enabled(true);
    /// </code>
    /// </example>
    public DocumentStorageOptions Enabled(bool enabled = true)
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
    public DocumentStorageOptions UseLifetime(ServiceLifetime lifetime)
    {
        this.Lifetime = lifetime;

        return this;
    }
}
