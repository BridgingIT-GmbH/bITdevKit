// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Diagnostics;
using System.Reflection;
using AspNetCore.Builder;
using AspNetCore.Hosting;
using BridgingIT.DevKit.Common;
using Configuration;
using Extensions;
using FluentValidation;
using Hosting;
using Serilog;

/// <summary>
///     Provides discovery, registration, activation, and configuration helpers for application modules.
/// </summary>
/// <example>
/// <code>
/// services.AddModules(configuration, modules =&gt; modules.WithModule&lt;CoreModule&gt;());
/// </code>
/// </example>
public static class ModuleExtensions
{
    /// <summary>
    ///     Gets the name of the assembly that contains the module extensions.
    /// </summary>
    /// <example>
    /// <code>
    /// Console.WriteLine(ModuleExtensions.ServiceName);
    /// </code>
    /// </example>
    public static string ServiceName { get; } = Assembly.GetExecutingAssembly().GetName().Name;

    /// <summary>
    ///     Discovers modules in the specified assemblies and registers them for one service collection.
    /// </summary>
    /// <remarks>
    ///     If <paramref name="assemblies" /> is empty, this overload scans the assemblies that are loaded when the
    ///     method runs. Prefer the callback overload with <c>WithModule</c> for explicit registration.
    /// </remarks>
    /// <param name="services">The service collection that owns the module registry.</param>
    /// <param name="configuration">The configuration supplied to each discovered module.</param>
    /// <param name="environment">The web hosting environment supplied to each discovered module.</param>
    /// <param name="assemblies">The assemblies to scan, or an empty array to scan the currently loaded assemblies.</param>
    /// <returns>The module builder context for the service collection.</returns>
    /// <example>
    /// <code>
    /// services.AddModules(configuration, environment, typeof(ModuleMarker).Assembly);
    /// </code>
    /// </example>
    public static ModuleBuilderContext AddModules(
        this IServiceCollection services,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null,
        params Assembly[] assemblies)
    {
        var context = CreateContext(services, configuration, environment);
        var discoveryAssemblies = assemblies is { Length: > 0 }
            ? assemblies
            : AppDomain.CurrentDomain.GetAssemblies();

        return DiscoverModules(context, discoveryAssemblies);
    }

    /// <summary>
    ///     Registers explicitly selected modules for one service collection.
    /// </summary>
    /// <param name="services">The service collection that owns the module registry.</param>
    /// <param name="configuration">The configuration supplied to module registration.</param>
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
    ///     Registers explicitly selected modules for one service collection and supplies the web hosting environment.
    /// </summary>
    /// <param name="services">The service collection that owns the module registry.</param>
    /// <param name="configuration">The configuration supplied to module registration.</param>
    /// <param name="environment">The web hosting environment supplied to module registration.</param>
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
        var context = CreateContext(services, configuration, environment);
        optionsAction?.Invoke(context);

