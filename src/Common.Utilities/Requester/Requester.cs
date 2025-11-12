// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Data;
using System.Reflection;

/// <summary>
/// Defines a request that can be dispatched to a handler.
/// </summary>
/// <typeparam name="TValue">The type of the response value returned by the handler.</typeparam>
/// <remarks>
/// This interface is implemented by request classes to provide metadata for tracking and auditing.
/// It is used by the Requester to dispatch requests to their corresponding handlers.
/// </remarks>
public interface IRequest<TValue> : IRequest;

public interface IRequest
{
    /// <summary>
    /// Gets the unique identifier for the request.
    /// </summary>
    Guid RequestId { get; }

    /// <summary>
    /// Gets the UTC timestamp when the request was created.
    /// </summary>
    DateTimeOffset RequestTimestamp { get; }
}

/// <summary>
/// Base class for requests, providing metadata and implementing <see cref="IRequest{TValue}"/>.
/// </summary>
/// <typeparam name="TValue">The type of the response value returned by the handler.</typeparam>
/// <remarks>
/// This abstract class provides a default implementation for request metadata, including a unique
/// <see cref="RequestId"/> generated using <see cref="GuidGenerator"/> and a <see cref="RequestTimestamp"/>
/// set to the current UTC time. Concrete request classes should inherit from this base class.
/// </remarks>
/// <example>
/// <code>
/// public class CustomerCreateCommand : RequestBase<string>
/// {
///     public string Name { get; set; }
/// }
/// </code>
/// </example>
public abstract class RequestBase<TValue> : IRequest<TValue>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequestBase{TValue}"/> class.
    /// </summary>
    protected RequestBase()
    {
        this.RequestId = GuidGenerator.CreateSequential();
        this.RequestTimestamp = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets the unique identifier for the request.
    /// </summary>
    public Guid RequestId { get; }

    /// <summary>
    /// Gets the UTC timestamp when the request was created.
    /// </summary>
    public DateTimeOffset RequestTimestamp { get; }
}

/// <summary>
/// Defines a handler for a specific request type.
/// </summary>
/// <typeparam name="TRequest">The type of the request to handle.</typeparam>
/// <typeparam name="TValue">The type of the response value returned by the handler.</typeparam>
/// <remarks>
/// This interface is implemented by handler classes to process requests dispatched by the Requester.
/// Handlers return a <see cref="Result{TValue}"/> to indicate success or failure, and support
/// <see cref="SendOptions"/> for context and progress reporting.
/// </remarks>
/// <example>
/// <code>
/// public class CustomerCreateCommandHandler : RequestHandlerBase<CustomerCreateCommand, string>
/// {
///     protected override async Task<Result<string>> HandleAsync(CustomerCreateCommand request, SendOptions options, CancellationToken cancellationToken)
///     {
///         // Implementation
///         return Result<string>.Success("Customer created");
///     }
/// }
/// </code>
/// </example>
public interface IRequestHandler<in TRequest, TValue>
    where TRequest : IRequest<TValue>
{
    /// <summary>
    /// Handles the specified request asynchronously.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="options">The options for request processing, including context and progress reporting.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task representing the result of the request handling, returning a <see cref="Result{TValue}"/>.</returns>
    Task<Result<TValue>> HandleAsync(TRequest request, SendOptions options, CancellationToken cancellationToken);
}

