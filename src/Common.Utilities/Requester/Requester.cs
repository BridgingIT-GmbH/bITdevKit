// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Requester;

using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using BridgingIT.DevKit.Common;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Represents an error that occurs during request processing.
/// </summary>
/// <remarks>
/// This exception is thrown when an error occurs in the Requester system, such as when no handler is found for a request.
/// It supports both a message-only constructor and a constructor with an inner exception for detailed error reporting.
/// </remarks>
/// <example>
/// <code>
/// throw new RequesterException("No handler found for request type CustomerCreateCommand");
/// throw new RequesterException("Request processing failed", innerException);
/// </code>
/// </example>
public class RequesterException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequesterException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public RequesterException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequesterException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public RequesterException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

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
/// Represents a progress report for request or notification processing.
/// </summary>
/// <remarks>
/// This class is used to report progress through <see cref="IProgress{ProgressReport}"/> in <see cref="SendOptions"/>
/// or <see cref="PublishOptions"/>. It includes the operation name, progress messages, percentage complete, and
/// completion status, allowing handlers and behaviors to provide feedback during processing.
/// </remarks>
/// <example>
/// <code>
/// options.Progress?.Report(new ProgressReport(
///     "SampleRequest",
///     new[] { "Processing request" },
///     50.0));
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="ProgressReport"/> class.
/// </remarks>
/// <param name="operation">The name of the operation being reported.</param>
/// <param name="messages">The messages describing the current progress.</param>
/// <param name="percentageComplete">The percentage of completion (0.0 to 100.0).</param>
/// <param name="isCompleted">Indicates whether the operation is fully completed.</param>
public class ProgressReport(string operation, IEnumerable<string> messages, double percentageComplete, bool isCompleted = false)
{
    /// <summary>
    /// Gets the name of the operation being reported.
    /// </summary>
    public string Operation { get; } = operation;

    /// <summary>
    /// Gets the messages describing the current progress.
    /// </summary>
    public string[] Messages { get; } = messages?.ToArray() ?? [];

    /// <summary>
    /// Gets the percentage of completion, ranging from 0.0 to 100.0.
    /// </summary>
    public double PercentageComplete { get; } = percentageComplete;

    /// <summary>
    /// Gets a value indicating whether the operation is fully completed.
    /// </summary>
    public bool IsCompleted { get; } = isCompleted;
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
        if (!this.handlerCache.TryGetValue(typeof(IRequestHandler<TRequest, TValue>), out var handlerType))
        {
            throw new RequesterException($"No handler found for request type {typeof(TRequest).Name}");
        }

        return (IRequestHandler<TRequest, TValue>)serviceProvider.GetRequiredService(handlerType);
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

