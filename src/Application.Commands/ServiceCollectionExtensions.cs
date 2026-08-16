// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Reflection;
using BridgingIT.DevKit.Application.Commands;
using MediatR.Registration;
using Scrutor;

/// <summary>
/// Represents service collection extensions.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds commands.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="assemblyExcludePatterns">The assembly exclude patterns used by the operation.</param>
    /// <param name="skipHandlerRegistration">The skip handler registration used by the operation.</param>
    /// <param name="lifetime">The lifetime used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static CommandBuilderContext AddCommands(
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
                        c.ImplementsInterface(typeof(ICommandRequestHandler))))
                .UsingRegistrationStrategy(RegistrationStrategy.Skip)
                .AsSelfWithInterfaces()
                .WithLifetime(lifetime));

            services.Scan(scan => scan
                .FromApplicationDependencies(a =>
                    !a.FullName.MatchAny(Blacklists.ApplicationDependencies.Add(assemblyExcludePatterns)))
                .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<>))
                    .Where(c => !c.IsAbstract &&
                        !c.IsGenericTypeDefinition &&
                        c.ImplementsInterface(typeof(ICommandRequestHandler))))
                .UsingRegistrationStrategy(RegistrationStrategy.Skip)
                .AsSelfWithInterfaces()
                .WithLifetime(lifetime));
        }

        return new CommandBuilderContext(services);
    }

    /// <summary>
    /// Adds commands.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="types">The types used by the operation.</param>
    /// <param name="lifetime">The lifetime used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static CommandBuilderContext AddCommands(
        this IServiceCollection services,
        IEnumerable<Type> types,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ServiceRegistrar.AddRequiredServices(services, new MediatRServiceConfiguration());

        services.Scan(scan => scan.FromAssemblies(types.Select(t => t.Assembly).Distinct())
        .AddClasses(classes => classes.AssignableTo(typeof(MediatR.IRequestHandler<,>))
            .Where(c => !c.IsAbstract &&
                !c.IsGenericTypeDefinition &&
                c.ImplementsInterface(typeof(ICommandRequestHandler))))
        .UsingRegistrationStrategy(RegistrationStrategy.Skip)
        .AsSelfWithInterfaces()
        .WithLifetime(lifetime));

        services.Scan(scan => scan.FromAssemblies(types.Select(t => t.Assembly).Distinct())
            .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<>))
                .Where(c => !c.IsAbstract &&
                    !c.IsGenericTypeDefinition &&
                    c.ImplementsInterface(typeof(ICommandRequestHandler))))
            .UsingRegistrationStrategy(RegistrationStrategy.Skip)
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        return new CommandBuilderContext(services);
    }

    /// <summary>
    /// Adds commands.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="assemblies">The assemblies used by the operation.</param>
    /// <param name="lifetime">The lifetime used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static CommandBuilderContext AddCommands(
        this IServiceCollection services,
        IEnumerable<Assembly> assemblies,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ServiceRegistrar.AddRequiredServices(services, new MediatRServiceConfiguration());

        services.Scan(scan => scan.FromAssemblies(assemblies)
            .AddClasses(classes => classes.AssignableTo(typeof(MediatR.IRequestHandler<,>))
                .Where(c => !c.IsAbstract &&
                    !c.IsGenericTypeDefinition &&
                    c.ImplementsInterface(typeof(ICommandRequestHandler))))
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        services.Scan(scan => scan.FromAssemblies(assemblies)
            .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<>))
                .Where(c => !c.IsAbstract &&
                    !c.IsGenericTypeDefinition &&
                    c.ImplementsInterface(typeof(ICommandRequestHandler))))
            .UsingRegistrationStrategy(RegistrationStrategy.Skip)
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        return new CommandBuilderContext(services);
    }

    /// <summary>
    /// Adds commands.
    /// </summary>
    /// <typeparam name="T">The  type.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="lifetime">The lifetime used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static CommandBuilderContext AddCommands<T>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ServiceRegistrar.AddRequiredServices(services, new MediatRServiceConfiguration());

        services.Scan(scan => scan.FromAssemblies(typeof(T).Assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(MediatR.IRequestHandler<,>))
                .Where(c => !c.IsAbstract &&
                    !c.IsGenericTypeDefinition &&
                    c.ImplementsInterface(typeof(ICommandRequestHandler))))
            .UsingRegistrationStrategy(RegistrationStrategy.Skip)
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        services.Scan(scan => scan.FromAssemblies(typeof(T).Assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<>))
                .Where(c => !c.IsAbstract &&
                    !c.IsGenericTypeDefinition &&
                    c.ImplementsInterface(typeof(ICommandRequestHandler))))
            .UsingRegistrationStrategy(RegistrationStrategy.Skip)
            .AsSelfWithInterfaces()
            .WithLifetime(lifetime));

        return new CommandBuilderContext(services);
    }

    /// <summary>
    /// Provides with behavior.
    /// </summary>
    /// <typeparam name="T">The  type.</typeparam>
    /// <param name="context">The context for the operation.</param>
    public static CommandBuilderContext WithBehavior<T>(this CommandBuilderContext context)
        where T : class, ICommandBehavior
    {
        return WithBehavior(context, typeof(T));
    }

    /// <summary>
    /// Executes the with behavior operation.
    /// </summary>
    /// <param name="context">The context for the operation.</param>
    /// <param name="behavior">The behavior used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public static CommandBuilderContext WithBehavior(this CommandBuilderContext context, Type behavior)
    {
        if (behavior is not null)
        {
            if (!behavior.ImplementsInterface(typeof(ICommandBehavior)))
            {
                throw new ArgumentException(
                    $"Command behavior {behavior.Name} does not implement {nameof(ICommandBehavior)}.");
            }

            context.Services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), behavior);
        }

        return context;
    }
}
