// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web.Caching.Dashboard;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using BridgingIT.DevKit.Presentation.Web.Caching;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Registers independent optional presentation capabilities for advanced Application Caching.</summary>
/// <example><code>services.AddApplicationCaching().WithEndpoints();</code></example>
public static class CachePresentationRegistration
{
    /// <summary>Registers the external cache HTTP endpoints and their Application management requests.</summary>
    /// <param name="context">The application-cache startup builder.</param>
    /// <param name="configure">Optional HTTP route, authorization, response, and request-limit settings.</param>
    /// <returns>The same startup builder.</returns>
    /// <remarks>
    /// Map the host's registered endpoints after authentication and authorization middleware. Collection exposure
    /// settings cannot register this endpoint group. Dashboard, Console, and MCP access require their own registrations.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <example><code>cache.WithEndpoints(options => options.WithReadPolicy("Cache.Read"));</code></example>
    public static ApplicationCachingBuilderContext WithEndpoints(this ApplicationCachingBuilderContext context,
        Action<CacheEndpointsOptionsBuilder> configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var builder = new CacheEndpointsOptionsBuilder();
        configure?.Invoke(builder);
        context.WithIntegration("Endpoints", builder.Build()).WithManagementRequests();
        EnsureWebServices(context.Services);
        context.Services.AddEndpoints<CacheEndpoints>();
        CacheHttpErrorMapping.Register();
        return context;
    }

    /// <summary>Opts one collection out of the external API without affecting in-process access or other management transports.</summary>
    /// <example><code>cache.WithCollection&lt;ProductCard&gt;("private-cards", collection => collection.WithoutEndpoints());</code></example>
    public static CacheCollectionBuilder WithoutEndpoints(this CacheCollectionBuilder builder) =>
        builder.WithExtension("Endpoints", "Enabled", "false");

    /// <summary>Explicitly declares HTTP exposure without registering an external endpoint group.</summary>
    /// <param name="builder">The collection policy builder.</param>
    /// <param name="enabled">Whether this collection permits HTTP access after global endpoint registration.</param>
    /// <returns>The same collection builder.</returns>
    /// <example><code>collection.WithEndpoints(false);</code></example>
    public static CacheCollectionBuilder WithEndpoints(this CacheCollectionBuilder builder, bool enabled) =>
        builder.WithExtension("Endpoints", "Enabled", enabled.ToString());

