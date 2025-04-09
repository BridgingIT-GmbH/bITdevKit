// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Application.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Provides extension methods for configuring Entity Framework Core storage in the FileMonitoring system.
/// </summary>
public static class FileMonitoringBuilderExtensions
{
    /// <summary>
    /// Configures the FileMonitoring system to use an Entity Framework Core event store with the specified DbContext.
    /// The DbContext must implement IFileMonitoringContext and be registered separately (e.g., via AddDbContext).
    /// </summary>
    /// <typeparam name="TContext">The DbContext type implementing IFileMonitoringContext.</typeparam>
    /// <param name="context">The FileMonitoringBuilderContext returned from AddFileMonitoring.</param>
    /// <returns>The FileMonitoringBuilderContext for further configuration chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddFileMonitoring(monitoring =>
    /// {
    ///     monitoring
    ///         .WithBehavior<LoggingBehavior>()
    ///         .UseLocal("Docs", "C:\\Docs", options =>
    ///         {
    ///             options.FilePattern = "*.txt";
    ///             options.UseProcessor<FileLoggerProcessor>();
    ///         });
    /// })
    /// .WithEntityFrameworkStore<MyAppDbContext>(); // Use EF Core store
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException">Thrown if the context is null.</exception>
    public static FileMonitoringBuilderContext WithEntityFrameworkStore<TContext>(
        this FileMonitoringBuilderContext context,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TContext : DbContext, IFileMonitoringContext
    {
        EnsureArg.IsNotNull(context, nameof(context));

        // Replace any existing IFileEventStore registration (e.g., default InMemoryFileEventStore)
        context.Services.RemoveAll<IFileEventStore>();
        if (lifetime == ServiceLifetime.Transient)
        {
            context.Services.TryAddTransient<IFileEventStore, EntityFrameworkFileEventStore<TContext>>();
        }
        else if (lifetime == ServiceLifetime.Singleton)
        {
            context.Services.TryAddSingleton<IFileEventStore, EntityFrameworkFileEventStore<TContext>>();
        }
        else
        {
            context.Services.TryAddScoped<IFileEventStore, EntityFrameworkFileEventStore<TContext>>(); // Default
        }

        return context;
    }
}