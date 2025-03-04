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
    /// <para>
    /// **Key Features:**
    /// - Supports multiple named providers (e.g., "inMemory", "local") for different storage scenarios.
    /// - Allows configuration of provider lifetimes (Scoped per request, Singleton for application-wide, Transient per instance).
    /// - Enables adding predefined behaviors (logging, caching, retry) or custom IFileStorageBehavior implementations.
    /// - Integrates seamlessly with dependency injection for ASP.NET Core or other DI frameworks.
    /// - Uses the Result pattern for consistent error handling across provider operations.
    /// </para>
    ///
    /// <para>
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
    ///
    /// <para>
    /// **Best Practices:**
    /// - Use Scoped lifetime for request-scoped operations (e.g., per HTTP request in web apps).
    /// - Use Singleton lifetime for application-wide shared resources (e.g., persistent storage).
    /// - Use Transient lifetime for isolated, short-lived operations (e.g., testing or temporary storage).
    /// - Ensure thread safety by registering FileStorageFactory as Scoped or Singleton in DI, and use appropriate LazyThreadSafetyMode for provider initialization.
    /// - Handle Result pattern errors appropriately in provider operations to manage failures gracefully.
    /// </para>
    ///
    /// <para>
    /// **Dependencies:**
    /// - Requires Microsoft.Extensions.Logging.Abstractions for logging behavior.
    /// - Requires Microsoft.Extensions.Caching.Memory for caching behavior.
    /// - Uses BridgingIT.DevKit.Common.Result for error handling in provider operations.
    /// </para>
    ///
    /// <para>
    /// **Thread Safety:**
    /// - FileStorageFactory uses ConcurrentDictionary and Lazy<IFileStorageProvider> for thread-safe provider management.
    /// - Singleton providers use LazyThreadSafetyMode.ExecutionAndPublication for thread-safe initialization.
    /// - Scoped and Transient providers use LazyThreadSafetyMode.None, relying on DI or calling code for scope/thread safety.
    /// </para>
    ///
    /// <para>
    /// **Limitations:**
    /// - Does not automatically manage provider disposal; ensure providers implement IDisposable if needed and handle disposal in your application.
    /// - Custom behaviors must implement IFileStorageBehavior and handle their own thread safety and resource management.
    /// </para>
    ///
    /// </summary>
    /// <param name="services">The IServiceCollection to register with.</param>
    /// <param name="configure">Optional action to configure initial providers at registration time.</param>
    /// <returns>The IServiceCollection for chaining.</returns>
    public static IServiceCollection AddFileStorage(this IServiceCollection services, Action<FileStorageFactory> configure = null)
    {
        //services.AddLogging();
        //services.AddMemoryCache();
        services.AddScoped(sp =>
        {
            var factory = new FileStorageFactory(sp);
            configure?.Invoke(factory);

            return factory;
        });

        return services;
    }
}