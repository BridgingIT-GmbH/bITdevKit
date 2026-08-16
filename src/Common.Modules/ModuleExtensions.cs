// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using AspNetCore.Builder;
using AspNetCore.Hosting;
using BridgingIT.DevKit.Common;
using Configuration;
using FluentValidation;
using Serilog;
using System.Diagnostics;
using System.Reflection;

/// <summary>
///     Provides discovery, registration, activation, and configuration helpers for application modules.
/// </summary>
public static class ModuleExtensions
{
    private static List<IModule> modules;

    /// <summary>
    ///     Gets the modules discovered by the first module-registration call.
    /// </summary>
    public static IEnumerable<IModule> Modules => modules;

    /// <summary>
    ///     Gets the name of the assembly that contains the module extensions.
    /// </summary>
    public static string ServiceName { get; } = Assembly.GetExecutingAssembly().GetName().Name;

    /// <summary>
    /// Registers module services and activity sources with the specified service collection, using optional
    /// configuration and environment settings.
    /// </summary>
    /// <remarks>This method scans the provided assemblies for modules, configures their enablement based on
    /// the configuration, and registers each enabled module with the service collection. Activity sources are
    /// registered for each module and for the service as a whole. This method should be called during application
    /// startup to ensure all modules are properly registered.</remarks>
    /// <param name="services">The service collection to which module services and activity sources will be added. Cannot be null.</param>
    /// <param name="configuration">An optional configuration source used to determine module enablement and provide settings during registration.
    /// If null, all modules are enabled by default.</param>
    /// <param name="environment">An optional web host environment that may be used by modules during registration.</param>
    /// <param name="assemblies">One or more assemblies to scan for modules to register. If no assemblies are provided, no modules will be
    /// discovered.</param>
    /// <returns>A ModuleBuilderContext instance containing the registered services and configuration.</returns>
    public static ModuleBuilderContext AddModules(
        this IServiceCollection services,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null,
        params Assembly[] assemblies)
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
                Log.Logger.Information("[{LogKey}] register (module={ModuleName}, enabled={ModuleEnabled}, priority={ModulePriority}) ", ModuleConstants.LogKey, module.Name, module.Enabled, module.Priority);
                services.AddSingleton(module);
                services.AddSingleton(new ActivitySource(module.Name));

