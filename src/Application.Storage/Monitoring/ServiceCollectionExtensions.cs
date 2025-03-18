// File: BridgingIT.DevKit.Application.FileMonitoring/ServiceCollectionExtensions.cs
namespace BridgingIT.DevKit.Application.FileMonitoring;

using System;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// Provides extension methods for IServiceCollection to configure the FileMonitoring system.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the FileMonitoring system to the service collection with a fluent configuration API.
    /// Registers FileMonitoringService as a singleton and allows customization via a builder.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="configure">An action to configure the FileMonitoringBuilder.</param>
    /// <returns>A FileMonitoringBuilderContext for further configuration chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddFileMonitoring(monitoring =>
    /// {
    ///     monitoring
    ///         .WithBehavior<LoggingBehavior>() // monitoring behavior for all locations
    ///         .UseLocal("Docs", "C:\\Docs", options =>
    ///         {
    ///             options.FilePattern = "*.txt";
    ///             options.UseOnDemandOnly();
    ///             options.WithProcessorBehavior<LoggingProcessorBehavior>();
    ///             options.UseProcessor<FileMoverProcessor>(config =>
    ///                 config.WithConfiguration(p => ((FileMoverProcessor)p).DestinationRoot = "C:\\MovedDocs")) // configure processor instance
    ///                 .WithBehavior<RetryProcessorBehavior>();
    ///          })
    ///         .UseLocal("OtherDocs", "C:\\OtherDocs", options =>
    ///         {
    ///             options.FilePattern = "*.pdf";
    ///             options.UseProcessor<FileLoggerProcessor>();
    ///             options.UseProcessor<FileMoverProcessor>(config =>
    ///                 config.WithConfiguration(p => ((FileMoverProcessor)p).DestinationRoot = "C:\\OtherMovedDocs")); // configure processor instance
    ///         });
    /// })
    /// .WithEntityFrameworkStore<MyAppDbContext>(); // Extension from EF infra project
    /// 
    /// </code>
    /// </example>
    public static FileMonitoringBuilderContext AddFileMonitoring(
        this IServiceCollection services,
        Action<FileMonitoringBuilder> configure)
    {
        EnsureArg.IsNotNull(services, nameof(services));
        EnsureArg.IsNotNull(configure, nameof(configure));

        services.TryAddSingleton<IFileMonitoringService, FileMonitoringService>();
        services.TryAddScoped<IFileEventStore, InMemoryFileEventStore>(); // Default store

        var builder = new FileMonitoringBuilder(services);
        configure(builder);

        return new FileMonitoringBuilderContext(services);
    }
}