        return context;
    }

    /// <summary>
    ///     Registers a module type explicitly. The method creates the module when the host has no instance of that type.
    /// </summary>
    /// <typeparam name="T">The concrete module type to register.</typeparam>
    /// <param name="context">The module builder context for the current host.</param>
    /// <returns>The same builder context.</returns>
    /// <example>
    /// <code>
    /// modules.AddModule&lt;CoreModule&gt;();
    /// </code>
    /// </example>
    public static ModuleBuilderContext AddModule<T>(this ModuleBuilderContext context)
        where T : class, IModule
    {
        return AddModule(context, typeof(T));
    }

    /// <summary>
    ///     Registers the specified module instance for the current host.
    /// </summary>
    /// <param name="context">The module builder context for the current host.</param>
    /// <param name="module">The module instance to register.</param>
    /// <returns>The same builder context.</returns>
    /// <example>
    /// <code>
    /// modules.AddModule(new CoreModule());
    /// </code>
    /// </example>
    public static ModuleBuilderContext AddModule(this ModuleBuilderContext context, IModule module)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(module);

        return RegisterModule(context, module);
    }

    /// <summary>
    ///     Registers a concrete module type explicitly. The method creates the module when the host has no instance of
    ///     the exact type.
    /// </summary>
    /// <param name="context">The module builder context for the current host.</param>
    /// <param name="type">The concrete module type to register.</param>
    /// <returns>The same builder context.</returns>
    /// <exception cref="ArgumentException">
    ///     The type does not implement <see cref="IModule" />, or it is an interface, an abstract type, or an open
    ///     generic type.
    /// </exception>
    /// <exception cref="InvalidOperationException">The module cannot be constructed.</exception>
    /// <example>
    /// <code>
    /// modules.AddModule(typeof(CoreModule));
    /// </code>
    /// </example>
    public static ModuleBuilderContext AddModule(this ModuleBuilderContext context, Type type)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(type);
        ValidateModuleType(type);

        var module = GetRegistry(context.Services).Find(type) ?? CreateModule(type);
        return RegisterModule(context, module);
    }

    /// <summary>
    ///     Selects, creates when necessary, and registers a module type for the current host.
    /// </summary>
    /// <typeparam name="T">The concrete module type to register.</typeparam>
    /// <param name="context">The module builder context for the current host.</param>
    /// <returns>The same builder context.</returns>
    /// <example>
    /// <code>
    /// modules.WithModule&lt;CoreModule&gt;();
    /// </code>
    /// </example>
    public static ModuleBuilderContext WithModule<T>(this ModuleBuilderContext context)
        where T : class, IModule
    {
        return AddModule<T>(context);
    }

    /// <summary>
    ///     Selects and registers the specified module instance for the current host.
    /// </summary>
    /// <param name="context">The module builder context for the current host.</param>
    /// <param name="module">The module instance to register.</param>
    /// <returns>The same builder context.</returns>
    /// <example>
    /// <code>
    /// modules.WithModule(new CoreModule());
    /// </code>
    /// </example>
    public static ModuleBuilderContext WithModule(this ModuleBuilderContext context, IModule module)
    {
        return AddModule(context, module);
    }

    /// <summary>
    ///     Selects, creates when necessary, and registers a concrete module type for the current host.
    /// </summary>
    /// <param name="context">The module builder context for the current host.</param>
    /// <param name="type">The concrete module type to register.</param>
    /// <returns>The same builder context.</returns>
    /// <example>
    /// <code>
    /// modules.WithModule(typeof(CoreModule));
    /// </code>
    /// </example>
    public static ModuleBuilderContext WithModule(this ModuleBuilderContext context, Type type)
    {
        return AddModule(context, type);
    }

    /// <summary>
    ///     Discovers and registers modules from the assembly that contains <typeparamref name="TMarker" />.
    /// </summary>
    /// <typeparam name="TMarker">A type in the assembly to scan.</typeparam>
    /// <param name="context">The module builder context for the current host.</param>
    /// <returns>The same builder context.</returns>
    /// <example>
    /// <code>
    /// modules.DiscoverModulesFrom&lt;ModuleMarker&gt;();
    /// </code>
    /// </example>
    public static ModuleBuilderContext DiscoverModulesFrom<TMarker>(this ModuleBuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return DiscoverModules(context, [typeof(TMarker).Assembly]);
    }

    /// <summary>
    ///     Discovers and registers modules from the specified assemblies.
    /// </summary>
    /// <param name="context">The module builder context for the current host.</param>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>The same builder context.</returns>
    /// <exception cref="ArgumentException">No assemblies were specified.</exception>
    /// <example>
    /// <code>
    /// modules.DiscoverModulesFrom(typeof(ModuleMarker).Assembly);
    /// </code>
    /// </example>
    public static ModuleBuilderContext DiscoverModulesFrom(
        this ModuleBuilderContext context,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(assemblies);

        if (assemblies.Length == 0)
        {
            throw new ArgumentException("Specify at least one assembly to discover modules from.", nameof(assemblies));
        }

        return DiscoverModules(context, assemblies);
    }

    /// <summary>
    ///     Applies every selected module to the application pipeline in deterministic module order.
    /// </summary>
    /// <param name="app">The application builder to configure.</param>
    /// <param name="configuration">The configuration supplied to each module.</param>
    /// <param name="environment">The hosting environment supplied to each module.</param>
    /// <returns>The same application builder.</returns>
    /// <exception cref="InvalidOperationException">The application's services do not contain a module registry.</exception>
    /// <example>
    /// <code>
    /// app.UseModules();
    /// </code>
    /// </example>
    public static IApplicationBuilder UseModules(
        this IApplicationBuilder app,
        IConfiguration configuration = null,
        IWebHostEnvironment environment = null)
    {
        ArgumentNullException.ThrowIfNull(app);

        var registry = GetRegistry(app.ApplicationServices);
        foreach (var module in registry.Modules)
        {
            Log.Logger.Information(
                "[{LogKey}] use (module={ModuleName}, enabled={ModuleEnabled}, priority={ModulePriority}) ",
                ModuleConstants.LogKey,
                module.Name,
                module.Enabled,
                module.Priority);
            module.Use(app, configuration, environment);
        }

        return app;
    }

    /// <summary>
    ///     Applies selected modules to a web application using the application's configuration and environment.
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
        ArgumentNullException.ThrowIfNull(app);

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
    /// <returns>The bound options, or <see langword="null" /> when services or configuration are unavailable.</returns>
    /// <example>
    /// <code>
    /// var options = module.Configure&lt;ModuleOptions&gt;(services, configuration);
    /// </code>
    /// </example>
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
    /// <returns>The bound options, or <see langword="null" /> when services or configuration are unavailable.</returns>
    /// <example>
    /// <code>
    /// var options = module.Configure&lt;ModuleOptions&gt;(services, configuration, value =&gt; value.Enabled);
    /// </code>
    /// </example>
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
    /// <returns>The bound options, or <see langword="null" /> when services or configuration are unavailable.</returns>
    /// <example>
    /// <code>
    /// var options = module.Configure&lt;ModuleOptions, ModuleOptionsValidator&gt;(services, configuration);
    /// </code>
    /// </example>
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

        if (EnvironmentExtensions.IsBuildTimeOpenApiGeneration())
        {
            return services.Configure<TOptions>(configuration, module, validateOnStart);
        }

        return services.Configure<TOptions, TValidator>(configuration, module, validateOnStart);
    }

    private static ModuleBuilderContext CreateContext(
        IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);

        var context = new ModuleBuilderContext(services, configuration, environment);
        if (!GetRegistry(services).TryRegisterInfrastructure())
        {
            return context;
        }

        AddActivitySource(services, "default");
        AddActivitySource(services, ServiceName);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ModuleActivityListenerService>());

        context.WithModuleContextAccessors()
            .WithRequestModuleContextAccessors();

        return context;
    }

    private static ModuleBuilderContext DiscoverModules(
        ModuleBuilderContext context,
        IEnumerable<Assembly> assemblies)
    {
        var distinctAssemblies = assemblies.SafeNull().Where(assembly => assembly is not null).Distinct().ToArray();
        var registry = GetRegistry(context.Services);
        Log.Logger.Information(
            "[{LogKey}] module discovery (type={ModuleType}) ",
            ModuleConstants.LogKey,
            typeof(IModule).Name);

        var modules = ReflectionHelper
            .FindTypes(
                type => typeof(IModule).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract,
                distinctAssemblies)
            .SafeNull()
            .Distinct()
            .Select(moduleType => registry.Find(moduleType) ?? CreateModule(moduleType))
            .OrderBy(module => module.Priority)
            .ThenBy(module => module.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var module in modules)
        {
            Log.Logger.Debug("[{LogKey}] module discovered (name={ModuleName}) ", ModuleConstants.LogKey, module.Name);
            RegisterModule(context, module);
        }

        return context;
    }

    private static ModuleBuilderContext RegisterModule(ModuleBuilderContext context, IModule module)
    {
        ValidateModuleType(module.GetType());

        var registry = GetRegistry(context.Services);
        var result = registry.Add(module);
        if (!result.Added || registry.IsRegistered(result.Module.GetType()))
        {
            return context;
        }

        ApplyEnablement(result.Module, context.Configuration);
        registry.SynchronizeModuleServices(context.Services);
        var activitySourceDescriptor = AddActivitySource(context.Services, result.Module.Name);

        try
        {
            Log.Logger.Information(
                "[{LogKey}] register (module={ModuleName}, enabled={ModuleEnabled}, priority={ModulePriority}) ",
                ModuleConstants.LogKey,
                result.Module.Name,
                result.Module.Enabled,
                result.Module.Priority);

            result.Module.Register(context.Services, context.Configuration, context.Environment);
            registry.MarkRegistered(result.Module.GetType());
            result.Module.IsRegistered = true;
        }
        catch
        {
            context.Services.Remove(activitySourceDescriptor);
            registry.Remove(result.Module);
            registry.SynchronizeModuleServices(context.Services);
            throw;
        }

        return context;
    }

    private static void ApplyEnablement(IModule module, IConfiguration configuration)
    {
        if (configuration is null)
        {
            return;
        }

        var disabled = configuration[$"Modules:{module.Name}:Enabled"].SafeEquals("False");
        module.Enabled = !disabled;
    }

    private static IModule CreateModule(Type moduleType)
    {
        try
        {
            if (Factory.Create(moduleType) is IModule module)
            {
                return module;
            }
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Module type '{moduleType.FullName}' could not be constructed. Ensure it has a public parameterless " +
                "constructor supported by BridgingIT.DevKit.Common.Factory.Create.",
                exception);
        }

        throw new InvalidOperationException(
            $"Module type '{moduleType.FullName}' could not be constructed. Ensure it has a public parameterless " +
            "constructor supported by BridgingIT.DevKit.Common.Factory.Create.");
    }

    private static void ValidateModuleType(Type moduleType)
    {
        ArgumentNullException.ThrowIfNull(moduleType);

        if (!typeof(IModule).IsAssignableFrom(moduleType))
        {
            throw new ArgumentException(
                $"Module type '{moduleType.FullName}' must implement '{typeof(IModule).FullName}'.",
                nameof(moduleType));
        }

        if (moduleType.IsInterface)
        {
            throw new ArgumentException($"Module type '{moduleType.FullName}' cannot be an interface.", nameof(moduleType));
        }

        if (moduleType.IsAbstract)
        {
            throw new ArgumentException($"Module type '{moduleType.FullName}' cannot be abstract.", nameof(moduleType));
        }

        if (moduleType.ContainsGenericParameters)
        {
            throw new ArgumentException(
                $"Module type '{moduleType.FullName}' cannot contain open generic parameters.",
                nameof(moduleType));
        }
    }

    private static ServiceDescriptor AddActivitySource(IServiceCollection services, string name)
    {
        var descriptor = ServiceDescriptor.Singleton<ActivitySource>(_ => new ActivitySource(name));
        services.Add(descriptor);
        return descriptor;
    }

    private static ModuleRegistry GetRegistry(IServiceProvider services)
    {
        return services.GetService<ModuleRegistry>() ??
            throw new InvalidOperationException(
                "No host-scoped module registry was found. Register modules with services.AddModules() before building the application.");
    }

    private static ModuleRegistry GetRegistry(IServiceCollection services)
    {
        return services
            .Where(descriptor => descriptor.ServiceType == typeof(ModuleRegistry))
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<ModuleRegistry>()
            .LastOrDefault() ??
            throw new InvalidOperationException(
                "No host-scoped module registry was found. Create a ModuleBuilderContext before registering modules.");
    }
}
