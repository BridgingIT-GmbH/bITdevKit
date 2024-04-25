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

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDomainEvents(
        this IServiceCollection services,
        IEnumerable<Type> types,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ServiceRegistrar.AddRequiredServices(services, new MediatRServiceConfiguration());

        services.Scan(scan => scan
            .FromAssemblies(types.Select(t => t.Assembly).Distinct())
            .AddClasses(classes =>
                classes.AssignableTo(typeof(INotificationHandler<>))
                    .Where(c => !c.IsAbstract && !c.IsGenericTypeDefinition && c.ImplementsInterface(typeof(IDomainEventHandler))))
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        return services;
    }

    public static IServiceCollection AddDomainEvents(
        this IServiceCollection services,
        IEnumerable<Assembly> assemblies,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ServiceRegistrar.AddRequiredServices(services, new MediatRServiceConfiguration());

        services.Scan(scan => scan
            .FromAssemblies(assemblies)
            .AddClasses(classes =>
                classes.AssignableTo(typeof(INotificationHandler<>))
                    .Where(c => !c.IsAbstract && !c.IsGenericTypeDefinition && c.ImplementsInterface(typeof(IDomainEventHandler))))
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        return services;
    }

    public static IServiceCollection AddDomainEvents<T>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ServiceRegistrar.AddRequiredServices(services, new MediatRServiceConfiguration());

        services.Scan(scan => scan
            .FromAssemblies(typeof(T).Assembly)
            .AddClasses(classes =>
                classes.AssignableTo(typeof(INotificationHandler<>))
                    .Where(c => !c.IsAbstract && !c.IsGenericTypeDefinition && c.ImplementsInterface(typeof(IDomainEventHandler))))
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        return services;
    }

    public static IServiceCollection AddDomainEvents(
        this IServiceCollection services,
        IEnumerable<string> assemblyExcludePatterns = null,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ServiceRegistrar.AddRequiredServices(services, new MediatRServiceConfiguration());

        services.Scan(scan => scan
            .FromApplicationDependencies(a => !a.FullName.EqualsPatternAny(new[] { "Microsoft*", "System*", "Scrutor*", "HealthChecks*" }.Add(assemblyExcludePatterns)))
            .AddClasses(classes =>
                classes.AssignableTo(typeof(INotificationHandler<>))
                    .Where(c => !c.IsAbstract && !c.IsGenericTypeDefinition && c.ImplementsInterface(typeof(IDomainEventHandler))))
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        return services;
    }
}