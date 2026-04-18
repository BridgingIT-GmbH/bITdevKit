// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Adds scan-based monitoring locations backed by already-registered file storage providers.
/// </summary>
public static class FileMonitoringBuilderProviderExtensions
{
    /// <summary>
    /// Configures a monitored location that resolves its storage provider by name from
    /// <see cref="IFileStorageProviderFactory" />.
    /// </summary>
    /// <param name="builder">The monitoring builder.</param>
    /// <param name="locationName">The monitoring location name.</param>
    /// <param name="providerName">The registered file storage provider name.</param>
    /// <param name="configure">The per-location monitoring options.</param>
    /// <returns>The current builder for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddFileStorage(factory => factory
    ///     .RegisterProvider("documents", storage => storage
    ///         .UseInMemory("Documents")
    ///         .WithLifetime(ServiceLifetime.Singleton)));
    ///
    /// services.AddFileMonitoring(monitoring =>
    /// {
    ///     monitoring.UseProvider("documents", "documents", options =>
    ///     {
    ///         options.UseOnDemandOnly = true;
    ///         options.FileFilter = "*.*";
    ///         options.UseProcessor&lt;FileLoggerProcessor&gt;();
    ///     });
    /// });
    /// </code>
    /// </example>
    public static FileMonitoringBuilder UseProvider(
        this FileMonitoringBuilder builder,
        string locationName,
        string providerName,
        Action<LocationOptions> configure)
    {
        EnsureArg.IsNotNull(builder, nameof(builder));
        EnsureArg.IsNotNullOrEmpty(locationName, nameof(locationName));
        EnsureArg.IsNotNullOrEmpty(providerName, nameof(providerName));
        EnsureArg.IsNotNull(configure, nameof(configure));

        var options = new LocationOptions(locationName);
        configure(options);

        builder.RegisterLocation(
            locationName,
            options,
            serviceProvider => serviceProvider.GetRequiredService<IFileStorageProviderFactory>().CreateProvider(providerName),
            typeof(FileStorageLocationHandler));

        return builder;
    }
}
