// File: BridgingIT.DevKit.Application.FileMonitoring/FileMonitoringBuilderContext.cs
namespace BridgingIT.DevKit.Application.FileMonitoring;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Context returned by AddFileMonitoring to allow further configuration chaining.
/// </summary>
public class FileMonitoringBuilderContext
{
    private readonly IServiceCollection services;

    internal FileMonitoringBuilderContext(IServiceCollection services) => this.services = services;

    /// <summary>
    /// Provides access to the underlying IServiceCollection for additional service registrations.
    /// </summary>
    public IServiceCollection Services => this.services;
}