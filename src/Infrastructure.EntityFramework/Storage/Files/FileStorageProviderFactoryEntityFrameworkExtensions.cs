// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Application.Storage;
using static BridgingIT.DevKit.Application.Storage.FileStorageProviderFactory;

/// <summary>
/// Provides file-storage factory extensions for the Entity Framework backed provider.
/// </summary>
public static class FileStorageProviderFactoryEntityFrameworkExtensions
{
    /// <summary>
    /// Configures an Entity Framework backed file storage provider for the current file storage builder.
    /// </summary>
    /// <typeparam name="TContext">The database context type that implements <see cref="IFileStorageContext" />.</typeparam>
    /// <param name="builder">The file storage builder instance.</param>
    /// <param name="locationName">The logical name for the storage location.</param>
    /// <param name="description">An optional human-readable description.</param>
    /// <param name="configure">An optional callback used to customize provider runtime options.</param>
    /// <returns>The builder for method chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddDbContext&lt;AppDbContext&gt;(options => options.UseSqlServer(connectionString));
    ///
    /// services.AddFileStorage(factory => factory.RegisterProvider("db", fileStorage => fileStorage
    ///     .UseEntityFramework&lt;AppDbContext&gt;(
    ///         "DatabaseFiles",
    ///         "Entity Framework file storage",
    ///         options =>
    ///         {
    ///             options.LeaseDuration(TimeSpan.FromSeconds(30))
    ///                 .RetryCount(3)
    ///                 .RetryBackoff(TimeSpan.FromMilliseconds(250))
    ///                 .PageSize(100)
    ///                 .MaximumBufferedContentSize(4 * 1024 * 1024);
    ///         })
    ///     .WithLogging()));
    /// </code>
    /// </example>
    public static FileStorageBuilder UseEntityFramework<TContext>(
        this FileStorageBuilder builder,
        string locationName,
        string description = null,
        Action<EntityFrameworkFileStorageOptionsBuilder> configure = null)
        where TContext : DbContext, IFileStorageContext
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (string.IsNullOrWhiteSpace(locationName))
        {
            throw new ArgumentException("Location name cannot be null or whitespace.", nameof(locationName));
        }

        var optionsBuilder = new EntityFrameworkFileStorageOptionsBuilder();
        configure?.Invoke(optionsBuilder);
        var options = optionsBuilder.Build();

        builder.ProviderFactory = serviceProvider =>
        {
            options.LoggerFactory ??= serviceProvider.GetRequiredService<ILoggerFactory>();

            return new EntityFrameworkFileStorageProvider<TContext>(
                serviceProvider,
                serviceProvider.GetRequiredService<ILoggerFactory>(),
                locationName,
                description,
                options);
        };

        return builder;
    }
}
