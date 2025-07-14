// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;

/// <summary>
/// Configures and builds the Notifier service for dependency injection registration.
/// </summary>
/// <remarks>
/// This class provides a fluent API for registering handlers, behaviors, and providers for the Notifier system.
/// It scans assemblies for handlers and validators, caches handler types, and registers services with the DI container.
/// </remarks>
/// <example>
/// <code>
/// var services = new ServiceCollection();
/// services.AddNotifier()
///     .AddHandlers(new[] { "^System\\..*" })
///     .WithBehavior<ValidationBehavior<,>>();
/// var provider = services.BuildServiceProvider();
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="NotifierBuilder"/> class.
/// </remarks>
/// <param name="services">The service collection for dependency injection registration.</param>
public class NotifierBuilder
{
    private readonly IServiceCollection services;
    private readonly List<Type> pipelineBehaviorTypes = [];
    private readonly List<Type> validatorTypes = [];
    private readonly IHandlerCache handlerCache = new HandlerCache();
    private readonly ConcurrentDictionary<Type, PolicyConfig> policyCache = [];

    public NotifierBuilder(IServiceCollection services)
    {
        this.services = services ?? throw new ArgumentNullException(nameof(services));

        this.services.AddSingleton(this.handlerCache);
        this.services.AddSingleton(this.policyCache);
        this.services.AddSingleton<INotificationHandlerProvider, NotificationHandlerProvider>();
        this.services.AddSingleton<INotificationBehaviorsProvider>(_ => new NotificationBehaviorsProvider(this.pipelineBehaviorTypes));
        this.services.AddScoped<INotifier>(sp => new Notifier(
            sp,
            sp.GetRequiredService<ILogger<Notifier>>(),
            sp.GetRequiredService<INotificationHandlerProvider>(),
            sp.GetRequiredService<INotificationBehaviorsProvider>(),
            sp.GetRequiredService<IHandlerCache>(),
            this.pipelineBehaviorTypes));
    }

    public NotifierBuilder AddHandlers(IEnumerable<string> blacklistPatterns = null)
    {
        blacklistPatterns ??= Blacklists.ApplicationDependencies;
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.GetName().Name.MatchAny(blacklistPatterns)).ToList();

        foreach (var assembly in assemblies)
        {
            var types = this.SafeGetTypes(assembly);
            foreach (var type in types)
            {
                if (type.IsGenericTypeDefinition)
                {
                    continue;
                }

                var handlerInterfaces = type.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INotificationHandler<>))
                    .ToList();

