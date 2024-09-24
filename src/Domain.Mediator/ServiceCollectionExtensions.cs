// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Reflection;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using MediatR;
using MediatR.Registration;

/// <summary>
///     Provides extension methods for the IServiceCollection to register domain event handlers (Mediator).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers domain event handlers within the specified assemblies or types into the service collection with a given
    ///     service lifetime.
    /// </summary>
    /// <param name="services">The service collection to add the handlers to.</param>
    /// <param name="types">A collection of types whose assemblies will be scanned for domain event handlers.</param>
    /// <param name="lifetime">The lifetime of the registered services. Default is transient.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddDomainEvents(
        this IServiceCollection services,
        IEnumerable<Type> types,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ServiceRegistrar.AddRequiredServices(services, new MediatRServiceConfiguration());

        services.Scan(scan => scan.FromAssemblies(types.Select(t => t.Assembly).Distinct())
            .AddClasses(classes => classes.AssignableTo(typeof(INotificationHandler<>))
                .Where(c => !c.IsAbstract &&
                    !c.IsGenericTypeDefinition &&
                    c.ImplementsInterface(typeof(IDomainEventHandler))))
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        return services;
    }

    /// <summary>
    ///     Registers domain event handlers within the specified assemblies into the service collection with a given service
    ///     lifetime.
    /// </summary>
    /// <param name="services">The service collection to add the handlers to.</param>
    /// <param name="assemblies">A collection of assemblies to scan for domain event handlers.</param>
    /// <param name="lifetime">The lifetime of the registered services. Default is transient.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddDomainEvents(
        this IServiceCollection services,
        IEnumerable<Assembly> assemblies,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ServiceRegistrar.AddRequiredServices(services, new MediatRServiceConfiguration());

        services.Scan(scan => scan.FromAssemblies(assemblies)
            .AddClasses(classes => classes.AssignableTo(typeof(INotificationHandler<>))
                .Where(c => !c.IsAbstract &&
                    !c.IsGenericTypeDefinition &&
                    c.ImplementsInterface(typeof(IDomainEventHandler))))
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        return services;
    }

    /// <summary>
    ///     Registers domain event handlers in the provided service collection.
    /// </summary>
    /// <param name="services">The service collection to add the domain event handlers to.</param>
    /// <param name="lifetime">The lifetime of the registered services. Defaults to transient.</param>
    /// <returns>The service collection with the domain event handlers registered.</returns>
    public static IServiceCollection AddDomainEvents<T>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ServiceRegistrar.AddRequiredServices(services, new MediatRServiceConfiguration());

        services.Scan(scan => scan.FromAssemblies(typeof(T).Assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(INotificationHandler<>))
                .Where(c => !c.IsAbstract &&
                    !c.IsGenericTypeDefinition &&
                    c.ImplementsInterface(typeof(IDomainEventHandler))))
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        return services;
    }

    /// <summary>
    ///     Adds domain event handlers from application dependencies to the service collection.
    /// </summary>
    /// <param name="services">The IServiceCollection to add the services to.</param>
    /// <param name="lifetime">The ServiceLifetime of the domain event handlers. Defaults to Transient.</param>
    /// <param name="assemblyExcludePatterns">Optional patterns for excluding assemblies from the scan.</param>
    /// <returns>The updated IServiceCollection.</returns>
    public static IServiceCollection AddDomainEvents(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Transient,
        IEnumerable<string> assemblyExcludePatterns = null)
    {
        ServiceRegistrar.AddRequiredServices(services, new MediatRServiceConfiguration());

        services.Scan(scan => scan
            .FromApplicationDependencies(a =>
                !a.FullName.EqualsPatternAny(
                    new[] { "Microsoft*", "System*", "Scrutor*", "HealthChecks*" }.Add(assemblyExcludePatterns)))
            .AddClasses(classes => classes.AssignableTo(typeof(INotificationHandler<>))
                .Where(c => !c.IsAbstract &&
                    !c.IsGenericTypeDefinition &&
                    c.ImplementsInterface(typeof(IDomainEventHandler))))
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        return services;
    }
}