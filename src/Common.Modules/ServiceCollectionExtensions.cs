// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using Configuration;
using FluentValidation;
using Serilog;

/// <summary>
///     Provides extension methods for the <see cref="IServiceCollection" /> interface to add and configure module context
///     accessors.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers all classes within the application dependencies that are
    ///     assignable to the IModuleContextAccessor interface as singleton services
    ///     in the dependency injection container.
    /// </summary>
    /// <param name="context">The ModuleBuilderContext containing the services collection.</param>
    /// <returns>The updated ModuleBuilderContext with the registered services.</returns>
    public static ModuleBuilderContext WithModuleContextAccessors(this ModuleBuilderContext context)
    {
        context.Services.Scan(scan =>
            scan // https://andrewlock.net/using-scrutor-to-automatically-register-your-services-with-the-asp-net-core-di-container/
                .FromApplicationDependencies(a =>
                    !a.FullName.EqualsPatternAny(["Microsoft*", "System*", "Scrutor*", "HealthChecks*"]))
                .AddClasses(classes => classes.AssignableTo(typeof(IModuleContextAccessor)), true)
                .AsImplementedInterfaces()
                .WithSingletonLifetime());

        return context;
    }

    /// <summary>
    ///     Adds a singleton service of the type specified in <typeparamref name="TTypeModuleContextAccessor" /> to the
    ///     IServiceCollection with an implementation type of IModuleContextAccessor.
    /// </summary>
    /// <typeparam name="TTypeModuleContextAccessor">
    ///     The type of the service to add. This type must implement
    ///     IModuleContextAccessor.
    /// </typeparam>
    /// <param name="context">The ModuleBuilderContext to add the service to.</param>
    /// <returns>The original ModuleBuilderContext instance, now with the specified service added.</returns>
    public static ModuleBuilderContext WithModuleContextAccessor<TTypeModuleContextAccessor>(
        this ModuleBuilderContext context)
        where TTypeModuleContextAccessor : class, IModuleContextAccessor
    {
        context.Services.AddSingleton<IModuleContextAccessor, TTypeModuleContextAccessor>();

        return context;
    }

    /// <summary>
    ///     Adds an instance of <see cref="IModuleContextAccessor" /> to the service collection within the provided module
    ///     builder context.
    /// </summary>
    /// <param name="context">The current <see cref="ModuleBuilderContext" />.</param>
    /// <param name="accessor">The instance of <see cref="IModuleContextAccessor" /> to add to the service collection.</param>
    /// <returns>The updated <see cref="ModuleBuilderContext" />.</returns>
    public static ModuleBuilderContext WithModuleContextAccessor(
        this ModuleBuilderContext context,
        IModuleContextAccessor accessor)
    {
        context.Services.AddSingleton(accessor);

        return context;
    }

    /// <summary>
    ///     Registers a module context accessor of the specified type within the provided module builder context.
    /// </summary>
    /// <param name="context">The module builder context to which the module context accessor will be registered.</param>
    /// <param name="type">The type of module context accessor to be registered.</param>
    /// <returns>The modified module builder context with the registered module context accessor.</returns>
    public static ModuleBuilderContext WithModuleContextAccessor(this ModuleBuilderContext context, Type type)
    {
        if (Factory.Create(type, context.Services.BuildServiceProvider()) is IModuleContextAccessor accessor)
        {
            context.Services.AddSingleton(accessor);
        }

        return context;
    }

    /// <summary>
    ///     Configures options of type TOptions using the provided configuration and module,
    ///     and registers the options with dependency injection.
    /// </summary>
    /// <typeparam name="TOptions">The type of options to configure.</typeparam>
    /// <param name="services">The service collection to which the options are added.</param>
    /// <param name="configuration">The configuration interface that provides access to the configuration values.</param>
    /// <param name="module">The module that dictates the configuration section.</param>
    /// <param name="validateOnStart">Indicates whether to validate options on application start.</param>
    /// <returns>The configured options of type TOptions.</returns>
    public static TOptions Configure<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        IModule module,
        bool validateOnStart = true)
        where TOptions : class
    {
        // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-8.0#use-di-services-to-configure-options
        if (configuration is not null)
        {
            Log.Logger.Information(
                "{LogKey} validate (module={ModuleName}, type={ConfigurationType}, method=Annotations) ",
                ModuleConstants.LogKey,
                module.Name,
                typeof(TOptions).Name);

            var builder = services.AddOptions<TOptions>()
                .Bind(configuration.GetModuleSection(module))
                .ValidateDataAnnotations(); // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-6.0#options-validation
            // WARN: ValidateDataAnnotations() does not validate nested properties > solution: https://stackoverflow.com/a/63877981
            if (validateOnStart)
            {
                builder.ValidateOnStart();
            }
        }

        return configuration.Get<TOptions>(module);
    }

    /// <summary>
    ///     Configures the specified options instance using the specified configuration and validation logic.
    /// </summary>
    /// <typeparam name="TOptions">The type of the options instance to configure.</typeparam>
    /// <param name="services">The collection of service descriptors.</param>
    /// <param name="configuration">The configuration instance containing the options values.</param>
    /// <param name="module">The module which contains additional configuration information.</param>
    /// <param name="validationOptions">A function to validate the options instance.</param>
    /// <param name="validateOnStart">
    ///     Indicates whether the options should be validated during application startup. The default
    ///     is true.
    /// </param>
    /// <returns>The configured options instance.</returns>
    public static TOptions Configure<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        IModule module,
        Func<TOptions, bool> validationOptions,
        bool validateOnStart = true)
        where TOptions : class
    {
        // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-8.0#use-di-services-to-configure-options
        if (configuration is not null)
        {
            Log.Logger.Information("{LogKey} validate (module={ModuleName}, type={ConfigurationType}, method=Inline) ",
                ModuleConstants.LogKey,
                module.Name,
                typeof(TOptions).Name);

            var builder = services.AddOptions<TOptions>()
                .Bind(configuration.GetModuleSection(module))
                .Validate(validationOptions); // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-6.0#options-validation

            if (validateOnStart)
            {
                builder.ValidateOnStart();
            }
        }

        return configuration.Get<TOptions>(module);
    }

    /// <summary>
    ///     Configures options for a specified module with validation.
    /// </summary>
    /// <typeparam name="TOptions">The type of options to configure.</typeparam>
    /// <typeparam name="TValidator">The type of validator used for validating the options.</typeparam>
    /// <param name="services">The service collection to add the options to.</param>
    /// <param name="configuration">The configuration that provides the values for the options.</param>
    /// <param name="module">The module that provides context for configuration.</param>
    /// <param name="validateOnStart">Specifies whether to validate the options on service startup.</param>
    /// <returns>The configured options of type <typeparamref name="TOptions" />.</returns>
    public static TOptions Configure<TOptions, TValidator>(
        this IServiceCollection services,
        IConfiguration configuration,
        IModule module,
        bool validateOnStart = true)
        where TOptions : class
        where TValidator : class, IValidator<TOptions>
    {
        // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-8.0#use-di-services-to-configure-options
        if (configuration is not null)
        {
            var section = configuration.GetModuleSection(module);
            var builder = services.AddOptions<TOptions>()
                .Bind(section)
                .Validate(
                    validationOptions => // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-6.0#options-validation
                    {
                        Log.Logger.Information(
                            "{LogKey} validate (module={ModuleName}, type={ConfigurationType}, method=Validator) ",
                            ModuleConstants.LogKey,
                            module.Name,
                            typeof(TOptions).Name);

                        return Factory<TValidator>.Create()
                            .Validate(validationOptions, strategy => strategy.ThrowOnFailures())
                            .IsValid;
                    });

            if (validateOnStart)
            {
                builder.ValidateOnStart();
            }

            return section.Get<TOptions>();
        }

        return default; //configuration.Get<TOptions>(module);
    }

    /// <summary>
    ///     Registers all classes in the application dependencies that implement the
    ///     <see cref="IRequestModuleContextAccessor" /> interface as singletons in the DI container.
    /// </summary>
    /// <param name="context">The <see cref="ModuleBuilderContext" /> to configure the services for.</param>
    /// <returns>The modified <see cref="ModuleBuilderContext" /> with the registered services.</returns>
    public static ModuleBuilderContext WithRequestModuleContextAccessors(this ModuleBuilderContext context)
    {
        context.Services.Scan(scan =>
            scan // https://andrewlock.net/using-scrutor-to-automatically-register-your-services-with-the-asp-net-core-di-container/
                .FromApplicationDependencies(a =>
                    !a.FullName.EqualsPatternAny(["Microsoft*", "System*", "Scrutor*", "HealthChecks*"]))
                .AddClasses(classes => classes.AssignableTo(typeof(IRequestModuleContextAccessor)), true)
                .AsImplementedInterfaces()
                .WithSingletonLifetime());

        return context;
    }

    /// <summary>
    ///     Adds a singleton instance of the specified request module context accessor type to the dependency injection
    ///     container.
    /// </summary>
    /// <typeparam name="TTypeModuleContextAccessor">The type of the request module context accessor.</typeparam>
    /// <param name="context">The module builder context.</param>
    /// <returns>The updated module builder context.</returns>
    public static ModuleBuilderContext WithRequestModuleContextAccessor<TTypeModuleContextAccessor>(
        this ModuleBuilderContext context)
        where TTypeModuleContextAccessor : class, IRequestModuleContextAccessor
    {
        context.Services.AddSingleton<IRequestModuleContextAccessor, TTypeModuleContextAccessor>();

        return context;
    }

    /// <summary>
    ///     Adds the specified IRequestModuleContextAccessor implementation to the service collection as a singleton.
    /// </summary>
    /// <param name="context">The current module builder context.</param>
    /// <param name="accessor">The IRequestModuleContextAccessor implementation to add to the service collection.</param>
    /// <returns>The updated module builder context.</returns>
    public static ModuleBuilderContext WithRequestModuleContextAccessor(
        this ModuleBuilderContext context,
        IRequestModuleContextAccessor accessor)
    {
        context.Services.AddSingleton(accessor);

        return context;
    }

    /// <summary>
    ///     Adds a request module context accessor of the specified type to the module builder context.
    /// </summary>
    /// <param name="context">The module builder context.</param>
    /// <param name="type">The type of IRequestModuleContextAccessor to add.</param>
    /// <returns>The module builder context with the added request module context accessor.</returns>
    public static ModuleBuilderContext WithRequestModuleContextAccessor(this ModuleBuilderContext context, Type type)
    {
        if (Factory.Create(type, context.Services.BuildServiceProvider()) is IRequestModuleContextAccessor accessor)
        {
            context.Services.AddSingleton(accessor);
        }

        return context;
    }
}