                module.Register(services, configuration, environment);
                module.IsRegistered = true;
            }
        }

        RegisterActivityListener();

        return new ModuleBuilderContext(services, configuration, environment)
            .WithModuleContextAccessors()
            .WithRequestModuleContextAccessors();
    }

    /// <summary>
    /// Registers modules using an explicit builder callback.
    /// </summary>
    /// <param name="services">The service collection to which module services will be added.</param>
    /// <param name="configuration">The application configuration used by module registration.</param>
    /// <param name="optionsAction">The callback that selects and configures modules.</param>
    /// <returns>The module builder context used by the registration callback.</returns>
    /// <example>
    /// <code>
    /// services.AddModules(configuration, modules =&gt; modules.WithModule&lt;CoreModule&gt;());
    /// </code>
    /// </example>
    public static ModuleBuilderContext AddModules(
        this IServiceCollection services,
        IConfiguration configuration = null,
        Action<ModuleBuilderContext> optionsAction = null)
    {
        return AddModules(services, configuration, null, optionsAction);
    }

    /// <summary>
    /// Registers modules using an explicit builder callback and web host environment.
    /// </summary>
    /// <param name="services">The service collection to which module services will be added.</param>
    /// <param name="configuration">The application configuration used by module registration.</param>
    /// <param name="environment">The web host environment passed to registered modules.</param>
    /// <param name="optionsAction">The callback that selects and configures modules.</param>
    /// <returns>The module builder context used by the registration callback.</returns>
    /// <example>
    /// <code>
    /// services.AddModules(configuration, environment, modules =&gt; modules.WithModule&lt;CoreModule&gt;());
    /// </code>
    /// </example>
    public static ModuleBuilderContext AddModules(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        Action<ModuleBuilderContext> optionsAction)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        modules ??= FindModules()?.ToList();

        services.AddSingleton(new ActivitySource("default"));
        services.AddSingleton(new ActivitySource(ServiceName));

        var context = new ModuleBuilderContext(services, configuration, environment)
            .WithModuleContextAccessors()
            .WithRequestModuleContextAccessors();
        optionsAction?.Invoke(context);

        RegisterActivityListener();

        return context;
    }

    /// <summary>
    /// Adds a module of the specified type to the current module builder context.
    /// </summary>
    /// <remarks>This method is a generic convenience overload for adding a module by type. It is equivalent
    /// to calling <c>AddModule(context, typeof(T))</c>.</remarks>
    /// <typeparam name="T">The type of the module to add. Must implement <see cref="IModule"/> and be a reference type.</typeparam>
    /// <param name="context">The module builder context to which the module will be added. Cannot be null.</param>
    /// <returns>A new <see cref="ModuleBuilderContext"/> instance that includes the specified module type.</returns>
    public static ModuleBuilderContext AddModule<T>(this ModuleBuilderContext context)
        where T : class, IModule
    {
        return AddModule(context, typeof(T));
    }

    /// <summary>
    /// Adds a module instance to the current module builder context.
    /// </summary>
    /// <param name="context">The module builder context to which the module will be added. Cannot be null.</param>
    /// <param name="module">The module instance to add. Cannot be null.</param>
    /// <returns>The same <see cref="ModuleBuilderContext"/> instance for fluent chaining.</returns>
    public static ModuleBuilderContext AddModule(this ModuleBuilderContext context, IModule module)
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
            Log.Logger.Information("[{LogKey}] register (module={ModuleName}, enabled={ModuleEnabled}, priority={ModulePriority}) ", ModuleConstants.LogKey, module.Name, module.Enabled, module.Priority);
            context.Services.AddSingleton(module);
            context.Services.AddSingleton(new ActivitySource(module.Name));

            module.Register(context.Services, context.Configuration, context.Environment);
            module.IsRegistered = true;
        }

        return context;
    }

    /// <summary>
    /// Adds a module of the specified type to the current module builder context.
    /// </summary>
    /// <param name="context">The module builder context to which the module will be added. Cannot be null.</param>
    /// <param name="type">The module type to add. Cannot be null.</param>
    /// <returns>The same <see cref="ModuleBuilderContext"/> instance for fluent chaining.</returns>
    public static ModuleBuilderContext AddModule(this ModuleBuilderContext context, Type type)
    {
        EnsureArg.IsNotNull(type, nameof(type));

        var module = modules.SafeNull().FirstOrDefault(m => m.IsOfType(type));
        if (module?.IsRegistered == false)
        {
            Log.Logger.Information("[{LogKey}] register (module={ModuleName}, enabled={ModuleEnabled}, priority={ModulePriority}) ", ModuleConstants.LogKey, module.Name, module.Enabled, module.Priority);
            context.Services.AddSingleton(module);
            context.Services.AddSingleton(new ActivitySource(module.Name));

            module.Register(context.Services, context.Configuration, context.Environment);
            module.IsRegistered = true;
        }

        return context;
    }

    /// <summary>
    ///     Registers a discovered module by its type if it has not already been registered.
    /// </summary>
    /// <typeparam name="T">The module type to register.</typeparam>
    /// <param name="context">The module builder context.</param>
    /// <returns>The same builder context.</returns>
    public static ModuleBuilderContext WithModule<T>(this ModuleBuilderContext context)
        where T : class, IModule
    {
        return AddModule<T>(context);
    }

    /// <summary>
    ///     Registers a specified module instance if it has not already been registered.
    /// </summary>
    /// <param name="context">The module builder context.</param>
    /// <param name="module">The module instance to register.</param>
    /// <returns>The same builder context.</returns>
    public static ModuleBuilderContext WithModule(this ModuleBuilderContext context, IModule module)
    {
        return AddModule(context, module);
    }

    /// <summary>
    ///     Registers a discovered module by runtime type if it has not already been registered.
    /// </summary>
    /// <param name="context">The module builder context.</param>
    /// <param name="type">The module type to register.</param>
    /// <returns>The same builder context.</returns>
    public static ModuleBuilderContext WithModule(this ModuleBuilderContext context, Type type)
    {
        return AddModule(context, type);
    }

    /// <summary>
    ///     Applies every discovered module to the application pipeline in module order.
    /// </summary>
    /// <param name="app">The application builder to configure.</param>
    /// <param name="configuration">Configuration supplied to each module.</param>
    /// <param name="environment">The hosting environment supplied to each module.</param>
    /// <returns>The same application builder.</returns>
    /// <exception cref="Exception">Modules have not first been discovered with <c>AddModules</c>.</exception>
    public static IApplicationBuilder UseModules(
        this IApplicationBuilder app,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        if (modules is null)
        {
            throw new Exception("No modules found. Add them first with services.AddModules()");
        }

        foreach (var module in modules.SafeNull()) // TODO: only load enabled modules
        {
            Log.Logger.Information("[{LogKey}] use (module={ModuleName}, enabled={ModuleEnabled}, priority={ModulePriority}) ", ModuleConstants.LogKey, module.Name, module.Enabled, module.Priority);
            module.Use(app, configuration, environment);
        }

        return app;
    }

    /// <summary>
    /// Applies registered modules to a web application using the application's configuration and environment.
    /// </summary>
    /// <param name="app">The web application to configure.</param>
    /// <returns>The same web application instance.</returns>
    /// <example>
    /// <code>
    /// var app = builder.Build();
    /// app.UseModules();
    /// </code>
    /// </example>
    public static WebApplication UseModules(this WebApplication app)
    {
        EnsureArg.IsNotNull(app, nameof(app));

        UseModules(app, app.Configuration, app.Environment);

        return app;
    }

    /// <summary>
    ///     Binds and registers a module options type from the module's configuration section.
    /// </summary>
    /// <typeparam name="TOptions">The options type to configure.</typeparam>
    /// <param name="module">The module that identifies the configuration section.</param>
    /// <param name="services">The service collection receiving the options registration.</param>
    /// <param name="configuration">The configuration source.</param>
    /// <param name="validateOnStart">Whether registered validation runs during application startup.</param>
    /// <returns>The bound options, or <see langword="null"/> when services or configuration are unavailable.</returns>
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

    /// <summary>
    ///     Binds and registers module options with a predicate used for validation.
    /// </summary>
    /// <typeparam name="TOptions">The options type to configure.</typeparam>
    /// <param name="module">The module that identifies the configuration section.</param>
    /// <param name="services">The service collection receiving the options registration.</param>
    /// <param name="configuration">The configuration source.</param>
    /// <param name="validationOptions">The validation predicate applied to the bound options.</param>
    /// <param name="validateOnStart">Whether registered validation runs during application startup.</param>
    /// <returns>The bound options, or <see langword="null"/> when services or configuration are unavailable.</returns>
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

    /// <summary>
    ///     Binds and registers module options with a FluentValidation validator outside build-time OpenAPI generation.
    /// </summary>
    /// <typeparam name="TOptions">The options type to configure.</typeparam>
    /// <typeparam name="TValidator">The validator type to register.</typeparam>
    /// <param name="module">The module that identifies the configuration section.</param>
    /// <param name="services">The service collection receiving the options registration.</param>
    /// <param name="configuration">The configuration source.</param>
    /// <param name="validateOnStart">Whether registered validation runs during application startup.</param>
    /// <returns>The bound options, or <see langword="null"/> when services or configuration are unavailable.</returns>
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

        if (EnvironmentExtensions.IsBuildTimeOpenApiGeneration()) // dont use validator when generating OpenAPI docs at build time
        {
            return services.Configure<TOptions>(configuration, module, validateOnStart);
        }

        return services.Configure<TOptions, TValidator>(configuration, module, validateOnStart);
    }

    private static IEnumerable<IModule> FindModules(params Assembly[] assemblies)
    {
        var logResult = false;

        if (modules is null)
        {
            Log.Logger.Information("[{LogKey}] module discovery (type={ModuleType}) ", ModuleConstants.LogKey, typeof(IModule).Name);
            logResult = true;
        }

        if (assemblies?.Length == 0)
        {
            assemblies = AppDomain.CurrentDomain.GetAssemblies();
        }

        modules ??= ReflectionHelper
            .FindTypes(t => typeof(IModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract, assemblies?.Distinct()?.ToArray())
            ?.Select(t => Factory.Create(t))
            ?.Cast<IModule>()
            ?.OrderBy(m => m.Priority).ThenBy(m => m.Name)
            ?.ToList();

        if (logResult)
        {
            foreach (var module in modules.SafeNull())
            {
                Log.Logger.Debug("[{LogKey}] module discovered (name={ModuleName}) ", ModuleConstants.LogKey, module.Name);
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
            ActivityStarted = a =>
            {
                if (string.IsNullOrWhiteSpace(a?.DisplayName))
                {
                    return;
                }

                Log.Logger.Verbose("[{LogKey}] started activity: {ActivityOperationName} {ActivityDisplayName} (module={ModuleName}, status={ActivityStatus})", "TRC", a.OperationName, a.DisplayName, a.Source.Name, a.Status);
            },
            ActivityStopped = a =>
            {
                if (string.IsNullOrWhiteSpace(a?.DisplayName))
                {
                    return;
                }

                Log.Logger.Verbose("[{LogKey}] finished activity: {ActivityOperationName} {ActivityDisplayName} (module={ModuleName}, status={ActivityStatus}) -> took {TimeElapsed:0.0000} ms", "TRC", a.OperationName, a.DisplayName, a.Source.Name, a.Status, a.Duration.TotalMilliseconds);
            },
            ShouldListenTo = s => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        });
    }

}
