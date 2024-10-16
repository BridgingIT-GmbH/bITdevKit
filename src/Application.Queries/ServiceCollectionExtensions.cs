// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Reflection;
using BridgingIT.DevKit.Application.Queries;
using MediatR.Registration;
using Scrutor;

public static class ServiceCollectionExtensions
{
    private static readonly string[] sourceArray = ["Microsoft*", "System*", "Scrutor*", "HealthChecks*"];

    public static QueryBuilderContext AddQueries(
        this IServiceCollection services,
        IEnumerable<string> assemblyExcludePatterns = null,
        bool skipHandlerRegistration = false,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        if (!skipHandlerRegistration)
        {
            ServiceRegistrar.AddRequiredServices(services, new MediatRServiceConfiguration());

            services.Scan(scan => scan
                .FromApplicationDependencies(a =>
                    !a.FullName.EqualsPatternAny(sourceArray.Add(assemblyExcludePatterns)))
                .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<,>))
                    .Where(c => !c.IsAbstract &&
                        !c.IsGenericTypeDefinition &&
                        c.ImplementsInterface(typeof(IQueryHandler))))
                .UsingRegistrationStrategy(RegistrationStrategy.Skip)
                .AsSelfWithInterfaces()
                .WithLifetime(lifetime));

            services.Scan(scan => scan
                .FromApplicationDependencies(a =>
                    !a.FullName.EqualsPatternAny(sourceArray.Add(assemblyExcludePatterns)))
                .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<>))
                    .Where(c => !c.IsAbstract &&
                        !c.IsGenericTypeDefinition &&
                        c.ImplementsInterface(typeof(IQueryHandler))))
                .UsingRegistrationStrategy(RegistrationStrategy.Skip)
                .AsSelfWithInterfaces()
                .WithLifetime(lifetime));
        }

        return new QueryBuilderContext(services);
    }

    public static QueryBuilderContext AddQueries(
        this IServiceCollection services,
        IEnumerable<Type> types,
        bool skipHandlerRegistration = false,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ServiceRegistrar.AddRequiredServices(services, new MediatRServiceConfiguration());

        services.Scan(scan => scan.FromAssemblies(types.Select(t => t.Assembly).Distinct())
            .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<,>))
                .Where(c => !c.IsAbstract &&
                    !c.IsGenericTypeDefinition &&
                    c.ImplementsInterface(typeof(IQueryHandler))))
            .UsingRegistrationStrategy(RegistrationStrategy.Skip)
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        services.Scan(scan => scan.FromAssemblies(types.Select(t => t.Assembly).Distinct())
            .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<>))
                .Where(c => !c.IsAbstract &&
                    !c.IsGenericTypeDefinition &&
                    c.ImplementsInterface(typeof(IQueryHandler))))
            .UsingRegistrationStrategy(RegistrationStrategy.Skip)
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        return new QueryBuilderContext(services);
    }

    public static QueryBuilderContext AddQueries(
        this IServiceCollection services,
        IEnumerable<Assembly> assemblies,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ServiceRegistrar.AddRequiredServices(services, new MediatRServiceConfiguration());

        services.Scan(scan => scan.FromAssemblies(assemblies)
            .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<,>))
                .Where(c => !c.IsAbstract &&
                    !c.IsGenericTypeDefinition &&
                    c.ImplementsInterface(typeof(IQueryHandler))))
            .UsingRegistrationStrategy(RegistrationStrategy.Skip)
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        services.Scan(scan => scan.FromAssemblies(assemblies)
            .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<>))
                .Where(c => !c.IsAbstract &&
                    !c.IsGenericTypeDefinition &&
                    c.ImplementsInterface(typeof(IQueryHandler))))
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        return new QueryBuilderContext(services);
    }

    public static QueryBuilderContext AddQueries<T>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ServiceRegistrar.AddRequiredServices(services, new MediatRServiceConfiguration());

        services.Scan(scan => scan.FromAssemblies(typeof(T).Assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<,>))
                .Where(c => !c.IsAbstract &&
                    !c.IsGenericTypeDefinition &&
                    c.ImplementsInterface(typeof(IQueryHandler))))
            .UsingRegistrationStrategy(RegistrationStrategy.Skip)
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        services.Scan(scan => scan.FromAssemblies(typeof(T).Assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<>))
                .Where(c => !c.IsAbstract &&
                    !c.IsGenericTypeDefinition &&
                    c.ImplementsInterface(typeof(IQueryHandler))))
            .UsingRegistrationStrategy(RegistrationStrategy.Skip)
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        return new QueryBuilderContext(services);
    }

    public static QueryBuilderContext WithBehavior<T>(this QueryBuilderContext context)
        where T : class, IQueryBehavior
    {
        return WithBehavior(context, typeof(T));
    }

    public static QueryBuilderContext WithBehavior(this QueryBuilderContext context, Type behavior)
    {
        if (behavior is not null)
        {
            if (!behavior.ImplementsInterface(typeof(IQueryBehavior)))
            {
                throw new ArgumentException(
                    $"Query behavior {behavior.Name} does not implement {nameof(IQueryBehavior)}.");
            }

            context.Services.AddTransient(typeof(IPipelineBehavior<,>), behavior);
        }

        return context;
    }
}