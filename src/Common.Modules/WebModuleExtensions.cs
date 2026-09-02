// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Reflection;
using AspNetCore.Builder;
using AspNetCore.Hosting;
using AspNetCore.Mvc.ApplicationParts;
using AspNetCore.Mvc.Controllers;
using AspNetCore.Routing;
using BridgingIT.DevKit.Common;
using Configuration;
using Serilog;

/// <summary>
///     Provides extension methods for web modules, allowing for the integration
///     and mapping of module services and controllers within an application's service collection
///     and endpoint route builder.
/// </summary>
/// <example>
/// <code>
/// var app = builder.Build();
/// app.MapModules();
/// </code>
/// </example>
public static class WebModuleExtensions
{
    /// <summary>
    ///     Maps the modules to the given <see cref="IEndpointRouteBuilder" />.
    /// </summary>
    /// <param name="app">The <see cref="IEndpointRouteBuilder" /> to map the modules to.</param>
    /// <param name="configuration">The configuration settings for the modules.</param>
    /// <param name="environment">The web hosting environment.</param>
    /// <returns>The <see cref="IEndpointRouteBuilder" /> with the modules mapped.</returns>
    /// <exception cref="InvalidOperationException">The endpoint builder's services do not contain a module registry.</exception>
    /// <example>
    /// <code>
    /// app.MapModules();
    /// </code>
    /// </example>
    public static IEndpointRouteBuilder MapModules(
        this IEndpointRouteBuilder app,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        ArgumentNullException.ThrowIfNull(app);

        var registry = app.ServiceProvider.GetService<ModuleRegistry>() ??
            throw new InvalidOperationException(
                "No host-scoped module registry was found. Register modules with services.AddModules() before building the application.");

        foreach (var module in registry.Modules.OfType<IWebModule>())
        {
            Log.Logger.Information(
                "[{LogKey}] map (module={ModuleName}, enabled={ModuleEnabled}, priority={ModulePriority}) ",
                ModuleConstants.LogKey,
                module.Name,
                module.Enabled,
                module.Priority);
            module.Map(app, configuration, environment);
        }

        return app;
    }

    /// <summary>
    /// Maps registered web modules using the application's configuration and environment.
    /// </summary>
    /// <param name="app">The web application to map module endpoints on.</param>
    /// <returns>The same web application instance.</returns>
    /// <example>
    /// <code>
    /// var app = builder.Build();
    /// app.MapModules();
    /// </code>
    /// </example>
    public static WebApplication MapModules(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        MapModules(app, app.Configuration, app.Environment);

        return app;
    }

    /// <summary>
    ///     Adds services for module controllers to the <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="context">The <see cref="ModuleBuilderContext" /> for configuring the module.</param>
    /// <param name="optionsAction">An optional <see cref="Action{IMvcBuilder}" /> to configure the MVC services.</param>
    /// <returns>The modified <see cref="ModuleBuilderContext" />.</returns>
    /// <example>
    /// <code>
    /// modules.WithModuleControllers();
    /// </code>
    /// </example>
    public static ModuleBuilderContext WithModuleControllers(
        this ModuleBuilderContext context,
        Action<IMvcBuilder> optionsAction = null)
    {
        return context.WithModuleControllers([], optionsAction);
    }

    /// <summary>
    ///     Adds services for module controllers to the <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="context">The context for building modules with dependencies and configuration.</param>
    /// <param name="optionsAction">Optional action for configuring MVC builder.</param>
    /// <typeparam name="T">The type whose assembly to include for module controllers.</typeparam>
    /// <returns>The updated <see cref="ModuleBuilderContext" />.</returns>
    /// <example>
    /// <code>
    /// modules.WithModuleControllers&lt;CustomerController&gt;();
    /// </code>
    /// </example>
    public static ModuleBuilderContext WithModuleControllers<T>(
        this ModuleBuilderContext context,
        Action<IMvcBuilder> optionsAction = null)
    {
        return context.WithModuleControllers([typeof(T).Assembly], optionsAction);
    }

    /// <summary>
    ///     Adds services for module controllers to the <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="context">The context for building modules.</param>
    /// <param name="assemblies">The assemblies to add controllers from.</param>
    /// <param name="optionsAction">An optional action to configure the MVC builder.</param>
    /// <returns>The updated <see cref="ModuleBuilderContext" />.</returns>
    /// <example>
    /// <code>
    /// modules.WithModuleControllers([typeof(CustomerModule).Assembly]);
    /// </code>
    /// </example>
    public static ModuleBuilderContext WithModuleControllers(
        this ModuleBuilderContext context,
        IEnumerable<Assembly> assemblies,
        Action<IMvcBuilder> optionsAction = null)
    {
        var registry = GetRegistry(context.Services);
        var builder = context.Services.AddControllers()
            .ConfigureApplicationPartManager(manager =>
            {
                // only add the controllers from enabled modules
                foreach (var module in registry.Modules.Where(module => module.Enabled))
                {
                    Log.Logger.Information("[{LogKey}] module assemblypart added (module={ModuleName})",
                        ModuleConstants.LogKey,
                        module.Name);

                    // INFO: controllers should be in same assembly (Presentation) where the module definition resides
                    manager.ApplicationParts.Add(new AssemblyPart(module.GetType().Assembly));
                }

                foreach (var assembly in assemblies.SafeNull()) // optionally load in more assemblies as webparts
                {
                    manager.ApplicationParts.Add(new AssemblyPart(assembly));
                }
            });

        optionsAction?.Invoke(builder);

        return context;
    }

    /// <summary>
    ///     Configures the application part manager to use a custom feature provider for module controllers.
    /// </summary>
    /// <param name="context">The <see cref="ModuleBuilderContext" /> which provides the services and configuration.</param>
    /// <param name="optionsAction">An optional action to configure the MVC builder.</param>
    /// <returns>The updated <see cref="ModuleBuilderContext" />.</returns>
    /// <example>
    /// <code>
    /// modules.WithModuleFeatureProvider();
    /// </code>
    /// </example>
    public static ModuleBuilderContext WithModuleFeatureProvider(
        this ModuleBuilderContext context,
        Action<IMvcBuilder> optionsAction = null)
    {
        var builder = context.Services.AddControllers()
            .ConfigureApplicationPartManager(manager =>
            {
                // only add the controllers from enabled modules
                using var scope = context.Services.BuildServiceProvider().CreateScope();
                manager.FeatureProviders.Remove(manager.FeatureProviders.OfType<ControllerFeatureProvider>()
                    .FirstOrDefault());
                manager.FeatureProviders.Add(
                    new ModuleControllerFeatureProvider(scope.ServiceProvider.GetServices<IModuleContextAccessor>()));
            });

        optionsAction?.Invoke(builder);

        return context;
    }

    private static ModuleRegistry GetRegistry(IServiceCollection services)
    {
        return services
            .Where(descriptor => descriptor.ServiceType == typeof(ModuleRegistry))
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<ModuleRegistry>()
            .LastOrDefault() ??
            throw new InvalidOperationException(
                "No host-scoped module registry was found. Create a ModuleBuilderContext before configuring module controllers.");
    }

}
