// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Reflection;
using BridgingIT.DevKit.Application.Queries;
using MediatR.Registration;
using Scrutor;

/// <summary>
/// Represents service collection extensions.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds queries.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="assemblyExcludePatterns">The assembly exclude patterns used by the operation.</param>
    /// <param name="skipHandlerRegistration">The skip handler registration used by the operation.</param>
    /// <param name="lifetime">The lifetime used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
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
                    !a.FullName.MatchAny(Blacklists.ApplicationDependencies.Add(assemblyExcludePatterns)))
                .AddClasses(classes => classes.AssignableTo(typeof(MediatR.IRequestHandler<,>))
                    .Where(c => !c.IsAbstract &&
                        !c.IsGenericTypeDefinition &&
                        c.ImplementsInterface(typeof(IQueryHandler))))
                .UsingRegistrationStrategy(RegistrationStrategy.Skip)
                .AsSelfWithInterfaces()
                .WithLifetime(lifetime));

            services.Scan(scan => scan
                .FromApplicationDependencies(a =>
                    !a.FullName.MatchAny(Blacklists.ApplicationDependencies.Add(assemblyExcludePatterns)))
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

    /// <summary>
    /// Adds queries.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="types">The types used by the operation.</param>
    /// <param name="skipHandlerRegistration">The skip handler registration used by the operation.</param>
    /// <param name="lifetime">The lifetime used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static QueryBuilderContext AddQueries(
        this IServiceCollection services,
        IEnumerable<Type> types,
        bool skipHandlerRegistration = false,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ServiceRegistrar.AddRequiredServices(services, new MediatRServiceConfiguration());

        services.Scan(scan => scan.FromAssemblies(types.Select(t => t.Assembly).Distinct())
            .AddClasses(classes => classes.AssignableTo(typeof(MediatR.IRequestHandler<,>))
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

    /// <summary>
    /// Adds queries.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="assemblies">The assemblies used by the operation.</param>
    /// <param name="lifetime">The lifetime used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static QueryBuilderContext AddQueries(
        this IServiceCollection services,
        IEnumerable<Assembly> assemblies,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ServiceRegistrar.AddRequiredServices(services, new MediatRServiceConfiguration());

        services.Scan(scan => scan.FromAssemblies(assemblies)
            .AddClasses(classes => classes.AssignableTo(typeof(MediatR.IRequestHandler<,>))
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

    /// <summary>
    /// Adds queries.
    /// </summary>
    /// <typeparam name="T">The  type.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="lifetime">The lifetime used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static QueryBuilderContext AddQueries<T>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ServiceRegistrar.AddRequiredServices(services, new MediatRServiceConfiguration());

        services.Scan(scan => scan.FromAssemblies(typeof(T).Assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(MediatR.IRequestHandler<,>))
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

    /// <summary>
    /// Provides with behavior.
    /// </summary>
    /// <typeparam name="T">The  type.</typeparam>
    /// <param name="context">The context for the operation.</param>
    public static QueryBuilderContext WithBehavior<T>(this QueryBuilderContext context)
        where T : class, IQueryBehavior
    {
        return WithBehavior(context, typeof(T));
    }

    /// <summary>
    /// Executes the with behavior operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <param name="behavior">The behavior used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static QueryBuilderContext WithBehavior(this QueryBuilderContext context, Type behavior)
    {
        if (behavior is not null)
        {
            if (!behavior.ImplementsInterface(typeof(IQueryBehavior)))
            {
                throw new ArgumentException(
                    $"Query behavior {behavior.Name} does not implement {nameof(IQueryBehavior)}.");
            }

            context.Services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), behavior);
        }

        return context;
    }
}