/// <summary>
/// Base class for request handlers, providing a template for handling requests.
/// </summary>
/// <typeparam name="TRequest">The type of the request to handle.</typeparam>
/// <typeparam name="TValue">The type of the response value returned by the handler.</typeparam>
/// <remarks>
/// This abstract class implements <see cref="IRequestHandler{TRequest, TValue}"/> and requires
/// derived classes to provide the handling logic in the <see cref="HandleAsync"/> method.
/// It serves as a foundation for concrete handlers, ensuring consistent implementation patterns.
/// </remarks>
/// <example>
/// <code>
/// public class CustomerCreateCommandHandler : RequestHandlerBase<CustomerCreateCommand, string>
/// {
///     protected override async Task<Result<string>> HandleAsync(CustomerCreateCommand request, SendOptions options, CancellationToken cancellationToken)
///     {
///         // Implementation
///         return Result<string>.Success("Customer created");
///     }
/// }
/// </code>
/// </example>
public abstract class RequestHandlerBase<TRequest, TValue> : IRequestHandler<TRequest, TValue>
    where TRequest : IRequest<TValue>
{
    /// <summary>
    /// Handles the specified request asynchronously.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="options">The options for request processing, including context and progress reporting.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task representing the result of the request handling, returning a <see cref="Result{TValue}"/>.</returns>
    async Task<Result<TValue>> IRequestHandler<TRequest, TValue>.HandleAsync(TRequest request, SendOptions options, CancellationToken cancellationToken)
    {
        return await this.HandleAsync(request, options, cancellationToken);
    }

    /// <summary>
    /// When implemented in a derived class, handles the specified request asynchronously.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="options">The options for request processing, including context and progress reporting.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task representing the result of the request handling, returning a <see cref="Result{TValue}"/>.</returns>
    protected abstract Task<Result<TValue>> HandleAsync(TRequest request, SendOptions options, CancellationToken cancellationToken);
}

/// <summary>
/// Options for configuring request processing.
/// </summary>
/// <remarks>
/// This class provides configuration options for request processing, including a <see cref="RequestContext"/>
/// for contextual data (e.g., UserId, Locale) and a <see cref="Progress"/> reporter for tracking progress.
/// It is used by the Requester and handlers to access additional context and report progress during request handling.
/// </remarks>
/// <example>
/// <code>
/// var options = new SendOptions
/// {
///     Context = new RequestContext { Properties = { ["UserId"] = "user123", ["Locale"] = "en-US" } },
///     Progress = new Progress<ProgressReport>(report => Console.WriteLine($"Progress: {report.Messages[0]} ({report.PercentageComplete}%)"))
/// };
/// var result = await requester.SendAsync<SampleRequest, string>(new SampleRequest(), options);
/// </code>
/// </example>
public class SendOptions
{
    /// <summary>
    /// Gets or sets the context for the request, containing properties like UserId or Locale.
    /// </summary>
    public RequestContext Context { get; set; }

    /// <summary>
    /// Gets or sets the progress reporter for tracking request processing.
    /// </summary>
    public IProgress<ProgressReport> Progress { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether exceptions should be caught and returned as a failed result with an error.
    /// If set to <c>false</c>, exceptions will be thrown instead of being converted to a result.
    /// </summary>
    /// <value>
    /// <c>true</c> to handle exceptions as result errors (default); <c>false</c> to throw exceptions.
    /// </value>
    public bool HandleExceptionsAsResultError { get; set; } = true;
}

/// <summary>
/// Represents contextual properties for request or notification processing.
/// </summary>
/// <remarks>
/// This class stores a dictionary of properties (e.g., UserId, Locale) that provide context for request or notification handling.
/// It is used within <see cref="SendOptions"/> for requests and <see cref="PublishOptions"/> for notifications, allowing
/// handlers and behaviors to access contextual data.
/// </remarks>
/// <example>
/// <code>
/// var context = new RequestContext
/// {
///     Properties = { ["UserId"] = "user123", ["Locale"] = "en-US" }
/// };
/// var options = new SendOptions { Context = context };
/// </code>
/// </example>
public class RequestContext
{
    /// <summary>
    /// Gets the dictionary of contextual properties.
    /// </summary>
    public Dictionary<string, string> Properties { get; } = [];

