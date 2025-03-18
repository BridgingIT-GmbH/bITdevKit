// File: BridgingIT.DevKit.Infrastructure.EntityFramework/FileMonitoringServiceCollectionExtensions.cs
namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Application.FileMonitoring;
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
        this FileMonitoringBuilderContext context)
        where TContext : DbContext, IFileMonitoringContext
    {
        EnsureArg.IsNotNull(context, nameof(context));

        // Replace any existing IFileEventStore registration (e.g., default InMemoryFileEventStore)
        context.Services.RemoveAll<IFileEventStore>();
        context.Services.AddScoped<IFileEventStore, EntityFrameworkFileEventStore<TContext>>();

        return context;
    }
}