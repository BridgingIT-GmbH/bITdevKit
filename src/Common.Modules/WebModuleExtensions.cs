// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Serilog;

public static class WebModuleExtensions
{
    private static List<IWebModule> modules;

    public static IEnumerable<IWebModule> Modules { get => modules; }

    public static IEndpointRouteBuilder MapModules(this IEndpointRouteBuilder app, IConfiguration configuration = null, IWebHostEnvironment environment = null)
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

            Log.Logger.Information("{LogKey} map (module={ModuleName}, enabled={ModuleEnabled}, priority={ModulePriority}) ", ModuleConstants.LogKey, module.Name, module.Enabled, module.Priority);
            module.Map(app, configuration, environment);
        }

        return app;
    }

    /// <summary>
    /// Adds services for module controllers to the <see cref="IServiceCollection"/>.
    /// </summary>
    public static ModuleBuilderContext WithModuleControllers(this ModuleBuilderContext context, Action<IMvcBuilder> optionsAction = null)
    {
        return context.WithModuleControllers([], optionsAction);
    }

    /// <summary>
    /// Adds services for module controllers to the <see cref="IServiceCollection"/>.
    /// </summary>
    public static ModuleBuilderContext WithModuleControllers<T>(this ModuleBuilderContext context, Action<IMvcBuilder> optionsAction = null)
    {
        return context.WithModuleControllers(new[] { typeof(T).Assembly }, optionsAction);
    }

    /// <summary>
    /// Adds services for module controllers to the <see cref="IServiceCollection"/>.
    /// </summary>
    public static ModuleBuilderContext WithModuleControllers(this ModuleBuilderContext context, IEnumerable<Assembly> assemblies, Action<IMvcBuilder> optionsAction = null)
    {
        modules ??= FindModules()?.ToList();

        var builder = context.Services.AddControllers()
            .ConfigureApplicationPartManager(manager =>
            {
                // only add the controllers from enabled modules
                foreach (var module in modules.Where(m => m.Enabled))
                {
                    Log.Logger.Information("{LogKey} module assemblypart added (module={ModuleName})", ModuleConstants.LogKey, module.Name);

                    // INFO: controllers should be in same assembly (Presentation) where the module definition resides
                    manager.ApplicationParts.Add(new AspNetCore.Mvc.ApplicationParts.AssemblyPart(module.GetType().Assembly));
                }

                foreach (var assembly in assemblies.SafeNull()) // optionally load in more assemblies as webparts
                {
                    manager.ApplicationParts.Add(new AspNetCore.Mvc.ApplicationParts.AssemblyPart(assembly));
                }
            });

        optionsAction?.Invoke(builder);

        return context;
    }

    public static ModuleBuilderContext WithModuleFeatureProvider(this ModuleBuilderContext context, Action<IMvcBuilder> optionsAction = null)
    {
        var builder = context.Services.AddControllers()
            .ConfigureApplicationPartManager(manager =>
            {
                // only add the controllers from enabled modules
                using var scope = context.Services.BuildServiceProvider().CreateScope();
                manager.FeatureProviders.Remove(
                    manager.FeatureProviders.OfType<AspNetCore.Mvc.Controllers.ControllerFeatureProvider>().FirstOrDefault());
                manager.FeatureProviders.Add(
                    new ModuleControllerFeatureProvider(scope.ServiceProvider.GetServices<IModuleContextAccessor>()));
            });

        optionsAction?.Invoke(builder);

        return context;
    }

    private static List<IWebModule> FindModules()
    {
        var logResult = false;

        if (modules is null)
        {
            Log.Logger.Information("{LogKey} module discovery (module={ModuleName}) ", ModuleConstants.LogKey, typeof(IWebModule).Name);
            logResult = true;
        }

        modules ??= ReflectionHelper.FindTypes(t =>
                typeof(IWebModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract,
                ModuleExtensions.Modules.Select(m => m.GetType().Assembly).Distinct().ToArray())?
            .Select(t => Factory.Create(t))?.Cast<IWebModule>()?
            .OrderBy(m => m.Priority).ThenBy(m => m.Name)?.ToList();

        if (logResult)
        {
            foreach (var module in modules.SafeNull())
            {
                Log.Logger.Debug("{LogKey} module discovered (name={ModuleName}) ", ModuleConstants.LogKey, module.Name);
            }
        }

        return modules;
    }
}