    /// <summary>
    /// Retrieves a property value by key, returning null if not found.
    /// </summary>
    /// <param name="key">The property key to retrieve.</param>
    /// <returns>The property value, or null if the key does not exist.</returns>
    public string GetProperty(string key)
    {
        return this.Properties.TryGetValue(key, out var value) ? value : null;
    }
}

/// <summary>
/// Defines a provider for resolving request handlers.
/// </summary>
/// <remarks>
/// This interface is used by the Requester to resolve handlers for specific request types.
/// Implementations should retrieve handlers from a cache or dependency injection, ensuring
/// scoped lifetime resolution.
/// </remarks>
/// <example>
/// <code>
/// public class SampleRequestHandlerProvider : IRequestHandlerProvider
/// {
///     public IRequestHandler<TRequest, TValue> GetHandler<TRequest, TValue>(IServiceProvider serviceProvider)
///         where TRequest : IRequest<TValue>
///     {
///         return serviceProvider.GetService<IRequestHandler<TRequest, TValue>>();
///     }
/// }
/// </code>
/// </example>
public interface IRequestHandlerProvider
{
    /// <summary>
    /// Resolves the handler for a specific request type.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TValue">The type of the response value.</typeparam>
    /// <param name="serviceProvider">The service provider for resolving services.</param>
    /// <returns>The handler for the request.</returns>
    /// <exception cref="RequesterException">Thrown when no handler is found for the request type.</exception>
    IRequestHandler<TRequest, TValue> GetHandler<TRequest, TValue>(IServiceProvider serviceProvider)
        where TRequest : IRequest<TValue>;
}

/// <summary>
/// Provides handlers for requests using a cached dictionary of handler types.
/// </summary>
/// <remarks>
/// This class implements <see cref="IRequestHandlerProvider"/> to resolve request handlers from a cached
/// dictionary populated during dependency injection registration. It uses the provided
/// <see cref="IServiceProvider"/> to instantiate handlers, ensuring scoped lifetime resolution.
/// </remarks>
/// <example>
/// <code>
/// var provider = new RequestHandlerProvider(handlerCache);
/// var handler = provider.GetHandler<SampleRequest, string>(serviceProvider);
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="RequestHandlerProvider"/> class.
/// </remarks>
/// <param name="handlerCache">The cache of handler types, mapping request handler interfaces to concrete types.</param>
public class RequestHandlerProvider(IHandlerCache handlerCache) : IRequestHandlerProvider
{
    private readonly IHandlerCache handlerCache = handlerCache ?? throw new ArgumentNullException(nameof(handlerCache));

    /// <summary>
    /// Resolves the handler for a specific request type.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TValue">The type of the response value.</typeparam>
    /// <param name="serviceProvider">The service provider for resolving services.</param>
    /// <returns>The handler for the request.</returns>
    /// <exception cref="RequesterException">Thrown when no handler is found for the request type.</exception>
    public IRequestHandler<TRequest, TValue> GetHandler<TRequest, TValue>(IServiceProvider serviceProvider)
        where TRequest : IRequest<TValue>
    {
        var handlerInterface = typeof(IRequestHandler<TRequest, TValue>);
        var requestType = typeof(TRequest);

        // Only use the handlerCache for non-generic request types
        if (!requestType.IsGenericType && this.handlerCache.TryGetValue(handlerInterface, out var handlerType))
        {
            try
            {
                return (IRequestHandler<TRequest, TValue>)serviceProvider.GetRequiredService(handlerType);
            }
            catch (Exception ex)
            {
                throw new RequesterException($"No handler found for request type {requestType.Name}", ex);
            }
        }

        // Resolve directly from IServiceProvider (for generic handlers or if not found in cache)
        try
        {
            return serviceProvider.GetRequiredService<IRequestHandler<TRequest, TValue>>();
        }
        catch (Exception ex)
        {
            throw new RequesterException($"No handler found for request type {requestType.Name}", ex);
        }
    }
}

/// <summary>
/// Defines a provider for resolving pipeline behaviors for requests.
/// </summary>
/// <remarks>
/// This interface is used by the Requester to resolve a list of pipeline behaviors for specific request types.
/// Implementations should retrieve behaviors from a registered list, ensuring scoped lifetime resolution
/// via dependency injection.
/// </remarks>
/// <example>
/// <code>
/// public class SampleBehaviorsProvider : IRequestBehaviorsProvider
/// {
///     public IReadOnlyList<IPipelineBehavior<TRequest, Result<TValue>>> GetBehaviors<TRequest, TValue>(IServiceProvider serviceProvider)
///         where TRequest : IRequest<TValue>
///     {
///         return new List<IPipelineBehavior<TRequest, Result<TValue>>> { serviceProvider.GetService<ValidationBehavior<TRequest, Result<TValue>>>() };
///     }
/// }
/// </code>
/// </example>
public interface IRequestBehaviorsProvider
{
    IReadOnlyList<IPipelineBehavior<TRequest, IResult<TValue>>> GetBehaviors<TRequest, TValue>(IServiceProvider serviceProvider)
        where TRequest : class, IRequest<TValue>;
}

/// <summary>
/// Provides pipeline behaviors for requests using registered behavior types.
/// </summary>
/// <remarks>
/// This class implements <see cref="IRequestBehaviorsProvider"/> to resolve pipeline behaviors from a list
/// of registered behavior types, populated during dependency injection registration. It uses the provided
/// <see cref="IServiceProvider"/> to instantiate behaviors, ensuring scoped lifetime resolution.
/// Behaviors are resolved in the order they were registered.
/// </remarks>
/// <example>
/// <code>
/// var provider = new RequestBehaviorsProvider(pipelineBehaviorTypes);
/// var behaviors = provider.GetBehaviors<SampleRequest, string>(serviceProvider);
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="RequestBehaviorsProvider"/> class.
/// </remarks>
/// <param name="pipelineBehaviorTypes">The list of registered behavior types.</param>
public class RequestBehaviorsProvider(IReadOnlyList<Type> pipelineBehaviorTypes) : IRequestBehaviorsProvider
{
    private readonly IReadOnlyList<Type> pipelineBehaviorTypes = pipelineBehaviorTypes ?? throw new ArgumentNullException(nameof(pipelineBehaviorTypes));
    //private readonly Dictionary<Type, object> behaviorCache = []; // Scoped cache per provider instance

