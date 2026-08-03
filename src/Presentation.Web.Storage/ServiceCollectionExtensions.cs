// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web.Storage;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Adds file storage REST endpoints to the service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Blob Storage console commands to the service collection.
    /// </summary>
    /// <param name="context">The blob storage builder context.</param>
    /// <param name="enabled">Indicates whether console command registration should be enabled.</param>
    /// <returns>The current blob storage builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage()
    ///     .AddConsoleCommands();
    /// </code>
    /// </example>
    public static BlobStorageBuilderContext AddConsoleCommands(
        this BlobStorageBuilderContext context,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        RegisterBlobStorageConsoleCommands(context.Services, enabled);
        return context;
    }

    /// <summary>
    /// Adds Document Storage console commands to the service collection.
    /// </summary>
    /// <param name="context">The document storage builder context.</param>
    /// <param name="enabled">Indicates whether console command registration should be enabled.</param>
    /// <returns>The current document storage builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddDocumentStorage()
    ///     .AddConsoleCommands();
    /// </code>
    /// </example>
    public static DocumentStorageBuilderContext AddConsoleCommands(
        this DocumentStorageBuilderContext context,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        RegisterDocumentStorageConsoleCommands(context.Services, enabled);
        return context;
    }

    /// <summary>
    /// Adds File Storage console commands to the service collection.
    /// </summary>
    /// <param name="context">The file storage builder context.</param>
    /// <param name="enabled">Indicates whether console command registration should be enabled.</param>
    /// <returns>The current file storage builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddFileStorage(factory => factory.RegisterProvider("default", builder => builder.UseInMemory()))
    ///     .AddConsoleCommands();
    /// </code>
    /// </example>
    public static FileStorageBuilderContext AddConsoleCommands(
        this FileStorageBuilderContext context,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        RegisterFileStorageConsoleCommands(context.Services, enabled);
        return context;
    }

    /// <summary>
    /// Registers stable Storage Permalink download endpoints with fluent authorization options.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddStoragePermalinkEndpoints(options => options.AllowAnonymous());
    /// </code>
    /// </example>
    public static IServiceCollection AddStoragePermalinkEndpoints(this IServiceCollection services, Builder<StoragePermalinkEndpointsOptionsBuilder, StoragePermalinkEndpointsOptions> optionsBuilder, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(services);
        RegisterStoragePermalinkEndpoints(services, optionsBuilder?.Invoke(new StoragePermalinkEndpointsOptionsBuilder()).Build(), enabled);
        return services;
    }

    /// <summary>
    /// Registers stable Storage Permalink download endpoints.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddStoragePermalinkEndpoints();
    /// </code>
    /// </example>
    public static IServiceCollection AddStoragePermalinkEndpoints(this IServiceCollection services, StoragePermalinkEndpointsOptions options = null, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(services);
        RegisterStoragePermalinkEndpoints(services, options, enabled);
        return services;
    }

    /// <summary>
    /// Adds permalink download endpoints to the Storage Permalink registration flow.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddStoragePermalinks().UseInMemory().AddDownloadEndpoints();
    /// </code>
    /// </example>
    public static StoragePermalinkBuilderContext AddDownloadEndpoints(this StoragePermalinkBuilderContext context, StoragePermalinkEndpointsOptions options = null, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        RegisterStoragePermalinkEndpoints(context.Services, options, enabled);
        return context;
    }

    /// <summary>
    /// Adds permalink download endpoints with fluent authorization options to the Storage Permalink registration flow.
    /// </summary>
    /// <param name="context">
    /// The Storage Permalink builder context.
    /// </param>
    /// <param name="optionsBuilder">
    /// The endpoint options builder.
    /// </param>
    /// <param name="enabled">
    /// Indicates whether endpoint registration is enabled.
    /// </param>
    /// <returns>
    /// The current Storage Permalink builder context.
    /// </returns>
    /// <example>
    /// <code>
    /// services.AddStoragePermalinks()
    ///     .UseInMemory()
    ///     .AddDownloadEndpoints(options => options
    ///         .RequireAuthorization()
    ///         .RequirePolicy("StorageDownloads"));
    /// </code>
    /// </example>
    public static StoragePermalinkBuilderContext AddDownloadEndpoints(
        this StoragePermalinkBuilderContext context,
        Builder<StoragePermalinkEndpointsOptionsBuilder, StoragePermalinkEndpointsOptions> optionsBuilder,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        RegisterStoragePermalinkEndpoints(context.Services, optionsBuilder?.Invoke(new StoragePermalinkEndpointsOptionsBuilder()).Build(), enabled);
        return context;
    }

    /// <summary>
    /// Explicitly adds Blob Storage MCP handlers to the service collection.
    /// </summary>
    /// <param name="context">The blob storage builder context.</param>
    /// <param name="enabled">Indicates whether MCP handler registration should be enabled.</param>
    /// <returns>The current blob storage builder context.</returns>
    /// <example>
    /// <code>
    /// // Usually unnecessary in DevKit web hosts because AddBlobStorage()
    /// // registers the handler automatically when MCP is enabled.
    /// services.AddBlobStorage()
    ///     .AddMcpHandlers();
    /// </code>
    /// </example>
    public static BlobStorageBuilderContext AddMcpHandlers(
        this BlobStorageBuilderContext context,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (enabled)
        {
            context.Services.AddMcpHandler<BlobStorageMcpHandler>();
        }

        return context;
    }

    /// <summary>
    /// Registers Blob Storage maintenance endpoints from the fluent blob storage builder with a fluent options builder.
    /// </summary>
    /// <param name="context">The blob storage builder context.</param>
    /// <param name="optionsBuilder">The endpoint options builder.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current blob storage builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage()
    ///     .AddMaintenanceEndpoints(options => options.RequireAuthorization());
    /// </code>
    /// </example>
    public static BlobStorageBuilderContext AddMaintenanceEndpoints(
        this BlobStorageBuilderContext context,
        Builder<BlobStorageMaintenanceEndpointsOptionsBuilder, BlobStorageMaintenanceEndpointsOptions> optionsBuilder,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = optionsBuilder?.Invoke(new BlobStorageMaintenanceEndpointsOptionsBuilder()).Build();

        RegisterBlobStorageMaintenanceEndpoints(context.Services, options, enabled);

        return context;
    }

    /// <summary>
    /// Registers Blob Storage maintenance endpoints from the fluent blob storage builder with explicit options.
    /// </summary>
    /// <param name="context">The blob storage builder context.</param>
    /// <param name="options">Optional endpoint group options.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current blob storage builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage()
    ///     .AddMaintenanceEndpoints(new BlobStorageMaintenanceEndpointsOptions());
    /// </code>
    /// </example>
    public static BlobStorageBuilderContext AddMaintenanceEndpoints(
        this BlobStorageBuilderContext context,
        BlobStorageMaintenanceEndpointsOptions options,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(context);

        RegisterBlobStorageMaintenanceEndpoints(context.Services, options, enabled);

        return context;
    }

    /// <summary>
    /// Registers Blob Storage maintenance endpoints from the fluent blob storage builder with default options.
    /// </summary>
    /// <param name="context">The blob storage builder context.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current blob storage builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage()
    ///     .AddMaintenanceEndpoints();
    /// </code>
    /// </example>
    public static BlobStorageBuilderContext AddMaintenanceEndpoints(
        this BlobStorageBuilderContext context,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(context);

        RegisterBlobStorageMaintenanceEndpoints(context.Services, options: null, enabled);

        return context;
    }

    /// <summary>
    /// Registers Blob Storage read-only content endpoints from the fluent blob storage builder with a fluent options builder.
    /// </summary>
    /// <param name="context">The blob storage builder context.</param>
    /// <param name="optionsBuilder">The endpoint options builder.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current blob storage builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage()
    ///     .AddReadEndpoints(options => options.AllowAnonymous());
    /// </code>
    /// </example>
    public static BlobStorageBuilderContext AddReadEndpoints(
        this BlobStorageBuilderContext context,
        Builder<BlobStorageReadEndpointsOptionsBuilder, BlobStorageReadEndpointsOptions> optionsBuilder,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = optionsBuilder?.Invoke(new BlobStorageReadEndpointsOptionsBuilder()).Build();

        RegisterBlobStorageReadEndpoints(context.Services, options, enabled);

        return context;
    }

    /// <summary>
    /// Registers Blob Storage read-only content endpoints from the fluent blob storage builder with explicit options.
    /// </summary>
    /// <param name="context">The blob storage builder context.</param>
    /// <param name="options">Optional endpoint group options.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current blob storage builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage()
    ///     .AddReadEndpoints(new BlobStorageReadEndpointsOptions());
    /// </code>
    /// </example>
    public static BlobStorageBuilderContext AddReadEndpoints(
        this BlobStorageBuilderContext context,
        BlobStorageReadEndpointsOptions options,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(context);

        RegisterBlobStorageReadEndpoints(context.Services, options, enabled);

        return context;
    }

    /// <summary>
    /// Registers Blob Storage read-only content endpoints from the fluent blob storage builder with default options.
    /// </summary>
    /// <param name="context">The blob storage builder context.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current blob storage builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage()
    ///     .AddReadEndpoints();
    /// </code>
    /// </example>
    public static BlobStorageBuilderContext AddReadEndpoints(
        this BlobStorageBuilderContext context,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(context);

        RegisterBlobStorageReadEndpoints(context.Services, options: null, enabled);

        return context;
    }

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
    ///     .GroupPath("/_bdk/api")
    ///     .GroupTag("_bdk.Storage"));
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

    /// <summary>
    /// Registers Blob Storage maintenance endpoints with a fluent options builder.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsBuilder">The endpoint options builder.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current service collection.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorageMaintenanceEndpoints(options => options.RequireAuthorization());
    /// </code>
    /// </example>
    public static IServiceCollection AddBlobStorageMaintenanceEndpoints(
        this IServiceCollection services,
        Builder<BlobStorageMaintenanceEndpointsOptionsBuilder, BlobStorageMaintenanceEndpointsOptions> optionsBuilder,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = optionsBuilder?.Invoke(new BlobStorageMaintenanceEndpointsOptionsBuilder()).Build();

        RegisterBlobStorageMaintenanceEndpoints(services, options, enabled);

        return services;
    }

    /// <summary>
    /// Registers Blob Storage maintenance endpoints with explicit options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional endpoint group options.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current service collection.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorageMaintenanceEndpoints(new BlobStorageMaintenanceEndpointsOptions());
    /// </code>
    /// </example>
    public static IServiceCollection AddBlobStorageMaintenanceEndpoints(
        this IServiceCollection services,
        BlobStorageMaintenanceEndpointsOptions options = null,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(services);

        RegisterBlobStorageMaintenanceEndpoints(services, options, enabled);

        return services;
    }

    /// <summary>
    /// Registers Blob Storage read-only content endpoints with a fluent options builder.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsBuilder">The endpoint options builder.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current service collection.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorageReadEndpoints(options => options.AllowAnonymous());
    /// </code>
    /// </example>
    public static IServiceCollection AddBlobStorageReadEndpoints(
        this IServiceCollection services,
        Builder<BlobStorageReadEndpointsOptionsBuilder, BlobStorageReadEndpointsOptions> optionsBuilder,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = optionsBuilder?.Invoke(new BlobStorageReadEndpointsOptionsBuilder()).Build();

        RegisterBlobStorageReadEndpoints(services, options, enabled);

        return services;
    }

    /// <summary>
    /// Registers Blob Storage read-only content endpoints with explicit options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional endpoint group options.</param>
    /// <param name="enabled">Indicates whether endpoint registration should be enabled.</param>
    /// <returns>The current service collection.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorageReadEndpoints(new BlobStorageReadEndpointsOptions());
    /// </code>
    /// </example>
    public static IServiceCollection AddBlobStorageReadEndpoints(
        this IServiceCollection services,
        BlobStorageReadEndpointsOptions options = null,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(services);

        RegisterBlobStorageReadEndpoints(services, options, enabled);

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

        RegisterFileStorageConsoleCommands(services, enabled);
        services.AddEndpoints<FileStorageEndpoints>(enabled);
    }

    private static void RegisterStoragePermalinkEndpoints(IServiceCollection services, StoragePermalinkEndpointsOptions options, bool enabled)
    {
        if (!enabled) return;
        if (options is not null) services.AddSingleton(options);
        services.AddEndpoints<StoragePermalinkEndpoints>(enabled);
    }

    private static void RegisterBlobStorageMaintenanceEndpoints(
        IServiceCollection services,
        BlobStorageMaintenanceEndpointsOptions options,
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

        RegisterBlobStorageConsoleCommands(services, enabled);
        services.AddEndpoints<BlobStorageMaintenanceEndpoints>(enabled);
    }

    private static void RegisterBlobStorageReadEndpoints(
        IServiceCollection services,
        BlobStorageReadEndpointsOptions options,
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

        RegisterBlobStorageConsoleCommands(services, enabled);
        services.AddEndpoints<BlobStorageReadEndpoints>(enabled);
    }

    private static void RegisterBlobStorageConsoleCommands(IServiceCollection services, bool enabled)
    {
        if (!enabled)
        {
            return;
        }

        services.TryAddEnumerable(ServiceDescriptor.Transient<IConsoleCommand, StorageBlobsConsoleCommand>());
    }

    private static void RegisterDocumentStorageConsoleCommands(IServiceCollection services, bool enabled)
    {
        if (!enabled)
        {
            return;
        }

        services.TryAddEnumerable(ServiceDescriptor.Transient<IConsoleCommand, StorageDocumentsConsoleCommand>());
    }

    private static void RegisterFileStorageConsoleCommands(IServiceCollection services, bool enabled)
    {
        if (!enabled)
        {
            return;
        }

        services.TryAddEnumerable(ServiceDescriptor.Transient<IConsoleCommand, StorageFilesConsoleCommand>());
    }
}