    /// <summary>Registers a bounded public JSON input DTO and its mapping to a declared cache value contract.</summary>
    /// <typeparam name="TValue">The code-declared cache value type.</typeparam>
    /// <typeparam name="TModel">The public request DTO.</typeparam>
    /// <param name="context">The application-cache startup builder.</param>
    /// <param name="contract">The code-registered value-contract alias whose collections accept this public model.</param>
    /// <param name="map">Maps an admitted input DTO to the cache value. Runs only after its size estimate passes.</param>
    /// <param name="estimate">
    /// Estimates the complete deserialized <typeparamref name="TModel"/> graph in bytes, including nested objects,
    /// collections, and strings. This estimates the input DTO, not the mapped <typeparamref name="TValue"/>.
    /// Returning null, a negative value, or a value above <paramref name="maximumDecodedBytes"/> produces a
    /// <see cref="CacheErrorCode.Capacity"/> failure without invoking <paramref name="map"/>.
    /// </param>
    /// <param name="maximumBodyBytes">The positive serialized JSON body limit. Defaults to 65,536 bytes, or 64 KiB.</param>
    /// <param name="maximumDecodedBytes">The positive input DTO graph limit. Defaults to 1,048,576 bytes, or 1 MiB.</param>
    /// <returns>The same startup builder.</returns>
    /// <remarks>
    /// Requests use application/json and System.Text.Json web defaults with a maximum depth of 32. These settings
    /// are independent of host JSON settings and the value contract's codec options. If JSON null deserializes to
    /// <typeparamref name="TModel"/>, the estimator receives null and the mapper receives null if that estimate passes.
    /// Each callback must handle or reject null deliberately.
    /// Startup validates that the contract is typed, that its value type matches <typeparamref name="TValue"/>, and that
    /// both limits fit every collection using the contract. Oversized bodies or unavailable decode capacity fail with
    /// <see cref="CacheErrorCode.Capacity"/>. Malformed JSON fails with <see cref="CacheErrorCode.Validation"/>.
    /// This registration does not enable HTTP endpoints.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="context"/>, <paramref name="map"/>, or <paramref name="estimate"/> is null.</exception>
    /// <exception cref="ArgumentException">The contract alias is invalid or already has a public HTTP model.</exception>
    /// <exception cref="InvalidOperationException">Public HTTP model registration has already been finalized.</exception>
    /// <example><code>
    /// cache.WithHttpModel&lt;ReportSummary, ReportSummaryInput&gt;("report-summary",
    ///     input =&gt; input is null ? null : new ReportSummary(input.Title, input.Rows),
    ///     input =&gt; input is null ? 0L : 256L + (input.Title?.Length ?? 0) * 2L);
    /// </code></example>
    public static ApplicationCachingBuilderContext WithHttpModel<TValue, TModel>(this ApplicationCachingBuilderContext context,
        string contract, Func<TModel, TValue> map, Func<TModel, long?> estimate, int maximumBodyBytes = 65536, long maximumDecodedBytes = 1048576)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(estimate);
        GetWriteProfiles(context.Services).Add(contract, new CacheJsonHttpWriteProfile<TValue, TModel>(map, estimate, maximumBodyBytes, maximumDecodedBytes));
        return context;
    }

    private static void EnsureWebServices(IServiceCollection services)
    {
        services.TryAddSingleton<CacheWebConfiguration>();
        services.TryAddSingleton<CacheHttpRequestReader>();
        services.TryAddScoped<CacheHttpResultMapper>();
        services.TryAddSingleton<ICacheHttpAccessContextFactory, CacheHttpAccessContextFactory>();
        _ = GetWriteProfiles(services);
    }

    private static CacheHttpWriteProfiles GetWriteProfiles(IServiceCollection services)
    {
        var previous = services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(CacheHttpWriteProfiles))?.ImplementationInstance as CacheHttpWriteProfiles;
        if (previous is not null) { return previous; }

        var profiles = new CacheHttpWriteProfiles();
        services.AddSingleton(profiles);
        return profiles;
    }

    /// <summary>Enables only the caching Dashboard pages within a separately registered Dashboard host.</summary>
    /// <remarks>An omitted or disabled Dashboard host contributes no caching routes, pages, or management authority.</remarks>
    /// <example><code>services.AddDashboard(options => options.RequireAuthorization()); cache.WithDashboard();</code></example>
    public static ApplicationCachingBuilderContext WithDashboard(this ApplicationCachingBuilderContext context,
        Action<CacheDashboardOptionsBuilder> configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var builder = new CacheDashboardOptionsBuilder();
        configure?.Invoke(builder);
        context.WithIntegration("Dashboard", builder.Build()).WithManagementRequests();
        context.Services.TryAddSingleton<BridgingIT.DevKit.Presentation.Web.Caching.CacheWebConfiguration>();
        BridgingIT.DevKit.Presentation.Web.Caching.CacheHttpErrorMapping.Register();
        context.Services.AddAntiforgery();
        context.Services.TryAddSingleton<CacheDashboardConfiguration>();
        context.Services.TryAddSingleton<ICacheDashboardAccessContextFactory>(services => new CacheDashboardAccessContextFactory(
            services.GetRequiredService<CacheCatalog>(), services.GetService<DashboardEndpointsOptions>() ?? new() { Enabled = false },
            services.GetRequiredService<CacheDashboardConfiguration>()));
        context.Services.TryAddScoped<CacheDashboardViewModelBuilder>();
        context.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IDashboardPageProvider, BridgingIT.DevKit.Presentation.Web.Caching.Dashboard.DashboardPageProvider>());
        context.Services.AddEndpoints<BridgingIT.DevKit.Presentation.Web.Caching.Dashboard.DashboardEndpoints>();
        return context;
    }

    /// <summary>Registers only the application-caching Console command and its trusted management boundary.</summary>
    /// <param name="context">The advanced cache startup builder.</param>
    /// <returns>The same startup builder.</returns>
    /// <example><code>services.AddApplicationCaching().WithConsoleCommands().WithManagementAccessContext&lt;OperationsAccessContextFactory&gt;();</code></example>
    public static ApplicationCachingBuilderContext WithConsoleCommands(this ApplicationCachingBuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Services.Any(d => d.ServiceType == typeof(IConsoleCommand) && d.ImplementationType == typeof(ApplicationCachingConsoleCommand))) { return context; }

        context.WithIntegration("ConsoleCommands", new Dictionary<string, string> { ["Enabled"] = "true" }).WithManagementRequests();
        AddManagementServices(context.Services);
        context.Services.TryAddEnumerable(ServiceDescriptor.Transient<IConsoleCommand, ApplicationCachingConsoleCommand>());
        return context;
    }

    /// <summary>Registers only application-cache MCP discovery and the existing host MCP dispatch contract.</summary>
    /// <param name="context">The advanced cache startup builder.</param>
    /// <returns>The same startup builder.</returns>
    /// <example><code>services.AddApplicationCaching().WithMcpHandlers().WithManagementAccessContext&lt;OperationsAccessContextFactory&gt;();</code></example>
    public static ApplicationCachingBuilderContext WithMcpHandlers(this ApplicationCachingBuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Services.Any(d => d.ServiceType == typeof(BridgingIT.DevKit.Common.IMcpHandler) && d.ImplementationType == typeof(ApplicationCachingMcpHandler))) { return context; }

        context.WithIntegration("Mcp", new Dictionary<string, string> { ["Enabled"] = "true" }).WithManagementRequests();
        AddManagementServices(context.Services);
        context.Services.AddMcpHandler<ApplicationCachingMcpHandler>();
        return context;
    }

    /// <summary>Registers a current host identity/action authorization factory shared by explicitly enabled Console and MCP adapters.</summary>
    /// <typeparam name="TFactory">The host-approved scoped authorization implementation.</typeparam>
    /// <param name="context">The advanced cache startup builder.</param>
    /// <returns>The same startup builder without enabling a management transport.</returns>
    /// <example><code>builder.WithManagementAccessContext&lt;OperationsAccessContextFactory&gt;();</code></example>
    public static ApplicationCachingBuilderContext WithManagementAccessContext<TFactory>(this ApplicationCachingBuilderContext context)
        where TFactory : class, ICacheManagementAccessContextFactory
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.Replace(ServiceDescriptor.Scoped<ICacheManagementAccessContextFactory, TFactory>());
        return context;
    }

    /// <summary>Declares independent Console exposure for one collection without registering Console discovery.</summary>
    /// <param name="builder">The collection policy builder.</param>
    /// <param name="enabled">Whether this collection permits Console management after global registration.</param>
    /// <returns>The same collection builder.</returns>
    /// <example><code>collection.WithConsoleCommands(false);</code></example>
    public static CacheCollectionBuilder WithConsoleCommands(this CacheCollectionBuilder builder, bool enabled) =>
        builder.WithExtension("ConsoleCommands", "Enabled", enabled.ToString());

    /// <summary>Declares independent MCP exposure for one collection without registering MCP discovery.</summary>
    /// <param name="builder">The collection policy builder.</param>
    /// <param name="enabled">Whether this collection permits MCP management after global registration.</param>
    /// <returns>The same collection builder.</returns>
    /// <example><code>collection.WithMcpHandlers(false);</code></example>
    public static CacheCollectionBuilder WithMcpHandlers(this CacheCollectionBuilder builder, bool enabled) =>
        builder.WithExtension("Mcp", "Enabled", enabled.ToString());

    private static void AddManagementServices(IServiceCollection services)
    {
        services.TryAddSingleton<CacheManagementConfiguration>();
        services.TryAddScoped<ICacheManagementAccessContextFactory, CacheDeniedManagementAccessContextFactory>();
        services.TryAddScoped<CacheManagementDispatcher>();
    }
}