    public IReadOnlyList<IPipelineBehavior<TRequest, IResult<TValue>>> GetBehaviors<TRequest, TValue>(IServiceProvider serviceProvider)
        where TRequest : class, IRequest<TValue>
    {
        if (serviceProvider == null || this.pipelineBehaviorTypes.Count == 0)
        {
            return Array.Empty<IPipelineBehavior<TRequest, IResult<TValue>>>();
        }

        var behaviorType = typeof(IPipelineBehavior<TRequest, IResult<TValue>>);
        //if (this.behaviorCache.TryGetValue(behaviorType, out var cachedBehaviors)) // WARN: causes issues with scoped services in behaviors
        //{
        //    return (IReadOnlyList<IPipelineBehavior<TRequest, IResult<TValue>>>)cachedBehaviors;
        //}

        var allBehaviors = serviceProvider.GetServices(behaviorType).Cast<IPipelineBehavior<TRequest, IResult<TValue>>>().ToArray();
        if (allBehaviors.Length == 0 && this.pipelineBehaviorTypes.Count > 0)
        {
            throw new InvalidOperationException($"No service for type '{behaviorType}' has been registered.");
        }

        var orderedBehaviors = new IPipelineBehavior<TRequest, IResult<TValue>>[this.pipelineBehaviorTypes.Count];
        var index = 0;
        for (var i = 0; i < this.pipelineBehaviorTypes.Count; i++)
        {
            var type = this.pipelineBehaviorTypes[i];
            for (var j = 0; j < allBehaviors.Length; j++)
            {
                if (allBehaviors[j].GetType().GetGenericTypeDefinition() == type)
                {
                    orderedBehaviors[index++] = allBehaviors[j];
                    break;
                }
            }
        }

        if (index == 0 && this.pipelineBehaviorTypes.Count > 0)
        {
            throw new InvalidOperationException($"No service for type '{behaviorType}' has been registered.");
        }

        var finalBehaviors = index < orderedBehaviors.Length ? orderedBehaviors.AsSpan(0, index).ToArray() : orderedBehaviors;
        //this.behaviorCache[behaviorType] = finalBehaviors; // Cache for scope lifetime // WARN: causes issues with scoped services in behaviors
        return finalBehaviors;
    }
}

/// <summary>
/// Defines the interface for dispatching requests.
/// </summary>
/// <remarks>
/// This interface provides methods for sending requests to their handlers through a pipeline of behaviors
/// and retrieving registration information about handlers and behaviors.
/// </remarks>
/// <example>
/// <code>
/// var requester = serviceProvider.GetRequiredService<IRequester>();
/// var request = new SampleRequest();
/// var result = await requester.SendAsync<SampleRequest, string>(request);
/// var info = requester.GetRegistrationInformation();
/// </code>
/// </example>
public interface IRequester
{
    /// <summary>
    /// Dispatches a request to its handler asynchronously.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TValue">The type of the response value.</typeparam>
    /// <param name="request">The request to dispatch.</param>
    /// <param name="options">The options for request processing, including context and progress reporting.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task representing the result of the request, returning a <see cref="Result{TValue}"/>.</returns>
    Task<Result<TValue>> SendAsync<TRequest, TValue>(
        TRequest request,
        SendOptions options = null,
        CancellationToken cancellationToken = default)
        where TRequest : class, IRequest<TValue>;

