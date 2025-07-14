// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Requester service to the specified <see cref="IServiceCollection"/> and returns a <see cref="RequesterBuilder"/> for configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method registers the necessary services for the Requester system, including the <see cref="IRequester"/> implementation
    /// and its dependencies. It returns a <see cref="RequesterBuilder"/> that can be used to configure handlers and behaviors
    /// using a fluent API. The Requester system enables dispatching requests to their corresponding handlers through a pipeline
    /// of behaviors, supporting features like validation, retry, timeout, and chaos injection.
    /// </para>
    /// <para>
    /// To use the Requester system, you must:
    /// 1. Call <see cref="AddRequester"/> to register the core services.
    /// 2. Use <see cref="RequesterBuilder.AddHandlers"/> to scan for and register request handlers.
    /// 3. Optionally, use <see cref="RequesterBuilder.WithBehavior{TBehavior}"/> to add pipeline behaviors.
    /// 4. Build the service provider to resolve the <see cref="IRequester"/> service for dispatching requests.
    /// </para>
    /// <para>
    /// The <see cref="IRequester"/> service is registered with a scoped lifetime, meaning a new instance is created for each
    /// scope (e.g., per HTTP request in ASP.NET Core). Ensure that any dependencies (e.g., logging) are also registered in the
    /// service collection before calling this method.
    /// </para>
    /// </remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the Requester service to.</param>
    /// <returns>A <see cref="RequesterBuilder"/> for fluent configuration.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <example>
    /// <code>
    /// // Basic usage with default configuration
    /// var services = new ServiceCollection();
    /// services.AddLogging(); // Required for IRequester logging
    /// services.AddRequester()
    ///     .AddHandlers(new[] { "^System\\..*" }); // Exclude System assemblies
    /// var provider = services.BuildServiceProvider();
    /// var requester = provider.GetRequiredService<IRequester>();
    /// var result = await requester.SendAsync(new MyRequest());
    ///
    /// // Usage with pipeline behaviors
    /// var services = new ServiceCollection();
    /// services.AddLogging();
    /// services.AddRequester()
    ///     .AddHandlers(new[] { "^System\\..*" })
    ///     .WithBehavior<ValidationBehavior<,>>() // Add validation behavior
    ///     .WithBehavior<RetryBehavior<,>>();    // Add retry behavior
    /// var provider = services.BuildServiceProvider();
    /// var requester = provider.GetRequiredService<IRequester>();
    /// var result = await requester.SendAsync(new MyRequest());
    /// </code>
    /// </example>
    /// <seealso cref="RequesterBuilder"/>
    /// <seealso cref="IRequester"/>
    public static RequesterBuilder AddRequester(this IServiceCollection services)
    {
        return services == null ? throw new ArgumentNullException(nameof(services)) : new RequesterBuilder(services);
    }

    /// <summary>
    /// Adds the Notifier service to the specified <see cref="IServiceCollection"/> and returns a <see cref="NotifierBuilder"/> for configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method registers the necessary services for the Notifier system, including the <see cref="INotifier"/> implementation
    /// and its dependencies. It returns a <see cref="NotifierBuilder"/> that can be used to configure handlers and behaviors
    /// using a fluent API. The Notifier system enables dispatching notifications to their corresponding handlers through a pipeline
    /// of behaviors, supporting features like validation, retry, and timeout.
    /// </para>
    /// <para>
    /// To use the Notifier system, you must:
    /// 1. Call <see cref="AddNotifier"/> to register the core services.
    /// 2. Use <see cref="NotifierBuilder.AddHandlers"/> to scan for and register notification handlers.
    /// 3. Optionally, use <see cref="NotifierBuilder.WithBehavior{TBehavior}"/> to add pipeline behaviors.
    /// 4. Build the service provider to resolve the <see cref="INotifier"/> service for dispatching notifications.
    /// </para>
    /// <para>
    /// The <see cref="INotifier"/> service is registered with a scoped lifetime, meaning a new instance is created for each
    /// scope (e.g., per HTTP request in ASP.NET Core). Ensure that any dependencies (e.g., logging) are also registered in the
    /// service collection before calling this method.
    /// </para>
    /// </remarks>
    /// <returns>A <see cref="NotifierBuilder"/> for fluent configuration.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <example>
    /// <code>
    /// // Basic usage with default configuration
    /// var services = new ServiceCollection();
    /// services.AddLogging(); // Required for INotifier logging
    /// services.AddNotifier()
    ///     .AddHandlers(new[] { "^System\\..*" }); // Exclude System assemblies
    /// var provider = services.BuildServiceProvider();
    /// var notifier = provider.GetRequiredService<INotifier>();
    /// var result = await notifier.PublishAsync(new EmailSentNotification());
    ///
    /// // Usage with pipeline behaviors
    /// var services = new ServiceCollection();
    /// services.AddLogging();
    /// services.AddNotifier()
    ///     .AddHandlers(new[] { "^System\\..*" })
    ///     .WithBehavior<ValidationBehavior<,>>() // Add validation behavior
    ///     .WithBehavior<RetryBehavior<,>>();    // Add retry behavior
    /// var provider = services.BuildServiceProvider();
    /// var notifier = provider.GetRequiredService<INotifier>();
    /// var result = await notifier.PublishAsync(new EmailSentNotification());
    /// </code>
    /// </example>
    /// <seealso cref="NotifierBuilder"/>
    /// <seealso cref="INotifier"/>
    public static NotifierBuilder AddNotifier(this IServiceCollection services)
    {
        return services == null ? throw new ArgumentNullException(nameof(services)) : new NotifierBuilder(services);
    }
}