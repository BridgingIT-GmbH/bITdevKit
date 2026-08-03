// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Provides Entity Framework Storage Permalink Registry registration.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Uses an application <see cref="DbContext" /> as the persistent permalink registry.
    /// </summary>
    /// <typeparam name="TContext">
    /// The registered context implementing <see cref="IStoragePermalinkRegistryContext" />.
    /// </typeparam>
    /// <param name="context">
    /// The permalink builder context.
    /// </param>
    /// <returns>
    /// The same builder context.
    /// </returns>
    /// <example>
    /// <code>
    /// services.AddStoragePermalinks().UseEntityFramework&lt;AppDbContext&gt;();
    /// </code>
    /// </example>
    public static StoragePermalinkBuilderContext UseEntityFramework<TContext>(this StoragePermalinkBuilderContext context)
        where TContext : DbContext, IStoragePermalinkRegistryContext
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.Replace(ServiceDescriptor.Singleton<IStoragePermalinkRegistryProvider>(sp =>
            new EntityFrameworkStoragePermalinkRegistryProvider<TContext>(sp.GetRequiredService<IServiceScopeFactory>(), sp.GetService<TimeProvider>())));
        return context;
    }
}