    /// <summary>
    /// Dispatches a request to its handler asynchronously, inferring the response type from the request.
    /// </summary>
    /// <typeparam name="TValue">The type of the response value.</typeparam>
    /// <param name="request">The request to dispatch.</param>
    /// <param name="options">The options for request processing, including context and progress reporting.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task representing the result of the request, returning a <see cref="Result{TValue}"/>.</returns>
    Task<Result<TValue>> SendAsync<TValue>(
        IRequest<TValue> request,
        SendOptions options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a request to its handler asynchronously
    /// </summary>
    /// <param name="request">The request to dispatch.</param>
    /// <param name="options">The options for request processing, including context and progress reporting.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task representing the result of the request, returning a <see cref="Result{TValue}"/>.</returns>
    Task<Result> SendDynamicAsync(
        IRequest request,
        SendOptions options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a request to its handler asynchronously
    /// </summary>
    /// <param name="request">The request to dispatch.</param>
    /// <param name="options">The options for request processing, including context and progress reporting.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task representing the result of the request, returning a <see cref="Result{TValue}"/>.</returns>
    Task<Result<TValue>> SendDynamicAsync<TValue>(
        IRequest<TValue> request,
        SendOptions options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns information about registered request handlers and behaviors.
    /// </summary>
    /// <returns>An object containing handler mappings and behavior types.</returns>
    RegistrationInformation GetRegistrationInformation();
}

/// <summary>
/// Dispatches requests to their handlers through a pipeline of behaviors.
/// </summary>
/// <remarks>
/// This class implements <see cref="IRequester"/> to dispatch requests to their handlers, processing them through
/// a pipeline of behaviors resolved via <see cref="IRequestBehaviorsProvider"/>. It uses structured logging
/// with <see cref="TypedLogger"/> to log processing details and supports registration information retrieval
/// through <see cref="GetRegistrationInformation"/>.
/// </remarks>
/// <example>
/// <code>
/// var requester = serviceProvider.GetRequiredService<IRequester>();
/// var request = new SampleRequest();
/// var options = new SendOptions { Context = new RequestContext { Properties = { ["UserId"] = "user123" } } };
/// var result = await requester.SendAsync<SampleRequest, string>(request, options);
/// var info = requester.GetRegistrationInformation();
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="Requester"/> class.
/// </remarks>
/// <param name="serviceProvider">The service provider for resolving services.</param>
/// <param name="logger">The logger for structured logging.</param>
/// <param name="handlerProvider">The provider for resolving request handlers.</param>
/// <param name="behaviorsProvider">The provider for resolving pipeline behaviors.</param>
/// <param name="handlerCache">The cache of handler types, mapping request handler interfaces to concrete types.</param>
/// <param name="pipelineBehaviorTypes">The list of registered behavior types.</param>
public partial class Requester(
    IServiceProvider serviceProvider,
    ILogger<Requester> logger,
    IRequestHandlerProvider handlerProvider,
    IRequestBehaviorsProvider behaviorsProvider,
    IHandlerCache handlerCache,
    IReadOnlyList<Type> pipelineBehaviorTypes) : IRequester
{
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly ILogger<Requester> logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IRequestHandlerProvider handlerProvider = handlerProvider ?? throw new ArgumentNullException(nameof(handlerProvider));
    private readonly IRequestBehaviorsProvider behaviorsProvider = behaviorsProvider ?? throw new ArgumentNullException(nameof(behaviorsProvider));
    private readonly IHandlerCache handlerCache = handlerCache ?? throw new ArgumentNullException(nameof(handlerCache));
    private readonly IReadOnlyList<Type> pipelineBehaviorTypes = pipelineBehaviorTypes ?? throw new ArgumentNullException(nameof(pipelineBehaviorTypes));

    private const string RequestLogKey = "REQ";
    private const string RequestIdLogKey = "RequestId";
    private const string RequestTypeLogKey = "RequestType";
    private static readonly ConcurrentDictionary<Type, string> TypeNameCache = [];
    private static readonly SendOptions DefaultOptions = new();

    /// <summary>
    /// Dispatches a request to its handler asynchronously.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TValue">The type of the response value.</typeparam>
    /// <param name="request">The request to dispatch.</param>
    /// <param name="options">The options for request processing, including context and progress reporting.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task representing the result of the request, returning a <see cref="Result{TValue}"/>.</returns>
    public async Task<Result<TValue>> SendAsync<TRequest, TValue>(
        TRequest request,
        SendOptions options = null,
        CancellationToken cancellationToken = default)
        where TRequest : class, IRequest<TValue>
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestTypeName = TypeNameCache.GetOrAdd(typeof(TRequest), static t => t.Name);
        var requestIdString = request.RequestId.ToString("N");

        using (this.logger.BeginScope(new Dictionary<string, object>
        {
            [RequestIdLogKey] = requestIdString,
            [RequestTypeLogKey] = requestTypeName
        }))
        {
            TypedLogger.LogProcessing(this.logger, RequestLogKey, requestTypeName, requestIdString);
            cancellationToken.ThrowIfCancellationRequested();
            options ??= DefaultOptions;
            var startTicks = Environment.TickCount64; // Zero-alloc timing
            var handler = this.handlerProvider.GetHandler<TRequest, TValue>(this.serviceProvider);
            var behaviors = this.behaviorsProvider.GetBehaviors<TRequest, TValue>(this.serviceProvider);

            try
            {
                Func<Task<IResult<TValue>>> next = async () =>
                    await handler.HandleAsync(request, options, cancellationToken);

                for (var i = behaviors.Count - 1; i >= 0; i--)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var behavior = behaviors[i];
                    var behaviorTypeName = TypeNameCache.GetOrAdd(behavior.GetType(), static t => t.Name);
                    //TypedLogger.LogProcessing(this.logger, behaviorTypeName, requestTypeName, requestIdString);
                    var behaviorStartTicks = Environment.TickCount64;
                    var currentNext = next;
                    next = async () =>
                    {
                        var result = await behavior.HandleAsync(request, options, handler.GetType(), currentNext, cancellationToken);
                        //TypedLogger.LogProcessed(this.logger, behaviorTypeName, requestTypeName, requestIdString, Environment.TickCount64 - behaviorStartTicks);
                        return result;
                    };
                }

                cancellationToken.ThrowIfCancellationRequested();
                var handlerResult = await next().ConfigureAwait(false);

                if (handlerResult.IsSuccess)
                {
                    TypedLogger.LogSuccess(this.logger, RequestLogKey, requestTypeName, requestIdString, Environment.TickCount64 - startTicks);

                    return (Result<TValue>)handlerResult;
                }
                else
                {
                    TypedLogger.LogFailed(this.logger, RequestLogKey, requestTypeName, requestIdString, Environment.TickCount64 - startTicks);
                    this.logger.LogError("{LogKey} request failed with errors: {Errors}", RequestLogKey, string.Join("; ", handlerResult.Errors.Select(e => e.Message)));
                }

                return (Result<TValue>)handlerResult;
            }
            catch (Exception ex) when (options.HandleExceptionsAsResultError)
            {
                TypedLogger.LogError(this.logger, RequestLogKey, ex, requestTypeName, requestIdString);

                return Result<TValue>.Failure().WithError(new ExceptionError(ex));
            }
        }
    }

    public Task<Result<TValue>> SendAsync<TValue>(
        IRequest<TValue> request,
        SendOptions options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();
        var requestType = request.GetType();
        var requestInterface = typeof(IRequest<TValue>);
        if (!requestInterface.IsAssignableFrom(requestType))
        {
            throw new ArgumentException($"Request of type '{requestType}' does not implement '{requestInterface}'.", nameof(request));
        }

        // Find the generic SendAsync<TRequest, TValue> method
        var method = typeof(Requester)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == nameof(this.SendAsync) && m.IsGenericMethod && m.GetGenericArguments().Length == 2) ?? throw new InvalidOperationException($"Generic method '{nameof(this.SendAsync)}' not found on '{nameof(Requester)}'.");

        // Create the generic method with the runtime requestType and TValue
        var genericMethod = method.MakeGenericMethod(requestType, typeof(TValue));
        return (Task<Result<TValue>>)genericMethod.Invoke(this, [request, options, cancellationToken]);
    }