    /// <summary>
    /// Resolves the pipeline behaviors for a specific request type.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TValue">The type of the response value.</typeparam>
    /// <param name="serviceProvider">The service provider for resolving services.</param>
    /// <returns>A read-only list of pipeline behaviors for the request.</returns>
    /// <summary>
    /// Resolves the pipeline behaviors for a specific request type.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TValue">The type of the response value.</typeparam>
    /// <param name="serviceProvider">The service provider for resolving services.</param>
    /// <returns>A read-only list of pipeline behaviors for the request.</returns>
    public IReadOnlyList<IPipelineBehavior<TRequest, IResult<TValue>>> GetBehaviors<TRequest, TValue>(IServiceProvider serviceProvider)
        where TRequest : class, IRequest<TValue>
    {
        if (serviceProvider == null || this.pipelineBehaviorTypes.Count == 0)
        {
            return Array.Empty<IPipelineBehavior<TRequest, IResult<TValue>>>().AsReadOnly();
        }

        var behaviorType = typeof(IPipelineBehavior<TRequest, IResult<TValue>>);
        var allBehaviors = serviceProvider.GetServices(behaviorType)
            .Cast<IPipelineBehavior<TRequest, IResult<TValue>>>()
            .ToList();

        // If no behaviors are registered but pipelineBehaviorTypes expects some, throw an exception
        if (allBehaviors.Count == 0 && this.pipelineBehaviorTypes.Count > 0)
        {
            throw new InvalidOperationException($"No service for type '{behaviorType}' has been registered.");
        }

        // Order behaviors according to the pipelineBehaviorTypes list
        var orderedBehaviors = new List<IPipelineBehavior<TRequest, IResult<TValue>>>();
        var remainingBehaviors = new List<IPipelineBehavior<TRequest, IResult<TValue>>>(allBehaviors);
        foreach (var type in this.pipelineBehaviorTypes)
        {
            var matchingBehavior = remainingBehaviors.FirstOrDefault(b => b.GetType().GetGenericTypeDefinition() == type);
            if (matchingBehavior != null)
            {
                orderedBehaviors.Add(matchingBehavior);
                remainingBehaviors.Remove(matchingBehavior);
            }
            else
            {
                // If a behavior type is in the list but not registered, throw an exception
                throw new InvalidOperationException($"Behavior type '{type}' is not registered in the DI container for '{behaviorType}'.");
            }
        }

        // Validate that all registered behaviors implement the expected interface
        foreach (var behavior in allBehaviors)
        {
            if (!this.pipelineBehaviorTypes.Any(type => type == behavior.GetType().GetGenericTypeDefinition()))
            {
                throw new InvalidOperationException($"Behavior type '{behavior.GetType()}' does not match any registered behavior types for '{behaviorType}'.");
            }
        }

        return orderedBehaviors.AsReadOnly();
    }
}

/// <summary>
/// Defines a pipeline behavior for processing requests or notifications.
/// </summary>
/// <typeparam name="TRequest">The type of the request or notification.</typeparam>
/// <typeparam name="TResponse">The type of the response, typically <see cref="Result{TValue}"/> for requests or <see cref="Result{Unit}"/> for notifications.</typeparam>
/// <remarks>
/// This interface is implemented by behavior classes to provide cross-cutting concerns (e.g., validation, retry, timeout)
/// in the request or notification processing pipeline. Behaviors can access options (<see cref="SendOptions"/> for requests,
/// <see cref="PublishOptions"/> for notifications) and call the next behavior or handler in the pipeline.
/// </remarks>
/// <example>
/// <code>
/// public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
///     where TRequest : class
///     where TResponse : Result
/// {
///     public async Task<TResponse> HandleAsync(TRequest request, object options, Func<Task<TResponse>> next, CancellationToken cancellationToken)
///     {
///         // Validation logic
///         return await next();
///     }
/// }
/// </code>
/// </example>
public interface IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : IResult
{
    /// <summary>
    /// Processes a request or notification, calling the next behavior or handler in the pipeline.
    /// </summary>
    /// <param name="request">The request or notification to process.</param>
    /// <param name="options">The options for request (<see cref="SendOptions"/>) or notification (<see cref="PublishOptions"/>) processing.</param>
    /// <param name="next">The delegate to call the next behavior or handler in the pipeline.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task representing the result of the operation, returning a <see cref="TResponse"/>.</returns>
    Task<TResponse> HandleAsync(TRequest request, object options, Func<Task<TResponse>> next, CancellationToken cancellationToken = default);
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

        TypedLogger.LogProcessing(this.logger, "REQ", typeof(TRequest).Name, request.RequestId.ToString("N"));
        var watch = ValueStopwatch.StartNew();

