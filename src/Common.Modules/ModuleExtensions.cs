// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using BridgingIT.DevKit.Common;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;

public static class ModuleExtensions
{
    private static List<IModule> modules;

    public static IEnumerable<IModule> Modules { get => modules; }

    public static string ServiceName { get; } = Assembly.GetExecutingAssembly().GetName().Name;

    public static ModuleBuilderContext AddModules(this IServiceCollection services, IConfiguration configuration = null, IWebHostEnvironment environment = null, params Assembly[] assemblies)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        modules ??= FindModules(assemblies)?.ToList();

        services.AddSingleton(new ActivitySource("default"));
        services.AddSingleton(new ActivitySource(ServiceName));

        foreach (var module in modules.SafeNull())
        {
            if (configuration is not null)
            {
                var disabled = configuration[$"Modules:{module.Name}:Enabled"].SafeEquals("False");
                module.Enabled = !disabled;
            }

            if (module?.IsRegistered == false)
            {
                Log.Logger.Information("{LogKey} register (module={ModuleName}, enabled={ModuleEnabled}, priority={ModulePriority}) ", ModuleConstants.LogKey, module.Name, module.Enabled, module.Priority);
                services.AddSingleton(module);
                services.AddSingleton(new ActivitySource(module.Name));

                module.Register(services, configuration, environment);
                module.IsRegistered = true;
            }
        }

        RegisterActivityListener();

        return new ModuleBuilderContext(services, configuration);
    }

    public static ModuleBuilderContext AddModules(
        this IServiceCollection services,
        IConfiguration configuration = null,
        Action<ModuleBuilderContext> optionsAction = null)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        modules ??= FindModules()?.ToList();

        services.AddSingleton(new ActivitySource("default"));
        services.AddSingleton(new ActivitySource(ServiceName));

        var context = new ModuleBuilderContext(services, configuration);
        optionsAction?.Invoke(context);

        RegisterActivityListener();

        return context;
    }

    public static ModuleBuilderContext WithModule<T>(this ModuleBuilderContext context)
        where T : class, IModule
    {
        return WithModule(context, typeof(T));
    }

    public static ModuleBuilderContext WithModule(this ModuleBuilderContext context, IModule module)
    {
        EnsureArg.IsNotNull(module, nameof(module));

        var existingModule = modules.SafeNull().FirstOrDefault(m => m.Name.Equals(module.Name));
        if (existingModule != null)
        {
            modules.Remove(existingModule);
            modules.Add(module);
        }

        if (module?.IsRegistered == false)
        {
            Log.Logger.Information("{LogKey} register (module={ModuleName}, enabled={ModuleEnabled}, priority={ModulePriority}) ", ModuleConstants.LogKey, module.Name, module.Enabled, module.Priority);
            context.Services.AddSingleton(module);
            context.Services.AddSingleton(new ActivitySource(module.Name));

            module.Register(context.Services, context.Configuration);
            module.IsRegistered = true;
        }

        return context;
    }

    public static ModuleBuilderContext WithModule(this ModuleBuilderContext context, Type type)
    {
        EnsureArg.IsNotNull(type, nameof(type));

        var module = modules.SafeNull().FirstOrDefault(m => m.IsOfType(type));
        if (module?.IsRegistered == false)
        {
            Log.Logger.Information("{LogKey} register (module={ModuleName}, enabled={ModuleEnabled}, priority={ModulePriority}) ", ModuleConstants.LogKey, module.Name, module.Enabled, module.Priority);
            context.Services.AddSingleton(module);
            context.Services.AddSingleton(new ActivitySource(module.Name));

            module.Register(context.Services, context.Configuration);
            module.IsRegistered = true;
        }

        return context;
    }

    public static IApplicationBuilder UseModules(this IApplicationBuilder app, IConfiguration configuration = null, IWebHostEnvironment environment = null)
    {
        if (modules is null)
        {
            throw new Exception("No modules found. Add them first with services.AddModules()");
        }

        foreach (var module in modules.SafeNull()) // TODO: only load enabled modules
        {
            Log.Logger.Information("{LogKey} use (module={ModuleName}, enabled={ModuleEnabled}, priority={ModulePriority}) ", ModuleConstants.LogKey, module.Name, module.Enabled, module.Priority);
            module.Use(app, configuration, environment);
        }

        return app;
    }

    public static TOptions Configure<TOptions>(
        this IModule module,
        IServiceCollection services,
        IConfiguration configuration,
        bool validateOnStart = true)
        where TOptions : class
    {
        if (configuration is null || services is null)
        {
            return default;
        }

        return services.Configure<TOptions>(configuration, module, validateOnStart);
    }

    public static TOptions Configure<TOptions>(
        this IModule module,
        IServiceCollection services,
        IConfiguration configuration,
        Func<TOptions, bool> validationOptions,
        bool validateOnStart = true)
        where TOptions : class
    {
        if (configuration is null || services is null)
        {
            return default;
        }

        return services.Configure(configuration, module, validationOptions, validateOnStart);
    }

    public static TOptions Configure<TOptions, TValidator>(
        this IModule module,
        IServiceCollection services,
        IConfiguration configuration,
        bool validateOnStart = true)
        where TOptions : class
        where TValidator : class, IValidator<TOptions>
    {
        if (configuration is null || services is null)
        {
            return default;
        }

        return services.Configure<TOptions, TValidator>(configuration, module, validateOnStart);
    }

    private static IEnumerable<IModule> FindModules(params Assembly[] assemblies)
    {
        var logResult = false;

        if (modules is null)
        {
            Log.Logger.Information("{LogKey} module discovery (type={ModuleType}) ", ModuleConstants.LogKey, typeof(IModule).Name);
            logResult = true;
        }

        if (assemblies?.Length == 0)
        {
            assemblies = AppDomain.CurrentDomain.GetAssemblies();
        }

        modules ??= ReflectionHelper.FindTypes(t => typeof(IModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract, assemblies?.Distinct()?.ToArray())?
            .Select(t => Factory.Create(t))?.Cast<IModule>()?
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

    private static void RegisterActivityListener()
    {
        ActivitySource.AddActivityListener(new ActivityListener
        {
            // ensure that all baggage gets copied as tags so they are visible in the tracing system
            ShouldListenTo = _ => true,
            ActivityStopped = activity =>
            {
                foreach (var (key, value) in activity.Baggage)
                {
                    activity.SetTag(key, value);
                }
            }
        });

        ActivitySource.AddActivityListener(new ActivityListener
        {
            ActivityStarted = (a) =>
            {
                if (string.IsNullOrWhiteSpace(a?.DisplayName))
                {
                    return;
                }

                Log.Logger.Verbose("{LogKey} started activity: {ActivityOperationName} {ActivityDisplayName} (module={ModuleName}, status={ActivityStatus})", "TRC", a.OperationName, a.DisplayName, a.Source.Name, a.Status);
            },
            ActivityStopped = (a) =>
            {
                if (string.IsNullOrWhiteSpace(a?.DisplayName))
                {
                    return;
                }

                Log.Logger.Verbose("{LogKey} finished activity: {ActivityOperationName} {ActivityDisplayName} (module={ModuleName}, status={ActivityStatus}) -> took {TimeElapsed:0.0000} ms", "TRC", a.OperationName, a.DisplayName, a.Source.Name, a.Status, a.Duration.TotalMilliseconds);
            },
            ShouldListenTo = s => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        });
    }
}