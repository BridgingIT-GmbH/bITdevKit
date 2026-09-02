// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using AspNetCore.Hosting;
using BridgingIT.DevKit.Common;
using Configuration;

/// <summary>
///     Provides context for building modules with dependencies and configuration.
/// </summary>
/// <example>
/// <code>
/// services.AddModules(configuration, modules =&gt; modules.WithModule&lt;CoreModule&gt;());
/// </code>
/// </example>
public class ModuleBuilderContext
{
    /// <summary>
    ///     Initializes a module builder context for one service collection.
    /// </summary>
    /// <param name="services">The service collection that owns the module registry.</param>
    /// <param name="configuration">The configuration supplied to module registration.</param>
    /// <param name="environment">The web hosting environment supplied to module registration.</param>
    /// <example>
    /// <code>
    /// var context = new ModuleBuilderContext(services, configuration, environment);
    /// context.WithModule&lt;CoreModule&gt;();
    /// </code>
    /// </example>
    public ModuleBuilderContext(
        IServiceCollection services,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        this.Services = services;
        this.Configuration = configuration;
        this.Environment = environment;
        EnsureRegistry(services);
    }

    /// <summary>
    ///     Gets the collection of services that can be used to configure the application's dependencies.
    /// </summary>
    /// <value>
    ///     An instance of <see cref="IServiceCollection" /> which holds the service descriptors.
    /// </value>
    /// <example>
    /// <code>
    /// context.Services.AddSingleton&lt;CustomerService&gt;();
    /// </code>
    /// </example>
    public IServiceCollection Services { get; }

    /// <summary>
    ///     Gets the configuration settings for the module.
    ///     Provides access to application configuration such as settings from appsettings.json or environment variables.
    /// </summary>
    /// <example>
    /// <code>
    /// var enabled = context.Configuration?["Modules:customer:Enabled"];
    /// </code>
    /// </example>
    public IConfiguration Configuration { get; }

    /// <summary>
    ///     Gets the web hosting environment available during module registration.
    /// </summary>
    /// <example>
    /// <code>
    /// if (context.Environment?.EnvironmentName == "Development")
    /// {
    ///     context.Services.AddSingleton&lt;DevelopmentOnlyService&gt;();
    /// }
    /// </code>
    /// </example>
    public IWebHostEnvironment Environment { get; }

    private static void EnsureRegistry(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var registry = services
            .Where(descriptor => descriptor.ServiceType == typeof(ModuleRegistry))
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<ModuleRegistry>()
            .LastOrDefault();

        if (registry is not null)
        {
            return;
        }

        registry = new ModuleRegistry();
        services.AddSingleton(registry);
        services.AddSingleton<IModuleRegistry>(provider => provider.GetRequiredService<ModuleRegistry>());
    }
}
