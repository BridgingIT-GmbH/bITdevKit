// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license
namespace BridgingIT.DevKit.Common;
using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Configures and builds the Requester service for dependency injection registration.
/// </summary>
/// <remarks>
/// This class provides a fluent API for registering handlers, behaviors, and providers for the Requester system.
/// It scans assemblies for handlers and validators, caches handler types in a shared <see cref="IHandlerCache"/>,
/// and registers services with the DI container. Uses <see cref="HandlerCacheFactory"/> to ensure a single shared cache.
/// </remarks>
/// <example>
/// <code>
/// var services = new ServiceCollection();
/// services.AddRequester()
///     .AddHandlers(new[] { "^System\\..*" })
///     .WithBehavior&lt;ValidationBehavior&lt;,&gt;&gt;();
/// var provider = services.BuildServiceProvider();
/// </code>
/// </example>
public class RequesterBuilder
{
    private readonly IServiceCollection services;
    private readonly List<Type> pipelineBehaviorTypes = [];
    private readonly List<Type> validatorTypes = [];
    private readonly IHandlerCache handlerCache = HandlerCacheFactory.Create();
    private readonly ConcurrentDictionary<Type, PolicyConfig> policyCache = HandlerCacheFactory.CreatePolicyCache();

    /// <summary>
    /// Initializes a new instance of the <see cref="RequesterBuilder"/> class.
    /// </summary>
    /// <param name="services">The service collection for dependency injection registration.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
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
    /// Scans assemblies to register request handlers, validators, and providers, excluding those matching blacklist patterns.
    /// Populates the shared <see cref="IHandlerCache"/> with handler mappings and registers handlers in the DI container.
    /// </summary>
    /// <param name="blacklistPatterns">Optional regex patterns to exclude assemblies (e.g., "^System\\..*"). Defaults to <see cref="Blacklists.ApplicationDependencies"/>.</param>
    /// <returns>The <see cref="RequesterBuilder"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// Discovers types implementing <see cref="IRequestHandler{TRequest, TValue}"/> and registers nested validators
    /// if they implement <see cref="IValidator{T}"/> for the request type.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddRequester()
    ///     .AddHandlers(new[] { "^System\\..*" });
    /// </code>
    /// </example>
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
    /// Registers a specific request handler for a given request type.
    /// </summary>
    /// <typeparam name="TRequest">The request type, which must implement <see cref="IRequest{TValue}"/>.</typeparam>
    /// <typeparam name="TValue">The type of the value returned by the request.</typeparam>
    /// <typeparam name="THandler">The handler type, which must implement <see cref="IRequestHandler{TRequest, TValue}"/>.</typeparam>
    /// <returns>The <see cref="RequesterBuilder"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// Adds the handler to the DI container and the shared <see cref="IHandlerCache"/>. Also registers any nested
    /// <c>Validator</c> class implementing <see cref="IValidator{T}"/> for the request type.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddRequester()
    ///     .AddHandler&lt;GetUserQuery, User, GetUserQueryHandler&gt;();
    /// </code>
    /// </example>
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
    /// Registers generic request handlers for a specified generic request type using provided type arguments.
    /// </summary>
    /// <param name="genericHandlerType">The open generic handler type (e.g., <c>typeof(GenericDataProcessor&lt;&gt;)</c>).</param>
    /// <param name="genericRequestType">The open generic request type (e.g., <c>typeof(ProcessDataRequest&lt;&gt;)</c>).</param>
    /// <param name="typeArguments">The list of type arguments to create closed generic handlers (e.g., <c>new[] { typeof(UserData) }</c>).</param>
    /// <returns>The <see cref="RequesterBuilder"/> instance for fluent chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="genericHandlerType"/> or <paramref name="genericRequestType"/> is not an open generic type definition, or if the number of type arguments does not match the generic parameters.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="genericHandlerType"/>, <paramref name="genericRequestType"/>, or <paramref name="typeArguments"/> is null or empty.</exception>
    /// <remarks>
    /// Registers closed generic handlers in the DI container and the shared <see cref="IHandlerCache"/>. Also registers
    /// nested validators for the closed request types if they implement <see cref="IValidator{T}"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddRequester()
    ///     .AddGenericHandler(typeof(GenericDataProcessor&lt;&gt;), typeof(ProcessDataRequest&lt;&gt;), new[] { typeof(UserData) });
    /// </code>
    /// </example>
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
        var valueType = handlerInterface.GetGenericArguments()[1];

        foreach (var typeArg in typeArguments)
        {
            var closedRequestType = genericRequestType.MakeGenericType(typeArg);
            var closedHandlerType = genericHandlerType.MakeGenericType(typeArg);
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
        return this;
    }

    /// <summary>
    /// Automatically discovers and registers generic request handlers by scanning assemblies for open generic handlers
    /// and their corresponding request types, based on generic constraints.
    /// </summary>
    /// <param name="blacklistPatterns">Optional regex patterns to exclude assemblies (e.g., "^System\\..*"). Defaults to <see cref="Blacklists.ApplicationDependencies"/>.</param>
    /// <returns>The <see cref="RequesterBuilder"/> instance for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a generic handler has an invalid number of generic parameters or no valid type arguments are found.</exception>
    /// <remarks>
    /// Discovers types implementing <see cref="IRequestHandler{TRequest, TValue}"/> where <c>TRequest</c> is a generic
    /// type definition. Registers closed handlers for concrete types satisfying the generic constraints, along with nested validators.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddRequester()
    ///     .AddGenericHandlers(new[] { "^System\\..*" });
    /// </code>
    /// </example>
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
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)).ToList();
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
    /// <param name="behaviorType">The open generic type of the behavior (e.g., <c>typeof(TestBehavior&lt;,&gt;)</c>).</param>
    /// <returns>The <see cref="RequesterBuilder"/> instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="behaviorType"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="behaviorType"/> is not an open generic type.</exception>
    /// <remarks>
    /// Behaviors are executed in the order they are added and can handle cross-cutting concerns like validation or logging.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddRequester()
    ///     .WithBehavior&lt;ValidationBehavior&lt;,&gt;&gt;();
    /// </code>
    /// </example>
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