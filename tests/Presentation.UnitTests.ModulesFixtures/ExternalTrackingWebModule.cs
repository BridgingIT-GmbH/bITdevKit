namespace BridgingIT.DevKit.Presentation.UnitTests.ModulesFixtures;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Records module lifecycle calls so consumers in another assembly can verify instance reuse and enablement.
/// </summary>
public sealed class ExternalTrackingWebModule : WebModuleBase
{
    /// <summary>
    ///     Gets the identifier of this module instance.
    /// </summary>
    public Guid InstanceId { get; } = Guid.NewGuid();

    /// <summary>
    ///     Gets the instance identifier observed during registration.
    /// </summary>
    public Guid? RegisterInstanceId { get; private set; }

    /// <summary>
    ///     Gets the instance identifier observed during middleware configuration.
    /// </summary>
    public Guid? UseInstanceId { get; private set; }

    /// <summary>
    ///     Gets the instance identifier observed during endpoint mapping.
    /// </summary>
    public Guid? MapInstanceId { get; private set; }

    /// <summary>
    ///     Gets the number of registration calls.
    /// </summary>
    public int RegisterCount { get; private set; }

    /// <summary>
    ///     Gets the number of middleware configuration calls.
    /// </summary>
    public int UseCount { get; private set; }

    /// <summary>
    ///     Gets the number of endpoint mapping calls.
    /// </summary>
    public int MapCount { get; private set; }

    /// <summary>
    ///     Gets whether the module was enabled during registration.
    /// </summary>
    public bool? EnabledDuringRegister { get; private set; }

    /// <summary>
    ///     Gets whether the module was enabled during middleware configuration.
    /// </summary>
    public bool? EnabledDuringUse { get; private set; }

    /// <summary>
    ///     Gets whether the module was enabled during endpoint mapping.
    /// </summary>
    public bool? EnabledDuringMap { get; private set; }

    /// <inheritdoc />
    public override IServiceCollection Register(
        IServiceCollection services,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        this.RegisterCount++;
        this.RegisterInstanceId = this.InstanceId;
        this.EnabledDuringRegister = this.Enabled;
        return services;
    }

    /// <inheritdoc />
    public override IApplicationBuilder Use(
        IApplicationBuilder app,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        this.UseCount++;
        this.UseInstanceId = this.InstanceId;
        this.EnabledDuringUse = this.Enabled;
        return app;
    }

    /// <inheritdoc />
    public override IEndpointRouteBuilder Map(
        IEndpointRouteBuilder app,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        this.MapCount++;
        this.MapInstanceId = this.InstanceId;
        this.EnabledDuringMap = this.Enabled;
        return app;
    }
}