        try
        {
            var handler = this.handlerProvider.GetHandler<TRequest, TValue>(this.serviceProvider);
            Func<Task<IResult<TValue>>> next = async () =>
                await handler.HandleAsync(request, options, cancellationToken);

            var behaviors = this.behaviorsProvider.GetBehaviors<TRequest, TValue>(this.serviceProvider);

            foreach (var behavior in behaviors.Reverse())
            {
                var behaviorType = behavior.GetType().Name;
                TypedLogger.LogProcessing(this.logger, behaviorType, typeof(TRequest).Name, request.RequestId.ToString("N"));
                var behaviorWatch = ValueStopwatch.StartNew();
                var currentNext = next;
                next = async () =>
                {
                    var result = await behavior.HandleAsync(request, options, currentNext, cancellationToken);
                    TypedLogger.LogProcessed(this.logger, behaviorType, typeof(TRequest).Name, request.RequestId.ToString("N"), behaviorWatch.GetElapsedMilliseconds());
                    return result;
                };
            }

            var result = await next();
            TypedLogger.LogProcessed(this.logger, "REQ", typeof(TRequest).Name, request.RequestId.ToString("N"), watch.GetElapsedMilliseconds());
            return (Result<TValue>)result;
        }
        catch (Exception ex)
        {
            TypedLogger.LogError(this.logger, ex, typeof(TRequest).Name, request.RequestId.ToString("N"));
            return Result<TValue>.Failure().WithError(new ExceptionError(ex));
        }
    }

    /// <summary>
    /// Returns information about registered request handlers and behaviors, logging the details.
    /// </summary>
    /// <returns>An object containing handler mappings and behavior types.</returns>
    public RegistrationInformation GetRegistrationInformation()
    {
        var handlerMappings = this.handlerCache
            .ToDictionary(
                kvp => kvp.Key.GetGenericArguments()[0].Name, // Request type
                kvp => new List<string> { kvp.Value.Name }.AsReadOnly() as IReadOnlyList<string>);

        var behaviorTypes = this.pipelineBehaviorTypes
            .Select(t => t.Name)
            .ToList()
            .AsReadOnly();

        var information = new RegistrationInformation(handlerMappings, behaviorTypes);

        this.logger.LogDebug("Registered Request Handlers: {HandlerMappings}",
            string.Join("; ", handlerMappings.Select(kvp => $"{kvp.Key}: [{string.Join(", ", kvp.Value)}]")));
        this.logger.LogDebug("Registered Request Behaviors: {BehaviorTypes}",
            string.Join(", ", behaviorTypes));

        return information;
    }

    /// <summary>
    /// Provides structured logging messages for the <see cref="Requester"/> class.
    /// </summary>
    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "{LogKey} processing (type={RequestType}, id={RequestId})")]
        public static partial void LogProcessing(ILogger logger, string logKey, string requestType, string requestId);

        [LoggerMessage(1, LogLevel.Information, "{LogKey} processed (type={RequestType}, id={RequestId}) -> took {TimeElapsed:0.0000} ms")]
        public static partial void LogProcessed(ILogger logger, string logKey, string requestType, string requestId, long timeElapsed);

        [LoggerMessage(2, LogLevel.Error, "Request processing failed for {RequestType} ({RequestId})")]
        public static partial void LogError(ILogger logger, Exception ex, string requestType, string requestId);
    }
}

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
public class RequesterBuilder(IServiceCollection services)
{
    private readonly IServiceCollection services = services ?? throw new ArgumentNullException(nameof(services));
    private readonly List<Type> pipelineBehaviorTypes = [];
    private readonly List<Type> validatorTypes = [];
    private readonly IHandlerCache handlerCache = new HandlerCache();
    private readonly ConcurrentDictionary<Type, PolicyConfig> policyCache = [];

