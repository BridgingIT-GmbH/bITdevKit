// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Common;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Serilog;

public static class ServiceCollectionExtensions
{
    public static ModuleBuilderContext WithModuleContextAccessors(this ModuleBuilderContext context)
    {
        context.Services.Scan(scan => scan // https://andrewlock.net/using-scrutor-to-automatically-register-your-services-with-the-asp-net-core-di-container/
            .FromApplicationDependencies(a => !a.FullName.EqualsPatternAny(new[] { "Microsoft*", "System*", "Scrutor*", "HealthChecks*" }))
            .AddClasses(classes => classes.AssignableTo(typeof(IModuleContextAccessor)), true)
            .AsImplementedInterfaces()
            .WithSingletonLifetime());

        return context;
    }

    public static ModuleBuilderContext WithModuleContextAccessor<TTypeModuleContextAccessor>(this ModuleBuilderContext context)
        where TTypeModuleContextAccessor : class, IModuleContextAccessor
    {
        context.Services.AddSingleton<IModuleContextAccessor, TTypeModuleContextAccessor>();

        return context;
    }

    public static ModuleBuilderContext WithModuleContextAccessor(this ModuleBuilderContext context, IModuleContextAccessor accessor)
    {
        context.Services.AddSingleton(accessor);

        return context;
    }

    public static ModuleBuilderContext WithModuleContextAccessor(this ModuleBuilderContext context, Type type)
    {
        if (Factory.Create(type, context.Services.BuildServiceProvider()) is IModuleContextAccessor accessor)
        {
            context.Services.AddSingleton(accessor);
        }

        return context;
    }

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
            Log.Logger.Information("{LogKey} validate (module={ModuleName}, type={ConfigurationType}, method=Annotations) ", ModuleConstants.LogKey, module.Name, typeof(TOptions).Name);

            var builder = services.AddOptions<TOptions>().Bind(configuration.GetSection(module))
                .ValidateDataAnnotations(); // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-6.0#options-validation
                                            // WARN: ValidateDataAnnotations() does not validate nested properties > solution: https://stackoverflow.com/a/63877981
            if (validateOnStart)
            {
                builder.ValidateOnStart();
            }
        }

        return configuration.Get<TOptions>(module);
    }

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
            Log.Logger.Information("{LogKey} validate (module={ModuleName}, type={ConfigurationType}, method=Inline) ", ModuleConstants.LogKey, module.Name, typeof(TOptions).Name);

            var builder = services.AddOptions<TOptions>().Bind(configuration.GetSection(module))
                .Validate(validationOptions); // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-6.0#options-validation

            if (validateOnStart)
            {
                builder.ValidateOnStart();
            }
        }

        return configuration.Get<TOptions>(module);
    }

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
            var section = configuration.GetSection(module);
            var builder = services.AddOptions<TOptions>().Bind(section)
                .Validate(validationOptions => // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-6.0#options-validation
                {
                    Log.Logger.Information("{LogKey} validate (module={ModuleName}, type={ConfigurationType}, method=Validator) ", ModuleConstants.LogKey, module.Name, typeof(TOptions).Name);

                    return Factory<TValidator>.Create()
                        .Validate(validationOptions, strategy => strategy.ThrowOnFailures()).IsValid;
                });

            if (validateOnStart)
            {
                builder.ValidateOnStart();
            }

            return section.Get<TOptions>();
        }

        return default;  //configuration.Get<TOptions>(module);
    }

    public static ModuleBuilderContext WithRequestModuleContextAccessors(this ModuleBuilderContext context)
    {
        context.Services.Scan(scan => scan // https://andrewlock.net/using-scrutor-to-automatically-register-your-services-with-the-asp-net-core-di-container/
            .FromApplicationDependencies(a => !a.FullName.EqualsPatternAny(new[] { "Microsoft*", "System*", "Scrutor*", "HealthChecks*" }))
            .AddClasses(classes => classes.AssignableTo(typeof(IRequestModuleContextAccessor)), true)
            .AsImplementedInterfaces()
            .WithSingletonLifetime());

        return context;
    }

    public static ModuleBuilderContext WithRequestModuleContextAccessor<TTypeModuleContextAccessor>(this ModuleBuilderContext context)
    where TTypeModuleContextAccessor : class, IRequestModuleContextAccessor
    {
        context.Services.AddSingleton<IRequestModuleContextAccessor, TTypeModuleContextAccessor>();

        return context;
    }

    public static ModuleBuilderContext WithRequestModuleContextAccessor(this ModuleBuilderContext context, IRequestModuleContextAccessor accessor)
    {
        context.Services.AddSingleton(accessor);

        return context;
    }

    public static ModuleBuilderContext WithRequestModuleContextAccessor(this ModuleBuilderContext context, Type type)
    {
        if (Factory.Create(type, context.Services.BuildServiceProvider()) is IRequestModuleContextAccessor accessor)
        {
            context.Services.AddSingleton(accessor);
        }

        return context;
    }
}