// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Reflection;
using MediatR.Pipeline;
using MediatR.Registration;

/// <summary>
///     Provides extension methods for adding MediatR services to an <see cref="IServiceCollection" />.
/// </summary>
public static class MediatorServiceCollectionExtensions
{
    public static IServiceCollection AddMediatR(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Transient,
        IEnumerable<string> assemblyExcludePatterns = null)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.FullName.MatchAny(Blacklists.ApplicationDependencies.Add(assemblyExcludePatterns)))
            .SafeGetTypes(
                typeof(INotificationHandler<>), typeof(IRequestHandler<,>), typeof(IRequestHandler<>), typeof(IStreamRequestHandler<,>),
                typeof(IRequestExceptionHandler<,,>), typeof(IRequestExceptionAction<,>))
            .Select(t => t.Assembly)
            .Distinct().ToArray();

        return services.AddMediatR(assemblies, lifetime);
    }

    /// <summary>
    ///     Adds MediatR handlers and services to the <see cref="IServiceCollection" /> from the provided types.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the MediatR services to.</param>
    /// <param name="types">A collection of types containing MediatR handlers.</param>
    /// <returns>The modified <see cref="IServiceCollection" />.</returns>
    public static IServiceCollection AddMediatR(
        this IServiceCollection services,
        IEnumerable<Type> types,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        return services.AddMediatR(types.Select(t => t.Assembly));
    }

    /// <summary>
    ///     Adds MediatR services from the specified assemblies to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <param name="assemblies">The assemblies to scan for MediatR handlers, requests, and other related classes.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddMediatR(
        this IServiceCollection services,
        IEnumerable<Assembly> assemblies,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        return services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(assemblies.ToArray());
            cfg.Lifetime = lifetime;
        });
    }

    /// <summary>
    ///     Adds MediatR services to the specified IServiceCollection.
    /// </summary>
    /// <param name="services">The IServiceCollection to add MediatR services to.</param>
    /// <returns>The original IServiceCollection with MediatR services added.</returns>
    public static IServiceCollection AddMediatR<T>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        return services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<T>();
            cfg.Lifetime = lifetime;
        });
    }

    /// <summary>
    ///     Adds MediatR services to the specified service collection with the specified lifetime and optional assembly
    ///     exclusion patterns.
    /// </summary>
    /// <param name="services">The service collection to add the MediatR services to.</param>
    /// <param name="lifetime">The service lifetime for the MediatR services. Defaults to ServiceLifetime.Transient.</param>
    /// <param name="assemblyExcludePatterns">Optional assembly exclusion patterns to filter assemblies from being scanned.</param>
    /// <returns>The original service collection with MediatR services added.</returns>
    [Obsolete]
    public static IServiceCollection AddMediatRScan(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Transient,
        IEnumerable<string> assemblyExcludePatterns = null)
    {
        ServiceRegistrar.AddRequiredServices(services, new MediatRServiceConfiguration());

        services.Scan(scan => scan
            .FromApplicationDependencies(a =>
                !a.FullName.MatchAny(Blacklists.ApplicationDependencies.Add(assemblyExcludePatterns)))
            .AddClasses(classes => classes.AssignableTo(typeof(INotificationHandler<>))
                .Where(c => !c.IsAbstract && !c.IsGenericTypeDefinition))
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime)); // WARN: causes double registrations due to open-generic handler types (domain event handlers are registered twice and thus handled twice)

        services.Scan(scan => scan
            .FromApplicationDependencies(a =>
                !a.FullName.MatchAny(Blacklists.ApplicationDependencies.Add(assemblyExcludePatterns)))
            .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<,>))
                .Where(c => !c.IsAbstract && !c.IsGenericTypeDefinition))
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        services.Scan(scan => scan
            .FromApplicationDependencies(a =>
                !a.FullName.MatchAny(Blacklists.ApplicationDependencies.Add(assemblyExcludePatterns)))
            .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<>))
                .Where(c => !c.IsAbstract && !c.IsGenericTypeDefinition))
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        services.Scan(scan => scan
            .FromApplicationDependencies(a =>
                !a.FullName.MatchAny(Blacklists.ApplicationDependencies.Add(assemblyExcludePatterns)))
            .AddClasses(classes => classes.AssignableTo(typeof(IStreamRequestHandler<,>))
                .Where(c => !c.IsAbstract && !c.IsGenericTypeDefinition))
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        services.Scan(scan => scan
            .FromApplicationDependencies(a =>
                !a.FullName.MatchAny(Blacklists.ApplicationDependencies.Add(assemblyExcludePatterns)))
            .AddClasses(classes => classes.AssignableTo(typeof(IRequestExceptionHandler<,,>))
                .Where(c => !c.IsAbstract && !c.IsGenericTypeDefinition))
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        services.Scan(scan => scan
            .FromApplicationDependencies(a =>
                !a.FullName.MatchAny(Blacklists.ApplicationDependencies.Add(assemblyExcludePatterns)))
            .AddClasses(classes => classes.AssignableTo(typeof(IRequestExceptionAction<,>))
                .Where(c => !c.IsAbstract && !c.IsGenericTypeDefinition))
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        return services;
    }
}