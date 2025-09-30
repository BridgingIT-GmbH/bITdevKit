// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using BridgingIT.DevKit.Common;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Configures and builds the Requester service for dependency injection registration.
/// </summary>
/// <remarks>
/// This class provides a fluent API for registering handlers, behaviors, and providers for the Requester system.
/// It scans assemblies for handlers and validators, caches handler types, and registers services with the DI container.
/// </remarks>
/// <example>
/// <code>
/// var services = new ServiceCollection();
/// services.AddRequester()
///     .AddHandlers(new[] { "^System\\..*" })
///     .WithBehavior<ValidationBehavior<,>>();
/// var provider = services.BuildServiceProvider();
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="RequesterBuilder"/> class.
/// </remarks>
/// <param name="services">The service collection for dependency injection registration.</param>
public class RequesterBuilder
{
    private readonly IServiceCollection services;
    private readonly List<Type> pipelineBehaviorTypes = [];
    private readonly List<Type> validatorTypes = [];
    private readonly IHandlerCache handlerCache = HandlerCacheFactory.Create();
    private readonly ConcurrentDictionary<Type, PolicyConfig> policyCache = HandlerCacheFactory.CreatePolicyCache();

    public RequesterBuilder(IServiceCollection services)
    {
        this.services = services ?? throw new ArgumentNullException(nameof(services));

        // Register the core services needed for Requester to function
        this.services.TryAddSingleton(this.handlerCache);
        this.services.TryAddSingleton(this.policyCache);
        this.services.AddSingleton<IRequestHandlerProvider, RequestHandlerProvider>();
        this.services.AddSingleton<IRequestBehaviorsProvider>(sp => new RequestBehaviorsProvider(this.pipelineBehaviorTypes));
        this.services.AddScoped<IRequester>(sp => new Requester(
            sp,
            sp.GetRequiredService<ILogger<Requester>>(),
            sp.GetRequiredService<IRequestHandlerProvider>(),
            sp.GetRequiredService<IRequestBehaviorsProvider>(),
            sp.GetRequiredService<IHandlerCache>(),
            this.pipelineBehaviorTypes));
    }

    /// <summary>
    /// Adds handlers, validators, and providers by scanning all loaded assemblies, excluding those matching blacklist patterns.
    /// </summary>
    /// <param name="blacklistPatterns">Optional regex patterns to exclude assemblies.</param>
    /// <returns>The <see cref="RequesterBuilder"/> for fluent chaining.</returns>
    public RequesterBuilder AddHandlers(IEnumerable<string> blacklistPatterns = null)
    {
        blacklistPatterns ??= Blacklists.ApplicationDependencies; // ["^System\\..*"];
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.GetName().Name.MatchAny(blacklistPatterns)).ToList();
        foreach (var assembly in assemblies)
        {
            var types = this.SafeGetTypes(assembly);
            foreach (var type in types)
            {
                if (type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
                {
                    var requestType = type.GetInterfaces()
                        .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
                        .GetGenericArguments()[0];
                    var valueType = type.GetInterfaces()
                        .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
                        .GetGenericArguments()[1];

                    this.handlerCache.TryAdd(typeof(IRequestHandler<,>).MakeGenericType(requestType, valueType), type);
                    this.policyCache.TryAdd(type, new PolicyConfig
                    {
                        Retry = type.GetCustomAttribute<HandlerRetryAttribute>(),
                        Timeout = type.GetCustomAttribute<HandlerTimeoutAttribute>(),
                        Chaos = type.GetCustomAttribute<HandlerChaosAttribute>(),
                        CircuitBreaker = type.GetCustomAttribute<HandlerCircuitBreakerAttribute>(),
                        CacheInvalidate = type.GetCustomAttribute<HandlerCacheInvalidateAttribute>(),
                    });

                    if (!type.IsAbstract) // Only register concrete (non-abstract) types in the DI container
                    {
                        this.services.AddScoped(type);
                    }

                    var validatorType = requestType.GetNestedType("Validator");
                    if (validatorType?.GetInterfaces().Any(i => i == typeof(IValidator<>).MakeGenericType(requestType)) == true)
                    {
                        this.validatorTypes.Add(validatorType);
                        this.services.AddScoped(typeof(IValidator<>).MakeGenericType(requestType), validatorType);
                    }
                }
            }
        }

        return this;
    }