    /// <summary>
    /// Dispatches a request to its handler asynchronously.
    /// </summary>
    /// <param name="request">The request to dispatch.</param>
    /// <param name="options">The options for request processing, including context and progress reporting.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task representing the result of the request, returning a <see cref="Result{TValue}"/>.</returns>
    public Task<Result> SendDynamicAsync(
        IRequest request,
        SendOptions options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Find the generic SendAsync<TRequest, TValue> method
        var method = typeof(IRequester).GetMethod(nameof(IRequester.SendAsync), [request.GetType(), typeof(SendOptions), typeof(CancellationToken)]);
        if (method?.IsGenericMethod != true) // fallback: search by name and generic args
        {
            method = typeof(IRequester)
                .GetMethods()
                .FirstOrDefault(m =>
                    m.Name == nameof(IRequester.SendAsync) &&
                    m.IsGenericMethod &&
                    m.GetGenericArguments().Length == 2);
        }

        if (method == null)
        {
            throw new InvalidOperationException($"Generic method '{nameof(IRequester.SendAsync)}' not found.");
        }

        var genericMethod = method.MakeGenericMethod(request.GetType());

        return (Task<Result>)genericMethod.Invoke(this, [request, options, cancellationToken])!;
    }

    /// <summary>
    /// Dispatches a request to its handler asynchronously.
    /// </summary>
    /// <typeparam name="TValue">The type of the response value.</typeparam>
    /// <param name="request">The request to dispatch.</param>
    /// <param name="options">The options for request processing, including context and progress reporting.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task representing the result of the request, returning a <see cref="Result{TValue}"/>.</returns>
    public Task<Result<TValue>> SendDynamicAsync<TValue>(
        IRequest<TValue> request,
        SendOptions options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Find the generic SendAsync<TRequest, TValue> method
        var method = typeof(IRequester).GetMethod(nameof(IRequester.SendAsync), [request.GetType(), typeof(SendOptions), typeof(CancellationToken)]);
        if (method?.IsGenericMethod != true) // fallback: search by name and generic args
        {
            method = typeof(IRequester)
                .GetMethods()
                .FirstOrDefault(m =>
                    m.Name == nameof(IRequester.SendAsync) &&
                    m.IsGenericMethod &&
                    m.GetGenericArguments().Length == 2);
        }

        if (method == null)
        {
            throw new InvalidOperationException($"Generic method '{nameof(IRequester.SendAsync)}' not found.");
        }

        var genericMethod = method.MakeGenericMethod(request.GetType(), typeof(TValue)); // Make generic with runtime request type + TValue

        return (Task<Result<TValue>>)genericMethod.Invoke(this, [request, options, cancellationToken])!;
    }

