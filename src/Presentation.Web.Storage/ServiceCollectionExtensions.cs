// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Presentation.Web.Storage;

/// <summary>
/// Adds file storage REST endpoints to the service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the file storage REST endpoints from the fluent file storage builder with a fluent options builder.
    /// </summary>
    /// <param name="context">The file storage builder context.</param>
    /// <param name="optionsBuilder">The endpoint options builder.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current file storage builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddFileStorage(factory => factory
    ///     .RegisterProvider("documents", builder => builder
    ///         .UseLocal("Documents", rootPath)
    ///         .WithLifetime(ServiceLifetime.Singleton)))
    ///     .AddEndpoints(options => options.RequireAuthorization());
    /// </code>
    /// </example>
    public static FileStorageBuilderContext AddEndpoints(
        this FileStorageBuilderContext context,
        Builder<FileStorageEndpointsOptionsBuilder, FileStorageEndpointsOptions> optionsBuilder,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = optionsBuilder?.Invoke(new FileStorageEndpointsOptionsBuilder()).Build();

        RegisterFileStorageEndpoints(context.Services, options, enabled);

        return context;
    }

    /// <summary>
    /// Registers the file storage REST endpoints from the fluent file storage builder with explicit options.
    /// </summary>
    /// <param name="context">The file storage builder context.</param>
    /// <param name="options">Optional endpoint group options.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current file storage builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddFileStorage(factory => factory
    ///     .RegisterProvider("documents", builder => builder
    ///         .UseLocal("Documents", rootPath)
    ///         .WithLifetime(ServiceLifetime.Singleton)))
    ///     .AddEndpoints(options => options.RequireAuthorization());
    /// </code>
    /// </example>
    public static FileStorageBuilderContext AddEndpoints(
        this FileStorageBuilderContext context,
        FileStorageEndpointsOptions options,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(context);

        RegisterFileStorageEndpoints(context.Services, options, enabled);

        return context;
    }

    /// <summary>
    /// Registers the file storage REST endpoints from the fluent file storage builder with default options.
    /// </summary>
    /// <param name="context">The file storage builder context.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current file storage builder context.</returns>
    public static FileStorageBuilderContext AddEndpoints(this FileStorageBuilderContext context, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(context);

        RegisterFileStorageEndpoints(context.Services, options: null, enabled);

        return context;
    }

    /// <summary>
    /// Registers the file storage REST endpoints with a fluent options builder.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsBuilder">The endpoint options builder.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current service collection.</returns>
    /// <example>
    /// <code>
    /// services.AddFileStorageEndpoints(options => options
    ///     .RequireAuthorization()
    ///     .GroupPath("/api/_system")
    ///     .GroupTag("_System.Storage"));
    /// </code>
    /// </example>
    public static IServiceCollection AddFileStorageEndpoints(
        this IServiceCollection services,
        Builder<FileStorageEndpointsOptionsBuilder, FileStorageEndpointsOptions> optionsBuilder,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = optionsBuilder?.Invoke(new FileStorageEndpointsOptionsBuilder()).Build();

        RegisterFileStorageEndpoints(services, options, enabled);

        return services;
    }

    /// <summary>
    /// Registers the file storage REST endpoints for all providers known to the file storage factory.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional endpoint group options.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current service collection.</returns>
    /// <example>
    /// <code>
    /// services.AddFileStorage(factory => factory
    ///     .RegisterProvider("documents", builder => builder
    ///         .UseLocal("Documents", rootPath)
    ///         .WithLifetime(ServiceLifetime.Singleton)))
    ///     .AddEndpoints(options => options.RequireAuthorization());
    /// </code>
    /// </example>
    public static IServiceCollection AddFileStorageEndpoints(
        this IServiceCollection services,
        FileStorageEndpointsOptions options = null,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(services);

        RegisterFileStorageEndpoints(services, options, enabled);

        return services;
    }

    private static void RegisterFileStorageEndpoints(
        IServiceCollection services,
        FileStorageEndpointsOptions options,
        bool enabled)
    {
        if (!enabled)
        {
            return;
        }

        if (options is not null)
        {
            services.AddSingleton(options);
        }

        services.AddEndpoints<FileStorageEndpoints>(enabled);
    }
}
