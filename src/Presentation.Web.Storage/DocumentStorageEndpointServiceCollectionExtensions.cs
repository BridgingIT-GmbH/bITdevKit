// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web.Storage;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Registers optional Document Storage maintenance and content-read REST endpoints.
/// </summary>
/// <example><code>services.AddDocumentStorage().AddMaintenanceEndpoints();</code></example>
public static class DocumentStorageEndpointServiceCollectionExtensions
{
    /// <summary>
    /// Registers Document Storage maintenance endpoints with a fluent options builder.
    /// </summary>
    /// <param name="context">The document storage builder context.</param>
    /// <param name="optionsBuilder">The endpoint options builder.</param>
    /// <param name="enabled">Indicates whether endpoint registration is enabled.</param>
    /// <returns>The current document storage builder context.</returns>
    /// <example><code>services.AddDocumentStorage().AddMaintenanceEndpoints(options => options.GroupPath("/ops/documents").RequireAuthorization());</code></example>
    public static DocumentStorageBuilderContext AddMaintenanceEndpoints(
        this DocumentStorageBuilderContext context,
        Builder<DocumentStorageMaintenanceEndpointsOptionsBuilder, DocumentStorageMaintenanceEndpointsOptions> optionsBuilder,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = optionsBuilder?.Invoke(new DocumentStorageMaintenanceEndpointsOptionsBuilder()).Build();
        RegisterDocumentStorageMaintenanceEndpoints(context.Services, options, enabled);
        return context;
    }

    /// <summary>
    /// Registers Document Storage maintenance endpoints with explicit options.
    /// </summary>
    /// <param name="context">The document storage builder context.</param>
    /// <param name="options">Optional endpoint group options.</param>
    /// <param name="enabled">Indicates whether endpoint registration is enabled.</param>
    /// <returns>The current document storage builder context.</returns>
    /// <example><code>services.AddDocumentStorage().AddMaintenanceEndpoints(new DocumentStorageMaintenanceEndpointsOptions());</code></example>
    public static DocumentStorageBuilderContext AddMaintenanceEndpoints(
        this DocumentStorageBuilderContext context,
        DocumentStorageMaintenanceEndpointsOptions options,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        RegisterDocumentStorageMaintenanceEndpoints(context.Services, options, enabled);
        return context;
    }

    /// <summary>
    /// Registers Document Storage maintenance endpoints with default options.
    /// </summary>
    /// <param name="context">The document storage builder context.</param>
    /// <param name="enabled">Indicates whether endpoint registration is enabled.</param>
    /// <returns>The current document storage builder context.</returns>
    /// <example><code>services.AddDocumentStorage().AddMaintenanceEndpoints();</code></example>
    public static DocumentStorageBuilderContext AddMaintenanceEndpoints(
        this DocumentStorageBuilderContext context,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        RegisterDocumentStorageMaintenanceEndpoints(context.Services, null, enabled);
        return context;
    }

    /// <summary>
    /// Registers Document Storage maintenance endpoints with a fluent options builder.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsBuilder">The endpoint options builder.</param>
    /// <param name="enabled">Indicates whether endpoint registration is enabled.</param>
    /// <returns>The current service collection.</returns>
    /// <example><code>services.AddDocumentStorageMaintenanceEndpoints(options => options.GroupPath("/ops/documents").RequireAuthorization());</code></example>
    public static IServiceCollection AddDocumentStorageMaintenanceEndpoints(
        this IServiceCollection services,
        Builder<DocumentStorageMaintenanceEndpointsOptionsBuilder, DocumentStorageMaintenanceEndpointsOptions> optionsBuilder,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = optionsBuilder?.Invoke(new DocumentStorageMaintenanceEndpointsOptionsBuilder()).Build();
        RegisterDocumentStorageMaintenanceEndpoints(services, options, enabled);
        return services;
    }

    /// <summary>
    /// Registers Document Storage maintenance endpoints with explicit or default options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional endpoint group options.</param>
    /// <param name="enabled">Indicates whether endpoint registration is enabled.</param>
    /// <returns>The current service collection.</returns>
    /// <example><code>services.AddDocumentStorageMaintenanceEndpoints(new DocumentStorageMaintenanceEndpointsOptions());</code></example>
    public static IServiceCollection AddDocumentStorageMaintenanceEndpoints(
        this IServiceCollection services,
        DocumentStorageMaintenanceEndpointsOptions options = null,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(services);
        RegisterDocumentStorageMaintenanceEndpoints(services, options, enabled);
        return services;
    }