    /// <summary>
    /// Returns information about registered request handlers and behaviors, logging the details.
    /// </summary>
    /// <returns>An object containing handler mappings and behavior types.</returns>
    public RegistrationInformation GetRegistrationInformation()
    {
        var handlerMappings = this.handlerCache
            .ToDictionary(
                kvp => kvp.Key.GetGenericArguments()[0].PrettyName(), // Request type
                kvp => new List<string> { kvp.Value.PrettyName() }.AsReadOnly() as IReadOnlyList<string>);

        var behaviorTypes = this.pipelineBehaviorTypes
            .Select(t => t.PrettyName())
            .ToList().AsReadOnly();

        var information = new RegistrationInformation(handlerMappings, behaviorTypes);

        this.logger.LogDebug("Registered Request Handlers: {HandlerMappings}", string.Join("; ", handlerMappings.Select(kvp => $"{kvp.Key}: [{string.Join(", ", kvp.Value)}]")));
        this.logger.LogDebug("Registered Request Behaviors: {BehaviorTypes}", string.Join(", ", behaviorTypes));

        return information;
    }

    /// <summary>
    /// Provides structured logging messages for the <see cref="Requester"/> class.
    /// </summary>
    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "{LogKey} request processing (type={RequestType}, id={RequestId})")]
        public static partial void LogProcessing(ILogger logger, string logKey, string requestType, string requestId);

        [LoggerMessage(1, LogLevel.Information, "{LogKey} request success (type={RequestType}, id={RequestId}) -> took {TimeElapsed} ms")]
        public static partial void LogSuccess(ILogger logger, string logKey, string requestType, string requestId, long timeElapsed);

        [LoggerMessage(2, LogLevel.Error, "{LogKey} request failed (type={RequestType}, id={RequestId}) -> took {TimeElapsed} ms")]
        public static partial void LogFailed(ILogger logger, string logKey, string requestType, string requestId, long timeElapsed);

        [LoggerMessage(3, LogLevel.Error, "{LogKey} request processing failed for {RequestType} ({RequestId})")]
        public static partial void LogError(ILogger logger, string logKey, Exception ex, string requestType, string requestId);
    }
}

