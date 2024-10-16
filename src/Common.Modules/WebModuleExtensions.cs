// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Reflection;
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
public static class WebModuleExtensions
{
    /// <summary>
    ///     A collection of web modules implemented from the IWebModule interface.
    /// </summary>
    private static List<IWebModule> modules;

    /// <summary>
    ///     Gets a collection of modules implementing the <see cref="IWebModule" /> interface.
    /// </summary>
    /// <returns>An enumerable collection of <see cref="IWebModule" /> instances.</returns>
    public static IEnumerable<IWebModule> Modules => modules;

    /// <summary>
    ///     Maps the modules to the given <see cref="IEndpointRouteBuilder" />.
    /// </summary>
    /// <param name="app">The <see cref="IEndpointRouteBuilder" /> to map the modules to.</param>
    /// <param name="configuration">The configuration settings for the modules.</param>
    /// <param name="environment">The web hosting environment.</param>
    /// <returns>The <see cref="IEndpointRouteBuilder" /> with the modules mapped.</returns>
    /// <exception cref="Exception">Thrown when no modules are found.</exception>
    public static IEndpointRouteBuilder MapModules(
        this IEndpointRouteBuilder app,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        if (ModuleExtensions.Modules is null)
        {
            throw new Exception("No modules found. Add them first with services.AddModules()");
        }

        modules ??= FindModules()?.ToList();

        foreach (var module in modules.SafeNull())
        {
            if (configuration is not null)
            {
                var disabled = configuration[$"Modules:{module.Name}:Enabled"].SafeEquals("False");
                module.Enabled = !disabled;
            }

            Log.Logger.Information(
                "{LogKey} map (module={ModuleName}, enabled={ModuleEnabled}, priority={ModulePriority}) ",
                ModuleConstants.LogKey,
                module.Name,
                module.Enabled,
                module.Priority);
            module.Map(app, configuration, environment);
        }

        return app;
    }

    /// <summary>
    ///     Adds services for module controllers to the <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="context">The <see cref="ModuleBuilderContext" /> for configuring the module.</param>
    /// <param name="optionsAction">An optional <see cref="Action{IMvcBuilder}" /> to configure the MVC services.</param>
    /// <returns>The modified <see cref="ModuleBuilderContext" />.</returns>
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
    public static ModuleBuilderContext WithModuleControllers(
        this ModuleBuilderContext context,
        IEnumerable<Assembly> assemblies,
        Action<IMvcBuilder> optionsAction = null)
    {
        modules ??= FindModules()?.ToList();

        var builder = context.Services.AddControllers()
            .ConfigureApplicationPartManager(manager =>
            {
                // only add the controllers from enabled modules
                foreach (var module in modules.Where(m => m.Enabled))
                {
                    Log.Logger.Information("{LogKey} module assemblypart added (module={ModuleName})",
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

    /// <summary>
    ///     Discovers and returns a list of all available modules that implement the <see cref="IWebModule" /> interface.
    /// </summary>
    /// <returns>
    ///     A list of discovered <see cref="IWebModule" /> instances, ordered by their priority and name.
    /// </returns>
    private static List<IWebModule> FindModules()
    {
        var logResult = false;

        if (modules is null)
        {
            Log.Logger.Information("{LogKey} module discovery (module={ModuleName}) ",
                ModuleConstants.LogKey,
                typeof(IWebModule).Name);
            logResult = true;
        }

        modules ??= ReflectionHelper
            .FindTypes(t => typeof(IWebModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract,
                ModuleExtensions.Modules.Select(m => m.GetType().Assembly).Distinct().ToArray())
            ?.Select(t => Factory.Create(t))
            ?.Cast<IWebModule>()
            ?.OrderBy(m => m.Priority)
            .ThenBy(m => m.Name)
            ?.ToList();

        if (logResult)
        {
            foreach (var module in modules.SafeNull())
            {
                Log.Logger.Debug("{LogKey} module discovered (name={ModuleName}) ",
                    ModuleConstants.LogKey,
                    module.Name);
            }
        }

        return modules;
    }
}