    /// <summary>
    /// Registers Document Storage read-only content endpoints with a fluent options builder.
    /// </summary>
    /// <param name="context">The document storage builder context.</param>
    /// <param name="optionsBuilder">The endpoint options builder.</param>
    /// <param name="enabled">Indicates whether endpoint registration is enabled.</param>
    /// <returns>The current document storage builder context.</returns>
    /// <example><code>services.AddDocumentStorage().AddReadEndpoints(options => options.GroupPath("/ops/documents").RequireAuthorization());</code></example>
    public static DocumentStorageBuilderContext AddReadEndpoints(
        this DocumentStorageBuilderContext context,
        Builder<DocumentStorageReadEndpointsOptionsBuilder, DocumentStorageReadEndpointsOptions> optionsBuilder,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = optionsBuilder?.Invoke(new DocumentStorageReadEndpointsOptionsBuilder()).Build();
        RegisterDocumentStorageReadEndpoints(context.Services, options, enabled);
        return context;
    }

    /// <summary>
    /// Registers Document Storage read-only content endpoints with explicit options.
    /// </summary>
    /// <param name="context">The document storage builder context.</param>
    /// <param name="options">Optional endpoint group options.</param>
    /// <param name="enabled">Indicates whether endpoint registration is enabled.</param>
    /// <returns>The current document storage builder context.</returns>
    /// <example><code>services.AddDocumentStorage().AddReadEndpoints(new DocumentStorageReadEndpointsOptions());</code></example>
    public static DocumentStorageBuilderContext AddReadEndpoints(
        this DocumentStorageBuilderContext context,
        DocumentStorageReadEndpointsOptions options,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        RegisterDocumentStorageReadEndpoints(context.Services, options, enabled);
        return context;
    }

    /// <summary>
    /// Registers Document Storage read-only content endpoints with default options.
    /// </summary>
    /// <param name="context">The document storage builder context.</param>
    /// <param name="enabled">Indicates whether endpoint registration is enabled.</param>
    /// <returns>The current document storage builder context.</returns>
    /// <example><code>services.AddDocumentStorage().AddReadEndpoints();</code></example>
    public static DocumentStorageBuilderContext AddReadEndpoints(
        this DocumentStorageBuilderContext context,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        RegisterDocumentStorageReadEndpoints(context.Services, null, enabled);
        return context;
    }

    /// <summary>
    /// Registers Document Storage read-only content endpoints with a fluent options builder.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="optionsBuilder">The endpoint options builder.</param>
    /// <param name="enabled">Indicates whether endpoint registration is enabled.</param>
    /// <returns>The current service collection.</returns>
    /// <example><code>services.AddDocumentStorageReadEndpoints(options => options.GroupPath("/ops/documents").RequireAuthorization());</code></example>
    public static IServiceCollection AddDocumentStorageReadEndpoints(
        this IServiceCollection services,
        Builder<DocumentStorageReadEndpointsOptionsBuilder, DocumentStorageReadEndpointsOptions> optionsBuilder,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = optionsBuilder?.Invoke(new DocumentStorageReadEndpointsOptionsBuilder()).Build();
        RegisterDocumentStorageReadEndpoints(services, options, enabled);
        return services;
    }

    /// <summary>
    /// Registers Document Storage read-only content endpoints with explicit or default options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional endpoint group options.</param>
    /// <param name="enabled">Indicates whether endpoint registration is enabled.</param>
    /// <returns>The current service collection.</returns>
    /// <example><code>services.AddDocumentStorageReadEndpoints(new DocumentStorageReadEndpointsOptions());</code></example>
    public static IServiceCollection AddDocumentStorageReadEndpoints(
        this IServiceCollection services,
        DocumentStorageReadEndpointsOptions options = null,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(services);
        RegisterDocumentStorageReadEndpoints(services, options, enabled);
        return services;
    }

    private static void RegisterDocumentStorageMaintenanceEndpoints(
        IServiceCollection services,
        DocumentStorageMaintenanceEndpointsOptions options,
        bool enabled)
    {
        if (!enabled)
        {
            return;
        }

        if (options is null) services.TryAddSingleton(new DocumentStorageMaintenanceEndpointsOptions());
        else services.AddSingleton(options);

        services.AddEndpoints<DocumentStorageMaintenanceEndpoints>(enabled);
    }

    private static void RegisterDocumentStorageReadEndpoints(
        IServiceCollection services,
        DocumentStorageReadEndpointsOptions options,
        bool enabled)
    {
        if (!enabled)
        {
            return;
        }

        if (options is null) services.TryAddSingleton(new DocumentStorageReadEndpointsOptions());
        else services.AddSingleton(options);

        services.AddEndpoints<DocumentStorageReadEndpoints>(enabled);
    }
}