    /// <summary>
    /// Adds handlers, validators, and providers by scanning all loaded assemblies, excluding those matching blacklist patterns.
    /// </summary>
    /// <param name="blacklistPatterns">Optional regex patterns to exclude assemblies.</param>
    /// <returns>The <see cref="RequesterBuilder"/> for fluent chaining.</returns>
    public RequesterBuilder AddHandlers(IEnumerable<string> blacklistPatterns = null)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.GetName().Name.MatchAny(blacklistPatterns))
            .ToList();
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
                        Timeout = type.GetCustomAttribute<HandlerTimeoutAttribute>()
                    });

                    // Only register concrete (non-abstract) types in the DI container
                    if (!type.IsAbstract)
                    {
                        this.services.AddScoped(type);
                    }

                    var validatorType = requestType.GetNestedType("Validator");
                    if (validatorType?.GetInterfaces().Any(i => i == typeof(IValidator<>).MakeGenericType(requestType)) == true)
                    {
                        this.validatorTypes.Add(validatorType);
                    }
                }
            }
        }

        this.services.AddSingleton<IHandlerCache>(this.handlerCache);
        this.services.AddSingleton(this.policyCache);
        this.services.AddSingleton<IRequestHandlerProvider, RequestHandlerProvider>();
        this.services.AddSingleton<IRequestBehaviorsProvider>(sp => new RequestBehaviorsProvider(this.pipelineBehaviorTypes));
        this.services.AddScoped<IRequester>(sp => new Requester(
            sp,
            sp.GetRequiredService<ILogger<Requester>>(),
            sp.GetRequiredService<IRequestHandlerProvider>(),
            sp.GetRequiredService<IRequestBehaviorsProvider>(),
            sp.GetRequiredService<IHandlerCache>(),
            this.pipelineBehaviorTypes));

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

/// <summary>
/// Represents the policy configuration for a handler, including retry and timeout settings.
/// </summary>
public class PolicyConfig
{
    /// <summary>
    /// Gets or sets the retry policy attribute for the handler.
    /// </summary>
    public HandlerRetryAttribute Retry { get; set; }

    /// <summary>
    /// Gets or sets the timeout policy attribute for the handler.
    /// </summary>
    public HandlerTimeoutAttribute Timeout { get; set; }
}

/// <summary>
/// Specifies a retry policy for a handler.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="HandlerRetryAttribute"/> class.
/// </remarks>
/// <param name="count">The number of retry attempts.</param>
/// <param name="delay">The delay between retries in milliseconds.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class HandlerRetryAttribute(int count, int delay) : Attribute
{
    /// <summary>
    /// Gets the number of retry attempts.
    /// </summary>
    public int Count { get; } = count;

    /// <summary>
    /// Gets the delay between retries in milliseconds.
    /// </summary>
    public int Delay { get; } = delay;
}

/// <summary>
/// Specifies a timeout policy for a handler.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="HandlerTimeoutAttribute"/> class.
/// </remarks>
/// <param name="timeout">The timeout duration in milliseconds.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class HandlerTimeoutAttribute(int timeout) : Attribute
{
    /// <summary>
    /// Gets the timeout duration in milliseconds.
    /// </summary>
    public int Timeout { get; } = timeout;
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

/// <summary>
/// Defines a cache for mapping request handler interfaces to their concrete types.
/// </summary>
public interface IHandlerCache : IReadOnlyDictionary<Type, Type>
{
    /// <summary>
    /// Attempts to add a handler type mapping to the cache.
    /// </summary>
    /// <param name="key">The request handler interface type.</param>
    /// <param name="value">The concrete handler type.</param>
    /// <returns>True if the mapping was added; otherwise, false.</returns>
    bool TryAdd(Type key, Type value);
}

/// <summary>
/// Implements <see cref="IHandlerCache"/> using a <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// </summary>
public class HandlerCache : IHandlerCache
{
    private readonly ConcurrentDictionary<Type, Type> cache;

    public HandlerCache()
    {
        this.cache = [];
    }

    public Type this[Type key] => this.cache[key];

    public IEnumerable<Type> Keys => this.cache.Keys;

    public IEnumerable<Type> Values => this.cache.Values;

    public int Count => this.cache.Count;

    public bool ContainsKey(Type key) => this.cache.ContainsKey(key);

    public bool TryGetValue(Type key, out Type value) => this.cache.TryGetValue(key, out value);

    public bool TryAdd(Type key, Type value) => this.cache.TryAdd(key, value);

    public IEnumerator<KeyValuePair<Type, Type>> GetEnumerator() => this.cache.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}