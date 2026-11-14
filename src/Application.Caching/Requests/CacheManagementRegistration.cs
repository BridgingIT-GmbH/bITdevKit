// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Registers only the common cache-management requester boundary, independently of every presentation transport.</summary>
/// <example><code>services.AddApplicationCaching().WithManagementRequests();</code></example>
public static class CacheManagementRegistration
{
    /// <summary>Adds bounded management adapters/handlers without routes, source workers, Console, Dashboard, or MCP discovery.</summary>
    public static ApplicationCachingBuilderContext WithManagementRequests(this ApplicationCachingBuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var services = context.Services;
        services.AddLogging();
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IRequester))) { services.AddRequester(); }

        services.TryAddSingleton<CacheCollectionAccessRegistry>();
        services.TryAddSingleton<CacheManagementAccessValidator>();
        services.TryAddScoped<CacheManagementHandler>();
        services.TryAddScoped<IRequestHandler<CacheDiagnosticsQuery, CacheDiagnosticsReport>, CacheManagementHandler>();
        services.TryAddScoped<IRequestHandler<CacheMetricHistoryQueryRequest, CacheMetricHistoryResult>, CacheManagementHandler>();
        services.TryAddScoped<IRequestHandler<CacheMetricSeriesQuery, IReadOnlyList<CacheMetricSeriesDescriptor>>, CacheManagementHandler>();
        services.TryAddScoped<IRequestHandler<CacheCollectionsQuery, IReadOnlyList<CacheCollectionInfo>>, CacheManagementHandler>();
        services.TryAddScoped<IRequestHandler<CacheEntriesQuery, CacheEntriesPage>, CacheManagementHandler>();
        services.TryAddScoped<IRequestHandler<CacheEntryMetadataQuery, CacheLookup<CacheEntryMetadata>>, CacheManagementHandler>();
        services.TryAddScoped<IRequestHandler<CacheEntryReadQuery, CacheLookup<ICacheReadHandle>>, CacheManagementHandler>();
        services.TryAddScoped<IRequestHandler<CacheEntryWriteCommand, CacheWriteOutcome>, CacheManagementHandler>();
        services.TryAddScoped<IRequestHandler<CacheEntryRemoveCommand, bool>, CacheManagementHandler>();
        services.TryAddScoped<IRequestHandler<CacheInvalidationPreviewQuery, CacheInvalidationPreview>, CacheManagementHandler>();
        services.TryAddScoped<IRequestHandler<CacheInvalidateCommand, IReadOnlyList<Result<CacheOperationSubmission>>>, CacheManagementHandler>();
        services.TryAddScoped<IRequestHandler<CacheOperationCommand, CacheProviderOperation>, CacheManagementHandler>();
        services.TryAddScoped<IRequestHandler<CacheBackgroundCommand, CacheBackgroundState>, CacheManagementHandler>();
        return context;
    }
}