                if (handlerInterfaces.Count != 0)
                {
                    foreach (var handlerInterface in handlerInterfaces)
                    {
                        var notificationType = handlerInterface.GetGenericArguments()[0];

                        this.handlerCache.TryAdd(handlerInterface, type);
                        this.policyCache.TryAdd(type, new PolicyConfig
                        {
                            Retry = type.GetCustomAttribute<HandlerRetryAttribute>(),
                            Timeout = type.GetCustomAttribute<HandlerTimeoutAttribute>(),
                            Chaos = type.GetCustomAttribute<HandlerChaosAttribute>(),
                            CircuitBreaker = type.GetCustomAttribute<HandlerCircuitBreakerAttribute>(),
                            CacheInvalidate = type.GetCustomAttribute<HandlerCacheInvalidateAttribute>(),
                        });

                        if (!type.IsAbstract)
                        {
                            this.services.AddScoped(handlerInterface, type);
                        }

                        var validatorType = notificationType.GetNestedType("Validator");
                        if (validatorType?.GetInterfaces().Any(i => i == typeof(IValidator<>).MakeGenericType(notificationType)) == true)
                        {
                            if (!this.validatorTypes.Contains(validatorType))
                            {
                                this.validatorTypes.Add(validatorType);
                            }
                            this.services.AddScoped(typeof(IValidator<>).MakeGenericType(notificationType), validatorType);
                        }
                    }
                }
            }
        }

        return this;
    }

    public NotifierBuilder AddHandler<TNotification, THandler>()
        where TNotification : INotification
        where THandler : class, INotificationHandler<TNotification>
    {
        var handlerInterface = typeof(INotificationHandler<TNotification>);
        var handlerType = typeof(THandler);

        this.services.AddScoped(handlerInterface, handlerType);

        if (!handlerType.IsGenericTypeDefinition)
        {
            this.handlerCache.TryAdd(handlerInterface, handlerType);
        }

        this.policyCache.TryAdd(handlerType, new PolicyConfig
        {
            Retry = handlerType.GetCustomAttribute<HandlerRetryAttribute>(),
            Timeout = handlerType.GetCustomAttribute<HandlerTimeoutAttribute>(),
            Chaos = handlerType.GetCustomAttribute<HandlerChaosAttribute>(),
            CircuitBreaker = handlerType.GetCustomAttribute<HandlerCircuitBreakerAttribute>(),
            CacheInvalidate = handlerType.GetCustomAttribute<HandlerCacheInvalidateAttribute>(),
        });

        var notificationType = typeof(TNotification);
        var validatorType = notificationType.GetNestedType("Validator");
        if (validatorType?.GetInterfaces().Any(i => i == typeof(IValidator<>).MakeGenericType(notificationType)) == true)
        {
            if (!this.validatorTypes.Contains(validatorType))
            {
                this.validatorTypes.Add(validatorType);
            }
            this.services.AddScoped(typeof(IValidator<>).MakeGenericType(notificationType), validatorType);
        }

        return this;
    }

    public NotifierBuilder AddGenericHandler(Type genericHandlerType, Type genericNotificationType, Type[] typeArguments)
    {
        if (genericHandlerType?.IsGenericTypeDefinition != true)
        {
            throw new ArgumentException("Generic handler type must be an open generic type definition.", nameof(genericHandlerType));
        }

        if (genericNotificationType?.IsGenericTypeDefinition != true)
        {
            throw new ArgumentException("Generic notification type must be an open generic type definition.", nameof(genericNotificationType));
        }

        if (typeArguments?.Any() != true)
        {
            throw new ArgumentException("At least one type argument must be provided.", nameof(typeArguments));
        }

        var notificationTypeParams = genericNotificationType.GetGenericArguments().Length;
        var handlerTypeParams = genericHandlerType.GetGenericArguments().Length;
        if (notificationTypeParams != handlerTypeParams || typeArguments.Length != notificationTypeParams)
        {
            throw new ArgumentException($"The number of type arguments ({typeArguments.Length}) must match the number of generic parameters in the notification type ({notificationTypeParams}) and handler type ({handlerTypeParams}).");
        }

        var handlerInterface = genericHandlerType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INotificationHandler<>)) ?? throw new ArgumentException($"Generic handler type {genericHandlerType.Name} does not implement INotificationHandler<>.", nameof(genericHandlerType));
        foreach (var typeArg in typeArguments)
        {
            var closedNotificationType = genericNotificationType.MakeGenericType(typeArg);
            var closedHandlerType = genericHandlerType.MakeGenericType(typeArg);
            var closedHandlerInterface = typeof(INotificationHandler<>).MakeGenericType(closedNotificationType);

            this.services.AddScoped(closedHandlerInterface, closedHandlerType);
            this.handlerCache.TryAdd(closedHandlerInterface, closedHandlerType);

            this.policyCache.TryAdd(closedHandlerType, new PolicyConfig
            {
                Retry = closedHandlerType.GetCustomAttribute<HandlerRetryAttribute>(),
                Timeout = closedHandlerType.GetCustomAttribute<HandlerTimeoutAttribute>(),
                Chaos = closedHandlerType.GetCustomAttribute<HandlerChaosAttribute>(),
                CircuitBreaker = closedHandlerType.GetCustomAttribute<HandlerCircuitBreakerAttribute>(),
                CacheInvalidate = closedHandlerType.GetCustomAttribute<HandlerCacheInvalidateAttribute>(),
            });

            var validatorType = closedNotificationType.GetNestedType("Validator");
            if (validatorType?.GetInterfaces().Any(i => i == typeof(IValidator<>).MakeGenericType(closedNotificationType)) == true)
            {
                lock (this.validatorTypes)
                {
                    if (!this.validatorTypes.Contains(validatorType))
                    {
                        this.validatorTypes.Add(validatorType);
                    }
                }
                this.services.AddScoped(typeof(IValidator<>).MakeGenericType(closedNotificationType), validatorType);
            }
        }

        return this;
    }

    public NotifierBuilder AddGenericHandlers(IEnumerable<string> blacklistPatterns = null)
    {
        blacklistPatterns ??= Blacklists.ApplicationDependencies;
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.GetName().Name.MatchAny(blacklistPatterns)).ToList();

        var genericHandlers = new ConcurrentBag<(Type HandlerType, Type NotificationTypeDefinition)>();
        Parallel.ForEach(assemblies, assembly =>
        {
            var types = this.SafeGetTypes(assembly);
            foreach (var type in types)
            {
                if (type.IsAbstract)
                {
                    continue;
                }

                var handlerInterfaces = type.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INotificationHandler<>))
                    .ToList();

                if (handlerInterfaces.Count != 0)
                {
                    foreach (var handlerInterface in handlerInterfaces)
                    {
                        var notificationType = handlerInterface.GetGenericArguments()[0];

                        if (type.IsGenericTypeDefinition)
                        {
                            var notificationTypeDefinition = notificationType.GetGenericTypeDefinition();
                            genericHandlers.Add((type, notificationTypeDefinition));
                        }
                    }
                }
            }
        });

        foreach (var (handlerType, notificationTypeDefinition) in genericHandlers)
        {
            var genericTypeParameters = handlerType.GetGenericArguments();
            if (genericTypeParameters.Length != 1)
            {
                throw new InvalidOperationException($"Handler type {handlerType.Name} must have exactly one generic type parameter for automatic discovery.");
            }

            var typeParameter = genericTypeParameters[0];
            var constraints = typeParameter.GetGenericParameterConstraints();
            var isClassConstraint = (typeParameter.GenericParameterAttributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0;
            var hasDefaultConstructorConstraint = (typeParameter.GenericParameterAttributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0;

            var typeArguments = new ConcurrentBag<Type>();
            Parallel.ForEach(assemblies, assembly =>
            {
                var types = this.SafeGetTypes(assembly);
                foreach (var candidateType in types)
                {
                    if (candidateType.IsAbstract || candidateType.IsInterface || candidateType.IsGenericTypeDefinition)
                    {
                        continue;
                    }

                    if (isClassConstraint && candidateType.IsValueType)
                    {
                        continue;
                    }

                    var satisfiesConstraints = true;

                    if (hasDefaultConstructorConstraint && !candidateType.GetConstructors().Any(c => c.GetParameters().Length == 0))
                    {
                        satisfiesConstraints = false;
                    }

                    if (satisfiesConstraints)
                    {
                        foreach (var constraint in constraints)
                        {
                            if (!constraint.IsAssignableFrom(candidateType))
                            {
                                satisfiesConstraints = false;
                                break;
                            }
                        }
                    }

                    if (satisfiesConstraints)
                    {
                        typeArguments.Add(candidateType);
                    }
                }
            });

            if (typeArguments.IsEmpty)
            {
                throw new InvalidOperationException($"No concrete types found that satisfy the constraints for generic handler {handlerType.Name}.");
            }

            foreach (var typeArg in typeArguments)
            {
                var closedNotificationType = notificationTypeDefinition.MakeGenericType(typeArg);
                var closedHandlerType = handlerType.MakeGenericType(typeArg);
                var closedHandlerInterface = typeof(INotificationHandler<>).MakeGenericType(closedNotificationType);

                this.services.AddScoped(closedHandlerInterface, closedHandlerType);
                this.handlerCache.TryAdd(closedHandlerInterface, closedHandlerType);

                this.policyCache.TryAdd(closedHandlerType, new PolicyConfig
                {
                    Retry = closedHandlerType.GetCustomAttribute<HandlerRetryAttribute>(),
                    Timeout = closedHandlerType.GetCustomAttribute<HandlerTimeoutAttribute>(),
                    Chaos = closedHandlerType.GetCustomAttribute<HandlerChaosAttribute>(),
                    CircuitBreaker = closedHandlerType.GetCustomAttribute<HandlerCircuitBreakerAttribute>(),
                    CacheInvalidate = closedHandlerType.GetCustomAttribute<HandlerCacheInvalidateAttribute>(),
                });

                var validatorType = closedNotificationType.GetNestedType("Validator");
                if (validatorType?.GetInterfaces().Any(i => i == typeof(IValidator<>).MakeGenericType(closedNotificationType)) == true)
                {
                    lock (this.validatorTypes)
                    {
                        if (!this.validatorTypes.Contains(validatorType))
                        {
                            this.validatorTypes.Add(validatorType);
                        }
                    }
                    this.services.AddScoped(typeof(IValidator<>).MakeGenericType(closedNotificationType), validatorType);
                }
            }
        }

        return this;
    }

    /// <summary>
    /// Adds a pipeline behavior to the notification processing pipeline.
    /// </summary>
    /// <param name="behaviorType">The open generic type of the behavior (e.g., typeof(TestBehavior<,>)).</param>
    /// <returns>The <see cref="NotifierBuilder"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="behaviorType"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="behaviorType"/> is not an open generic type.</exception>
    public NotifierBuilder WithBehavior(Type behaviorType)
    {
        if (behaviorType == null)
        {
            return this;
        }

        if (!behaviorType.IsGenericTypeDefinition)
        {
            throw new ArgumentException($"Behavior type '{behaviorType}' must be an open generic type.", nameof(behaviorType));
        }

        this.services.AddScoped(typeof(IPipelineBehavior<,>), behaviorType);
        this.pipelineBehaviorTypes.Add(behaviorType);
        return this;
    }

    private IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null);
        }
    }
}