    /// <summary>
    /// Adds a specific handler for a specific request type, usefull for generic handlers.
    /// </summary>
    public RequesterBuilder AddHandler<TRequest, TValue, THandler>()
        where TRequest : IRequest<TValue>
        where THandler : class, IRequestHandler<TRequest, TValue>
    {
        var handlerInterface = typeof(IRequestHandler<TRequest, TValue>);
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

        var requestType = typeof(TRequest);
        var validatorType = requestType.GetNestedType("Validator");
        if (validatorType?.GetInterfaces().Any(i => i == typeof(IValidator<>).MakeGenericType(requestType)) == true)
        {
            this.validatorTypes.Add(validatorType);
            this.services.AddScoped(typeof(IValidator<>).MakeGenericType(requestType), validatorType);
        }

        return this;
    }

    /// <summary>
    /// Adds generic handlers for the specified generic request type, using the provided type arguments.
    /// The value type (TValue) is inferred from the generic handler's interface.
    /// </summary>
    /// <param name="genericHandlerType">The open generic handler type (e.g., typeof(GenericDataProcessor<>))</param>
    /// <param name="genericRequestType">The open generic request type (e.g., typeof(ProcessDataRequest<>))</param>
    /// <param name="typeArguments">The list of type arguments to create closed generic handlers (e.g., new[] { typeof(UserData), typeof(string) })</param>
    /// <returns>The <see cref="RequesterBuilder"/> for fluent chaining.</returns>
    public RequesterBuilder AddGenericHandler(Type genericHandlerType, Type genericRequestType, Type[] typeArguments)
    {
        if (genericHandlerType?.IsGenericTypeDefinition != true)
        {
            throw new ArgumentException("Generic handler type must be an open generic type definition.", nameof(genericHandlerType));
        }

        if (genericRequestType?.IsGenericTypeDefinition != true)
        {
            throw new ArgumentException("Generic request type must be an open generic type definition.", nameof(genericRequestType));
        }

        if (typeArguments?.Any() != true)
        {
            throw new ArgumentException("At least one type argument must be provided.", nameof(typeArguments));
        }

        // Validate that the number of type arguments matches the generic parameters of the request and handler types
        var requestTypeParams = genericRequestType.GetGenericArguments().Length;
        var handlerTypeParams = genericHandlerType.GetGenericArguments().Length;
        if (requestTypeParams != handlerTypeParams || typeArguments.Length != requestTypeParams)
        {
            throw new ArgumentException($"The number of type arguments ({typeArguments.Length}) must match the number of generic parameters in the request type ({requestTypeParams}) and handler type ({handlerTypeParams}).");
        }

        // Determine the value type (TValue) from the generic handler's interface
        var handlerInterface = genericHandlerType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)) ?? throw new ArgumentException($"Generic handler type {genericHandlerType.Name} does not implement IRequestHandler<,>.", nameof(genericHandlerType));

        // The value type is the second generic argument of the IRequestHandler<,> interface (TValue)
        var valueType = handlerInterface.GetGenericArguments()[1];

        // Register closed generic handlers for each type argument
        foreach (var typeArg in typeArguments)
        {
            // Create the closed generic request type (e.g., ProcessDataRequest<UserData>)
            var closedRequestType = genericRequestType.MakeGenericType(typeArg);

            // Create the closed generic handler type (e.g., GenericDataProcessor<UserData>)
            var closedHandlerType = genericHandlerType.MakeGenericType(typeArg);

            // Create the closed generic interface (e.g., IRequestHandler<ProcessDataRequest<UserData>, string>)
            var closedHandlerInterface = typeof(IRequestHandler<,>).MakeGenericType(closedRequestType, valueType);

            // Register in DI
            this.services.AddScoped(closedHandlerInterface, closedHandlerType);

            // Register in handlerCache (since this is a closed generic type)
            this.handlerCache.TryAdd(closedHandlerInterface, closedHandlerType);

            // Register policies for the closed handler type
            this.policyCache.TryAdd(closedHandlerType, new PolicyConfig
            {
                Retry = closedHandlerType.GetCustomAttribute<HandlerRetryAttribute>(),
                Timeout = closedHandlerType.GetCustomAttribute<HandlerTimeoutAttribute>(),
                Chaos = closedHandlerType.GetCustomAttribute<HandlerChaosAttribute>(),
                CircuitBreaker = closedHandlerType.GetCustomAttribute<HandlerCircuitBreakerAttribute>(),
                CacheInvalidate = closedHandlerType.GetCustomAttribute<HandlerCacheInvalidateAttribute>(),
            });

            // Register validator if present
            var validatorType = closedRequestType.GetNestedType("Validator");
            if (validatorType?.GetInterfaces().Any(i => i == typeof(IValidator<>).MakeGenericType(closedRequestType)) == true)
            {
                this.validatorTypes.Add(validatorType);
                this.services.AddScoped(typeof(IValidator<>).MakeGenericType(closedRequestType), validatorType);
            }
        }

        return this;
    }

    /// <summary>
    /// Automatically discovers and registers generic handlers for generic requests.
    /// Uses reflection to find open generic handlers, their corresponding generic requests,
    /// and type arguments based on the generic constraints defined on the handler.
    /// </summary>
    /// <param name="blacklistPatterns">Optional regex patterns to exclude assemblies.</param>
    /// <returns>The <see cref="RequesterBuilder"/> for fluent chaining.</returns>
    public RequesterBuilder AddGenericHandlers(IEnumerable<string> blacklistPatterns = null)
    {
        blacklistPatterns ??= Blacklists.ApplicationDependencies;
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.GetName().Name.MatchAny(blacklistPatterns)).ToList();

        // Use ConcurrentBag for thread-safe collection of generic handlers
        var genericHandlers = new ConcurrentBag<(Type HandlerType, Type RequestTypeDefinition, Type ValueType)>();
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
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
                    .ToList();

                if (handlerInterfaces.Count != 0)
                {
                    foreach (var handlerInterface in handlerInterfaces)
                    {
                        var requestType = handlerInterface.GetGenericArguments()[0];
                        var valueType = handlerInterface.GetGenericArguments()[1];

                        if (type.IsGenericTypeDefinition)
                        {
                            var requestTypeDefinition = requestType.GetGenericTypeDefinition();
                            genericHandlers.Add((type, requestTypeDefinition, valueType));
                        }
                    }
                }
            }
        });

        foreach (var (handlerType, requestTypeDefinition, valueType) in genericHandlers)
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

            // Use ConcurrentBag for thread-safe collection of type arguments
            var typeArguments = new ConcurrentBag<Type>();
            Parallel.ForEach(assemblies, assembly =>
            {
                var types = this.SafeGetTypes(assembly);
                foreach (var candidateType in types)
                {
                    // Pre-filter types to reduce expensive reflection checks
                    if (candidateType.IsAbstract || candidateType.IsInterface || candidateType.IsGenericTypeDefinition)
                    {
                        continue;
                    }

                    // Fast checks first: class constraint and value type
                    if (isClassConstraint && candidateType.IsValueType)
                    {
                        continue;
                    }

                    var satisfiesConstraints = true;

                    // Check for parameterless constructor if required
                    if (hasDefaultConstructorConstraint && !candidateType.GetConstructors().Any(c => c.GetParameters().Length == 0))
                    {
                        satisfiesConstraints = false;
                    }

                    // Check interface/base class constraints
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
                var closedRequestType = requestTypeDefinition.MakeGenericType(typeArg);
                var closedHandlerType = handlerType.MakeGenericType(typeArg);
                var closedHandlerInterface = typeof(IRequestHandler<,>).MakeGenericType(closedRequestType, valueType);

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

                var validatorType = closedRequestType.GetNestedType("Validator");
                if (validatorType?.GetInterfaces().Any(i => i == typeof(IValidator<>).MakeGenericType(closedRequestType)) == true)
                {
                    this.validatorTypes.Add(validatorType);
                    this.services.AddScoped(typeof(IValidator<>).MakeGenericType(closedRequestType), validatorType);
                }
            }
        }

        return this;
    }

    /// <summary>
    /// Adds a pipeline behavior to the request processing pipeline.
    /// </summary>
    /// <param name="behaviorType">The open generic type of the behavior (e.g., typeof(TestBehavior<,>)).</param>
    /// <returns>The <see cref="RequesterBuilder"/> for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="behaviorType"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="behaviorType"/> is not an open generic type.</exception>
    public RequesterBuilder WithBehavior(Type behaviorType)
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