/// <summary>
/// Represents information about registered handlers and behaviors for Requester or Notifier.
/// </summary>
/// <remarks>
/// This class provides insights into the registered components of the Requester or Notifier system, including
/// a mapping of request/notification types to their handler types and a list of registered behavior types.
/// It is returned by <see cref="IRequester.GetRegistrationInformation"/> and <see cref="INotifier.GetRegistrationInformation"/>.
/// </remarks>
/// <example>
/// <code>
/// var information = requester.GetRegistrationInformation();
/// foreach (var mapping in information.HandlerMappings)
/// {
///     Console.WriteLine($"Request: {mapping.Key}, Handlers: [{string.Join(", ", mapping.Value)}]");
/// }
/// Console.WriteLine($"Behaviors: [{string.Join(", ", information.BehaviorTypes)}]");
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="RegistrationInformation"/> class.
/// </remarks>
/// <param name="handlerMappings">The mapping of request or notification types to their handler types.</param>
/// <param name="behaviorTypes">The list of registered behavior types.</param>
public class RegistrationInformation(
    IReadOnlyDictionary<string, IReadOnlyList<string>> handlerMappings,
    IReadOnlyList<string> behaviorTypes)
{
    /// <summary>
    /// Gets the mapping of request or notification types to their handler types.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> HandlerMappings { get; } = handlerMappings ?? throw new ArgumentNullException(nameof(handlerMappings));

    /// <summary>
    /// Gets the list of registered behavior types.
    /// </summary>
    public IReadOnlyList<string> BehaviorTypes { get; } = behaviorTypes ?? throw new ArgumentNullException(nameof(behaviorTypes));
}

//public static class RequesterExtensions
//{
//    /// <summary>
//    /// Dispatches a request to its handler asynchronously.
//    /// </summary>
//    /// <typeparam name="TValue">The type of the response value.</typeparam>
//    /// <param name="request">The request to dispatch.</param>
//    /// <param name="options">The options for request processing, including context and progress reporting.</param>
//    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
//    /// <returns>A task representing the result of the request, returning a <see cref="Result{TValue}"/>.</returns>
//    public static Task<Result<TValue>> SendDynamicAsync<TValue>(
//        this IRequester requester,
//        IRequest<TValue> request,
//        SendOptions options = null,
//        CancellationToken cancellationToken = default)
//    {
//        ArgumentNullException.ThrowIfNull(request);

//        // Find the generic SendAsync<TRequest, TValue> method
//        var method = typeof(IRequester).GetMethod(nameof(IRequester.SendAsync), [request.GetType(), typeof(SendOptions), typeof(CancellationToken)]);

//        if (method?.IsGenericMethod != true) // fallback: search by name and generic args
//        {
//            method = typeof(IRequester)
//                .GetMethods()
//                .FirstOrDefault(m =>
//                    m.Name == nameof(IRequester.SendAsync) &&
//                    m.IsGenericMethod &&
//                    m.GetGenericArguments().Length == 2);
//        }

//        if (method == null)
//        {
//            throw new InvalidOperationException($"Generic method '{nameof(IRequester.SendAsync)}' not found.");
//        }

//        var genericMethod = method.MakeGenericMethod(request.GetType(), typeof(TValue)); // Make generic with runtime request type + TValue

//        return (Task<Result<TValue>>)genericMethod.Invoke(requester, new object[] { request, options, cancellationToken })!;
//    }
//}