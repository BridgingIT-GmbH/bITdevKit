// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Storage;

/// <summary>
/// Provides Entity Framework blob-store service registration extensions.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithEntityFrameworkClient&lt;AppDbContext&gt;("reports");
/// </code>
/// </example>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers an Entity Framework backed blob-store client within a top-level blob-storage registration flow.
    /// </summary>
    /// <typeparam name="TContext">The EF Core context that implements <see cref="IBlobStoreContext" />.</typeparam>
    /// <param name="context">The blob-storage builder context.</param>
    /// <param name="configure">The optional per-client blob-store options callback.</param>
    /// <param name="lifetime">The optional client lifetime override.</param>
    /// <returns>The current blob-storage builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage()
    ///     .WithEntityFrameworkClient&lt;AppDbContext&gt;(options => options.MaxBlobSize = 1048576);
    /// </code>
    /// </example>
    public static BlobStorageBuilderContext WithEntityFrameworkClient<TContext>(
        this BlobStorageBuilderContext context,
        Action<BlobStoreOptions> configure = null,
        ServiceLifetime? lifetime = null)
        where TContext : DbContext, IBlobStoreContext
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.WithEntityFrameworkClient<TContext>("default", configure, lifetime);
    }

    /// <summary>
    /// Registers an Entity Framework backed blob-store client within a top-level blob-storage registration flow.
    /// </summary>
    /// <typeparam name="TContext">The EF Core context that implements <see cref="IBlobStoreContext" />.</typeparam>
    /// <param name="context">The blob-storage builder context.</param>
    /// <param name="name">The unique store/client name.</param>
    /// <param name="configure">The optional per-client blob-store options callback.</param>
    /// <param name="lifetime">The optional client lifetime override.</param>
    /// <returns>The current blob-storage builder context.</returns>
    /// <example>
    /// <code>
    /// services.AddBlobStorage()
    ///     .WithEntityFrameworkClient&lt;AppDbContext&gt;("reports", options => options.MaxBlobSize = 1048576);
    /// </code>
    /// </example>
    public static BlobStorageBuilderContext WithEntityFrameworkClient<TContext>(
        this BlobStorageBuilderContext context,
        string name,
        Action<BlobStoreOptions> configure = null,
        ServiceLifetime? lifetime = null)
        where TContext : DbContext, IBlobStoreContext
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (!typeof(IBlobStoreContext).IsAssignableFrom(typeof(TContext)))
        {
            throw new InvalidOperationException(
                $"Blob storage Entity Framework context '{typeof(TContext).Name}' must implement {nameof(IBlobStoreContext)}.");
        }

        return context.RegisterClient(
            name,
            (sp, options) => new EntityFrameworkBlobStoreProvider<TContext>(
                sp.GetRequiredService<IServiceScopeFactory>(),
                options,
                sp.GetService<IContinuationTokenProtector>(),
                sp.GetService<System.Diagnostics.Metrics.IMeterFactory>(),
                sp.GetService<ILoggerFactory>(),
                name),
            configure,
            EntityFrameworkBlobStoreProvider<TContext>.ProviderName,
            EntityFrameworkBlobStoreProvider<TContext>.CreateCapabilities(),
            lifetime);
    }
}
