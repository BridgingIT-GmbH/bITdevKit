// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering FileStorageFactory and related dependencies with IServiceCollection.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the FileStorageFactory and its dependencies with the IServiceCollection, supporting multiple providers with configurable lifetimes and behaviors.
    /// This method enables the creation and management of multiple IFileStorageProvider instances, such as InMemoryFileStorageProvider or LocalFileStorageProvider,
    /// with optional behaviors like logging, caching, retry, or custom behaviors. Providers can be registered with specific names, lifetimes (Scoped, Singleton, Transient),
    /// and behaviors to suit different use cases, such as in-memory testing, local filesystem storage, or cloud storage integrations.
    ///
    /// </summary>
    /// <param name="services">The IServiceCollection to register with.</param>
    /// <param name="configure">Optional action to configure initial providers at registration time.</param>
    /// <returns>The IServiceCollection for chaining.</returns>
    /// <example>
    /// **Usage Scenarios:**
    /// - **Testing**: Register an in-memory provider with Transient lifetime for isolated unit/integration tests:
    ///   ```csharp
    ///   services.AddFileStorage(cfg =>
    ///   {
    ///       cfg.RegisterProvider("test", builder =>
    ///       {
    ///           builder.UseInMemory("TestStorage")
    ///                  .WithLogging()
    ///                  .WithLifetime(ServiceLifetime.Transient);
    ///       });
    ///   });
    ///   var factory = services.GetRequiredService<FileStorageFactory>();
    ///   var provider = factory.CreateProvider("test");
    ///   var result = await provider.ExistsAsync("test/file.txt", null, CancellationToken.None);
    ///   ```
    ///
    /// - **Production Local Storage**: Register a local filesystem provider with Singleton lifetime for persistent storage:
    ///   ```csharp
    ///   services.AddFileStorage(cfg =>
    ///   {
    ///       cfg.RegisterProvider("local", builder =>
    ///       {
    ///           builder.UseLocal("C:\\Storage", "LocalStorage")
    ///                  .WithLogging()
    ///                  .WithCaching()
    ///                  .WithRetry()
    ///                  .WithLifetime(ServiceLifetime.Singleton);
    ///       });
    ///   });
    ///   var factory = services.GetRequiredService<FileStorageFactory>();
    ///   var provider = factory.CreateProvider("local");
    ///   var result = await provider.WriteFileAsync("data/sample.txt", new MemoryStream(Encoding.UTF8.GetBytes("Test content")), null, CancellationToken.None);
    ///   ```
    ///
    /// - **Custom Behaviors**: Add custom behaviors for specific provider needs:
    ///   ```csharp
    ///   services.AddFileStorage(cfg =>
    ///   {
    ///       cfg.RegisterProvider("custom", builder =>
    ///       {
    ///           builder.UseInMemory("CustomStorage")
    ///                  .WithCustomBehavior(p => new CustomBehavior(p))
    ///                  .WithLifetime(ServiceLifetime.Scoped);
    ///       });
    ///   });
    ///   var factory = services.GetRequiredService<FileStorageFactory>();
    ///   var provider = factory.CreateProvider("custom");
    ///   ```
    ///
    /// - **Type-Based Lookup**: Retrieve providers by type using CreateProvider<TImplementation>:
    ///   ```csharp
    ///   var inMemoryProvider = factory.CreateProvider<InMemoryFileStorageProvider>();
    ///   ```
    /// </example>
    public static IServiceCollection AddFileStorage(this IServiceCollection services, Action<FileStorageProviderFactory> configure = null)
    {
        //services.AddLogging();
        //services.AddMemoryCache();
        services.AddSingleton<IFileStorageProviderFactory>(sp =>
        {
            var factory = new FileStorageProviderFactory(sp);
            configure?.Invoke(factory);

            return factory;
        });

        return services;
    }
}