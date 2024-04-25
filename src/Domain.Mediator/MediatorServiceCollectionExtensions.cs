// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Reflection;
using BridgingIT.DevKit.Common;
using MediatR;
using MediatR.Pipeline;
using MediatR.Registration;

public static class MediatorServiceCollectionExtensions
{
    public static IServiceCollection AddMediatR(
        this IServiceCollection services,
        IEnumerable<Type> types)
    {
        return services.AddMediatR(types.Select(t => t.Assembly));
    }

    public static IServiceCollection AddMediatR(
        this IServiceCollection services,
        IEnumerable<Assembly> assemblies)
    {
        return services.AddMediatR(cfg => cfg
            .RegisterServicesFromAssemblies(assemblies.ToArray()));
    }

    public static IServiceCollection AddMediatR<T>(
        this IServiceCollection services)
    {
        return services.AddMediatR(cfg => cfg
            .RegisterServicesFromAssemblyContaining<T>());
    }

    public static IServiceCollection AddMediatR(
        this IServiceCollection services,
        IEnumerable<string> assemblyExcludePatterns = null,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ServiceRegistrar.AddRequiredServices(services, new MediatRServiceConfiguration());

        services.Scan(scan => scan
            .FromApplicationDependencies(a => !a.FullName.EqualsPatternAny(new[] { "Microsoft*", "System*", "Scrutor*", "HealthChecks*" }.Add(assemblyExcludePatterns)))
            .AddClasses(classes =>
                classes.AssignableTo(typeof(INotificationHandler<>))
                    .Where(c => !c.IsAbstract && !c.IsGenericTypeDefinition))
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        services.Scan(scan => scan
            .FromApplicationDependencies(a => !a.FullName.EqualsPatternAny(new[] { "Microsoft*", "System*", "Scrutor*", "HealthChecks*" }.Add(assemblyExcludePatterns)))
            .AddClasses(classes =>
                classes.AssignableTo(typeof(IRequestHandler<,>))
                    .Where(c => !c.IsAbstract && !c.IsGenericTypeDefinition))
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        services.Scan(scan => scan
            .FromApplicationDependencies(a => !a.FullName.EqualsPatternAny(new[] { "Microsoft*", "System*", "Scrutor*", "HealthChecks*" }.Add(assemblyExcludePatterns)))
            .AddClasses(classes =>
                classes.AssignableTo(typeof(IRequestHandler<>))
                    .Where(c => !c.IsAbstract && !c.IsGenericTypeDefinition))
            .AsSelfWithInterfaces()
        .WithLifetime(lifetime));

        services.Scan(scan => scan
            .FromApplicationDependencies(a => !a.FullName.EqualsPatternAny(new[] { "Microsoft*", "System*", "Scrutor*", "HealthChecks*" }.Add(assemblyExcludePatterns)))
            .AddClasses(classes =>
                classes.AssignableTo(typeof(IStreamRequestHandler<,>))
                    .Where(c => !c.IsAbstract && !c.IsGenericTypeDefinition))
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        services.Scan(scan => scan
            .FromApplicationDependencies(a => !a.FullName.EqualsPatternAny(new[] { "Microsoft*", "System*", "Scrutor*", "HealthChecks*" }.Add(assemblyExcludePatterns)))
            .AddClasses(classes =>
                classes.AssignableTo(typeof(IRequestExceptionHandler<,,>))
                    .Where(c => !c.IsAbstract && !c.IsGenericTypeDefinition))
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        services.Scan(scan => scan
            .FromApplicationDependencies(a => !a.FullName.EqualsPatternAny(new[] { "Microsoft*", "System*", "Scrutor*", "HealthChecks*" }.Add(assemblyExcludePatterns)))
            .AddClasses(classes =>
                classes.AssignableTo(typeof(IRequestExceptionAction<,>))
                    .Where(c => !c.IsAbstract && !c.IsGenericTypeDefinition))
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        return services;
    }
}