// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license
namespace BridgingIT.DevKit.Common;

using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;
using System.Linq;

/// <summary>
/// Configures and builds the Notifier service for dependency injection registration.
/// </summary>
/// <remarks>
/// This class provides a fluent API for registering handlers, behaviors, and providers for the Notifier system.
/// It scans assemblies for handlers and validators, caches handler types in a shared <see cref="IHandlerCache"/>,
/// and registers services with the DI container. Uses <see cref="HandlerCacheFactory"/> to ensure a single shared cache.
/// </remarks>
/// <example>
/// <code>
/// var services = new ServiceCollection();
/// services.AddNotifier()
///     .AddHandlers(new[] { "^System\\..*" })
///     .WithBehavior&lt;ValidationBehavior&lt;,&gt;&gt;();
/// var provider = services.BuildServiceProvider();
/// </code>
/// </example>
public class NotifierBuilder
{
    private readonly IServiceCollection services;
    private readonly List<Type> pipelineBehaviorTypes = [];
    private readonly List<Type> validatorTypes = [];
    private readonly IHandlerCache handlerCache = HandlerCacheFactory.Create();
    private readonly ConcurrentDictionary<Type, PolicyConfig> policyCache = HandlerCacheFactory.CreatePolicyCache();

    /// <summary>
    /// Initializes a new instance of the <see cref="NotifierBuilder"/> class.
    /// </summary>
    /// <param name="services">The service collection for dependency injection registration.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public NotifierBuilder(IServiceCollection services)
    {
        this.services = services ?? throw new ArgumentNullException(nameof(services));
        // Register the core services needed for Notifier to function
        this.services.TryAddSingleton(this.handlerCache);
        this.services.TryAddSingleton(this.policyCache);
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

    /// <summary>
    /// Scans assemblies to register notification handlers, validators, and providers, excluding those matching blacklist patterns.
    /// Populates the shared <see cref="IHandlerCache"/> with handler mappings and registers handlers in the DI container.
    /// </summary>
    /// <param name="blacklistPatterns">Optional regex patterns to exclude assemblies (e.g., "^System\\..*"). Defaults to <see cref="Blacklists.ApplicationDependencies"/>.</param>
    /// <returns>The <see cref="NotifierBuilder"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// Discovers types implementing <see cref="INotificationHandler{TNotification}"/> where <c>TNotification</c> implements
    /// <see cref="INotification"/> directly or indirectly (e.g., via <see cref="IDomainEvent"/>). Also registers nested validators
    /// if they implement <see cref="IValidator{T}"/> for the notification type.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddNotifier()
    ///     .AddHandlers(new[] { "^System\\..*" });
    /// </code>
    /// </example>
    public NotifierBuilder AddHandlers(IEnumerable<string> blacklistPatterns = null)
    {
        blacklistPatterns ??= Blacklists.ApplicationDependencies;
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.GetName().Name.MatchAny(blacklistPatterns)).ToList();
        foreach (var assembly in assemblies)
        {
            foreach (var type in this.SafeGetTypes(assembly))
            {
                if (type.IsGenericTypeDefinition)
                {
                    continue;
                }

                var handlerInterfaces = type.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INotificationHandler<>)).ToList();

                if (handlerInterfaces.Count != 0)
                {
                    foreach (var handlerInterface in handlerInterfaces)
                    {
                        var notificationType = handlerInterface.GetGenericArguments()[0];

                        this.handlerCache.TryAdd(handlerInterface, type);
                        this.policyCache.TryAdd(type, new PolicyConfig
                        {
                            AuthorizePolicy = type.GetCustomAttribute<HandlerAuthorizePolicyAttribute>(),
                            AuthorizeRoles = type.GetCustomAttribute<HandlerAuthorizeRolesAttribute>(),
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

    /// <summary>
    /// Registers a specific notification handler for a given notification type.
    /// </summary>
    /// <typeparam name="TNotification">The notification type, which must implement <see cref="INotification"/>.</typeparam>
    /// <typeparam name="THandler">The handler type, which must implement <see cref="INotificationHandler{TNotification}"/>.</typeparam>
    /// <returns>The <see cref="NotifierBuilder"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// Adds the handler to the DI container and the shared <see cref="IHandlerCache"/>. Also registers any nested
    /// <c>Validator</c> class implementing <see cref="IValidator{T}"/> for the notification type.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddNotifier()
    ///     .AddHandler&lt;PersonCreatedEvent, PersonCreatedHandler&gt;();
    /// </code>
    /// </example>
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
            AuthorizePolicy = handlerType.GetCustomAttribute<HandlerAuthorizePolicyAttribute>(),
            AuthorizeRoles = handlerType.GetCustomAttribute<HandlerAuthorizeRolesAttribute>(),
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

    /// <summary>
    /// Registers generic notification handlers for a specified generic notification type using provided type arguments.
    /// </summary>
    /// <param name="genericHandlerType">The open generic handler type (e.g., <c>typeof(GenericNotificationHandler&lt;&gt;)</c>).</param>
    /// <param name="genericNotificationType">The open generic notification type (e.g., <c>typeof(GenericNotification&lt;&gt;)</c>).</param>
    /// <param name="typeArguments">The list of type arguments to create closed generic handlers (e.g., <c>new[] { typeof(UserData) }</c>).</param>
    /// <returns>The <see cref="NotifierBuilder"/> instance for fluent chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="genericHandlerType"/> or <paramref name="genericNotificationType"/> is not an open generic type definition, or if the number of type arguments does not match the generic parameters.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="genericHandlerType"/>, <paramref name="genericNotificationType"/>, or <paramref name="typeArguments"/> is null or empty.</exception>
    /// <remarks>
    /// Registers closed generic handlers in the DI container and the shared <see cref="IHandlerCache"/>. Also registers
    /// nested validators for the closed notification types if they implement <see cref="IValidator{T}"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddNotifier()
    ///     .AddGenericHandler(typeof(GenericNotificationHandler&lt;&gt;), typeof(GenericNotification&lt;&gt;), new[] { typeof(UserData) });
    /// </code>
    /// </example>
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
                AuthorizePolicy = closedHandlerType.GetCustomAttribute<HandlerAuthorizePolicyAttribute>(),
                AuthorizeRoles = closedHandlerType.GetCustomAttribute<HandlerAuthorizeRolesAttribute>(),
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

    /// <summary>
    /// Automatically discovers and registers generic notification handlers by scanning assemblies for open generic handlers
    /// and their corresponding notification types, based on generic constraints.
    /// </summary>
    /// <param name="blacklistPatterns">Optional regex patterns to exclude assemblies (e.g., "^System\\..*"). Defaults to <see cref="Blacklists.ApplicationDependencies"/>.</param>
    /// <returns>The <see cref="NotifierBuilder"/> instance for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a generic handler has an invalid number of generic parameters or no valid type arguments are found.</exception>
    /// <remarks>
    /// Discovers types implementing <see cref="INotificationHandler{TNotification}"/> where <c>TNotification</c> is a generic
    /// type definition. Registers closed handlers for concrete types satisfying the generic constraints, along with nested validators.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddNotifier()
    ///     .AddGenericHandlers(new[] { "^System\\..*" });
    /// </code>
    /// </example>
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
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INotificationHandler<>)).ToList();
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
                    AuthorizePolicy = closedHandlerType.GetCustomAttribute<HandlerAuthorizePolicyAttribute>(),
                    AuthorizeRoles = closedHandlerType.GetCustomAttribute<HandlerAuthorizeRolesAttribute>(),
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
    /// <param name="behaviorType">The open generic type of the behavior (e.g., <c>typeof(TestBehavior&lt;,&gt;)</c>).</param>
    /// <returns>The <see cref="NotifierBuilder"/> instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="behaviorType"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="behaviorType"/> is not an open generic type.</exception>
    /// <remarks>
    /// Behaviors are executed in the order they are added and can handle cross-cutting concerns like validation or logging.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddNotifier()
    ///     .WithBehavior&lt;ValidationBehavior&lt;,&gt;&gt;();
    /// </code>
    /// </example>
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

    /// <summary>
    /// Safely retrieves types from an assembly, handling reflection exceptions.
    /// </summary>
    /// <param name="assembly">The assembly to scan for types.</param>
    /// <returns>An enumerable of types in the assembly, excluding null types from reflection errors.</returns>
    /// <remarks>
    /// Catches <see cref="ReflectionTypeLoadException"/> and returns non-null types to ensure robust assembly scanning.
    /// </remarks>
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