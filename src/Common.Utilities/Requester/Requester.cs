// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Requester;

using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using BridgingIT.DevKit.Common;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Contrib.Simmy;
using Polly.Contrib.Simmy.Outcomes;
using Polly.Timeout;

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
        var allBehaviorsArray = serviceProvider.GetServices(behaviorType)
            .Cast<IPipelineBehavior<TRequest, IResult<TValue>>>().ToArray();

        // If no behaviors are registered but pipelineBehaviorTypes expects some, throw an exception
        if (allBehaviorsArray.Length == 0 && this.pipelineBehaviorTypes.Count > 0)
        {
            throw new InvalidOperationException($"No service for type '{behaviorType}' has been registered.");
        }

        // Order behaviors according to the pipelineBehaviorTypes list
        var orderedBehaviors = new IPipelineBehavior<TRequest, IResult<TValue>>[this.pipelineBehaviorTypes.Count];
        var orderedCount = 0;
        for (var i = 0; i < this.pipelineBehaviorTypes.Count; i++)
        {
            var type = this.pipelineBehaviorTypes[i];
            IPipelineBehavior<TRequest, IResult<TValue>> matchingBehavior = null;
            for (var j = 0; j < allBehaviorsArray.Length; j++)
            {
                if (allBehaviorsArray[j].GetType().GetGenericTypeDefinition() == type)
                {
                    matchingBehavior = allBehaviorsArray[j];
                    break;
                }
            }

            if (matchingBehavior != null)
            {
                orderedBehaviors[orderedCount++] = matchingBehavior;
            }
            else
            {
                // If a behavior type is in the list but not registered, throw an exception
                throw new InvalidOperationException($"Behavior type '{type}' is not registered in the DI container for '{behaviorType}'.");
            }
        }

        // Validate that all registered behaviors implement the expected interface
        var matchedCount = orderedCount;
        if (allBehaviorsArray.Length != matchedCount)
        {
            throw new InvalidOperationException("Mismatch in registered behaviors for '{behaviorType}'.");
        }

        return orderedBehaviors.AsSpan(0, orderedCount).ToArray();
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
    Task<TResponse> HandleAsync(TRequest request, object options, Type handlerType, Func<Task<TResponse>> next, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indicates whether the behavior should be applied per handler.
    /// </summary>
    /// <returns><c>true</c> if the behavior is handler-specific (e.g., retry, timeout); <c>false</c> if it should run once per message (e.g., validation).</returns>
    bool IsHandlerSpecific();
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

    private static readonly ConcurrentDictionary<Type, string> TypeNameCache = [];

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

        TypedLogger.LogProcessing(this.logger, "REQ", requestTypeName, requestIdString);
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new SendOptions();
        var watch = ValueStopwatch.StartNew();
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
                TypedLogger.LogProcessing(this.logger, behaviorTypeName, requestTypeName, requestIdString);
                var behaviorWatch = ValueStopwatch.StartNew();
                var currentNext = next;
                next = async () =>
                {
                    var result = await behavior.HandleAsync(request, options, handler.GetType(), currentNext, cancellationToken);
                    TypedLogger.LogProcessed(this.logger, behaviorTypeName, requestTypeName, requestIdString, behaviorWatch.GetElapsedMilliseconds());
                    return result;
                };
            }

            cancellationToken.ThrowIfCancellationRequested();
            var result = await next().ConfigureAwait(false);
            TypedLogger.LogProcessed(this.logger, "REQ", requestTypeName, requestIdString, watch.GetElapsedMilliseconds());
            return (Result<TValue>)result;
        }
        catch (Exception ex) when (options.HandleExceptionsAsResultError)
        {
            TypedLogger.LogError(this.logger, ex, requestTypeName, requestIdString);
            return Result<TValue>.Failure().WithError(new ExceptionError(ex));
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
            .FirstOrDefault(m => m.Name == nameof(this.SendAsync) && m.IsGenericMethod && m.GetGenericArguments().Length == 2);

        if (method == null)
        {
            throw new InvalidOperationException($"Generic method '{nameof(this.SendAsync)}' not found on '{nameof(Requester)}'.");
        }

        // Create the generic method with the runtime requestType and TValue
        var genericMethod = method.MakeGenericMethod(requestType, typeof(TValue));
        return (Task<Result<TValue>>)genericMethod.Invoke(this, new object[] { request, options, cancellationToken });
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

        [LoggerMessage(1, LogLevel.Information, "{LogKey} processed (type={RequestType}, id={RequestId}) -> took {TimeElapsed} ms")]
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
public class RequesterBuilder
{
    private readonly IServiceCollection services;
    private readonly List<Type> pipelineBehaviorTypes = [];
    private readonly List<Type> validatorTypes = [];
    private readonly IHandlerCache handlerCache = new HandlerCache();
    private readonly ConcurrentDictionary<Type, PolicyConfig> policyCache = [];

    public RequesterBuilder(IServiceCollection services)
    {
        this.services = services ?? throw new ArgumentNullException(nameof(services));

        // Register the core services needed for Requester to function
        this.services.AddSingleton(this.handlerCache);
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
        if (genericHandlerType == null || !genericHandlerType.IsGenericTypeDefinition)
        {
            throw new ArgumentException("Generic handler type must be an open generic type definition.", nameof(genericHandlerType));
        }

        if (genericRequestType == null || !genericRequestType.IsGenericTypeDefinition)
        {
            throw new ArgumentException("Generic request type must be an open generic type definition.", nameof(genericRequestType));
        }

        if (typeArguments == null || !typeArguments.Any())
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
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>));
        if (handlerInterface == null)
        {
            throw new ArgumentException($"Generic handler type {genericHandlerType.Name} does not implement IRequestHandler<,>.", nameof(genericHandlerType));
        }

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

                if (handlerInterfaces.Any())
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

            if (!typeArguments.Any())
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

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Requester service to the specified <see cref="IServiceCollection"/> and returns a <see cref="RequesterBuilder"/> for configuration.
    /// </summary>
    /// <remarks>
    /// This method registers the necessary services for the Requester system, including the <see cref="IRequester"/> implementation
    /// and its dependencies. It returns a <see cref="RequesterBuilder"/> that can be used to configure handlers and behaviors
    /// using a fluent API. The Requester system enables dispatching requests to their corresponding handlers through a pipeline
    /// of behaviors, supporting features like validation, retry, timeout, and chaos injection.
    ///
    /// To use the Requester system, you must:
    /// 1. Call <see cref="AddRequester"/> to register the core services.
    /// 2. Use <see cref="RequesterBuilder.AddHandlers"/> to scan for and register request handlers.
    /// 3. Optionally, use <see cref="RequesterBuilder.WithBehavior{TBehavior}"/> to add pipeline behaviors.
    /// 4. Build the service provider to resolve the <see cref="IRequester"/> service for dispatching requests.
    ///
    /// The <see cref="IRequester"/> service is registered with a scoped lifetime, meaning a new instance is created for each
    /// scope (e.g., per HTTP request in ASP.NET Core). Ensure that any dependencies (e.g., logging) are also registered in the
    /// service collection before calling this method.
    /// </remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the Requester service to.</param>
    /// <returns>A <see cref="RequesterBuilder"/> for fluent configuration.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <example>
    /// <code>
    /// // Basic usage with default configuration
    /// var services = new ServiceCollection();
    /// services.AddLogging(); // Required for IRequester logging
    /// services.AddRequester()
    ///     .AddHandlers(new[] { "^System\\..*" }); // Exclude System assemblies
    /// var provider = services.BuildServiceProvider();
    /// var requester = provider.GetRequiredService<IRequester>();
    /// var result = await requester.SendAsync(new MyRequest());
    ///
    /// // Usage with pipeline behaviors
    /// var services = new ServiceCollection();
    /// services.AddLogging();
    /// services.AddRequester()
    ///     .AddHandlers(new[] { "^System\\..*" })
    ///     .WithBehavior<ValidationBehavior<,>>() // Add validation behavior
    ///     .WithBehavior<RetryBehavior<,>>();    // Add retry behavior
    /// var provider = services.BuildServiceProvider();
    /// var requester = provider.GetRequiredService<IRequester>();
    /// var result = await requester.SendAsync(new MyRequest());
    /// </code>
    /// </example>
    /// <seealso cref="RequesterBuilder"/>
    /// <seealso cref="IRequester"/>
    public static RequesterBuilder AddRequester(this IServiceCollection services)
    {
        return services == null ? throw new ArgumentNullException(nameof(services)) : new RequesterBuilder(services);
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

    /// <summary>
    /// Gets or sets the chaos policy attribute for the handler.
    /// </summary>
    public HandlerChaosAttribute Chaos { get; set; }

    /// <summary>
    /// Gets or sets the circuit breaker policy attribute for the handler.
    /// </summary>
    public HandlerCircuitBreakerAttribute CircuitBreaker { get; set; }

    /// <summary>
    /// Gets or sets the cache invalidation policy attribute for the handler.
    /// </summary>
    public HandlerCacheInvalidateAttribute CacheInvalidate { get; set; }

    ///// <summary>
    ///// Gets or sets the transaction policy attribute for the handler.
    ///// </summary>
    //public HandlerDatabaseTransactionAttribute DatabaseTransaction { get; set; }
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
    public int Duration { get; } = timeout;
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class HandlerChaosAttribute : Attribute
{
    public HandlerChaosAttribute(double injectionRate, bool enabled = true)
    {
        if (injectionRate < 0 || injectionRate > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(injectionRate), "Injection rate must be between 0 and 1.");
        }

        this.InjectionRate = injectionRate;
        this.Enabled = enabled;
    }

    public double InjectionRate { get; }

    public bool Enabled { get; } = true;
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

public abstract class PipelineBehaviorBase<TRequest, TResponse>(ILoggerFactory loggerFactory) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : IResult
{
    protected const string LogKey = "APP";

    protected ILogger<PipelineBehaviorBase<TRequest, TResponse>> Logger { get; } = loggerFactory?.CreateLogger<PipelineBehaviorBase<TRequest, TResponse>>() ?? throw new ArgumentNullException(nameof(loggerFactory));

    public async Task<TResponse> HandleAsync(
        TRequest request,
        object options,
        Type handlerType,
        Func<Task<TResponse>> next,
        CancellationToken cancellationToken = default)
    {
        if (!this.CanProcess(request, handlerType))
        {
            this.Logger.LogDebug("{LogKey} behavior skipped (type={BehaviorType})", LogKey, this.GetType().Name);
            return await next();
        }

        this.Logger.LogDebug("{LogKey} behavior started (type={BehaviorType})", LogKey, this.GetType().Name);
        var response = await this.Process(request, handlerType, next, cancellationToken);
        this.Logger.LogDebug("{LogKey} behavior finished (type={BehaviorType})", LogKey, this.GetType().Name);
        return response;
    }

    protected abstract bool CanProcess(TRequest request, Type handlerType);

    protected abstract Task<TResponse> Process(
        TRequest request,
        Type handlerType,
        Func<Task<TResponse>> next,
        CancellationToken cancellationToken);

    /// <summary>
    /// Indicates whether the behavior should be applied per handler.
    /// </summary>
    /// <returns><c>true</c> if the behavior is handler-specific (e.g., retry, timeout); <c>false</c> if it should run once per message (e.g., validation).</returns>
    public virtual bool IsHandlerSpecific()
    {
        return false;
    }
}

public class RetryPipelineBehavior<TRequest, TResponse>(
    ILoggerFactory loggerFactory,
    ConcurrentDictionary<Type, PolicyConfig> policyCache) : PipelineBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class
    where TResponse : IResult
{
    private readonly ConcurrentDictionary<Type, PolicyConfig> policyCache = policyCache ?? throw new ArgumentNullException(nameof(policyCache));

    protected override bool CanProcess(TRequest request, Type handlerType)
    {
        return true;
    }

    protected override async Task<TResponse> Process(
        TRequest request,
        Type handlerType,
        Func<Task<TResponse>> next,
        CancellationToken cancellationToken)
    {
        if (!this.policyCache.TryGetValue(handlerType, out var policyConfig) || policyConfig.Retry == null)
        {
            return await next();
        }

        var retryCount = policyConfig.Retry.Count;
        var delay = TimeSpan.FromMilliseconds(policyConfig.Retry.Delay);

        var policy = Policy<TResponse>
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount,
                retryAttempt => delay,
                (exception, timespan, retryAttempt, context) =>
                {
                    this.Logger.LogWarning("{LogKey} retry behavior attempt {RetryAttempt} of {RetryCount} for {HandlerType} after {DelayMs} ms due to {ExceptionMessage}", LogKey, retryAttempt, retryCount, handlerType.Name, timespan.TotalMilliseconds, exception.Exception?.Message);
                });

        return await policy.ExecuteAsync(async context => await next().AnyContext(), cancellationToken).AnyContext();
    }

    public override bool IsHandlerSpecific()
    {
        return true;
    }
}

public class TimeoutPipelineBehavior<TRequest, TResponse>(
    ILoggerFactory loggerFactory,
    ConcurrentDictionary<Type, PolicyConfig> policyCache) : PipelineBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class
    where TResponse : IResult
{
    private readonly ConcurrentDictionary<Type, PolicyConfig> policyCache = policyCache ?? throw new ArgumentNullException(nameof(policyCache));

    protected override bool CanProcess(TRequest request, Type handlerType)
    {
        return handlerType != null && this.policyCache.TryGetValue(handlerType, out var policyConfig) && policyConfig.Timeout != null;
    }

    protected override async Task<TResponse> Process(
        TRequest request,
        Type handlerType,
        Func<Task<TResponse>> next,
        CancellationToken cancellationToken)
    {
        if (!this.policyCache.TryGetValue(handlerType, out var policyConfig) || policyConfig.Timeout == null)
        {
            return await next();
        }

        var timeout = TimeSpan.FromMilliseconds(policyConfig.Timeout.Duration);
        var policy = Policy.TimeoutAsync<TResponse>(
            timeout,
            TimeoutStrategy.Pessimistic,
            (context, timespan, task, exception) =>
            {
                this.Logger.LogWarning("{LogKey} timeout behavior triggered (timeout={TimeoutMs} ms, type={BehaviorType})", LogKey, timespan.TotalMilliseconds, this.GetType().Name);
                return Task.CompletedTask;
            });

        return await policy.ExecuteAsync(async context => await next().AnyContext(), cancellationToken).AnyContext();
    }

    /// <summary>
    /// Indicates that this behavior is handler-specific and should run for each handler.
    /// </summary>
    /// <returns><c>true</c> to indicate this is a handler-specific behavior.</returns>
    public override bool IsHandlerSpecific()
    {
        return true;
    }
}

public class ChaosPipelineBehavior<TRequest, TResponse>(
    ILoggerFactory loggerFactory,
    ConcurrentDictionary<Type, PolicyConfig> policyCache) : PipelineBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class
    where TResponse : IResult
{
    private readonly ConcurrentDictionary<Type, PolicyConfig> policyCache = policyCache ?? throw new ArgumentNullException(nameof(policyCache));

    protected override bool CanProcess(TRequest request, Type handlerType)
    {
        return handlerType != null && this.policyCache.TryGetValue(handlerType, out var policyConfig) && policyConfig.Chaos != null;
    }

    protected override async Task<TResponse> Process(
        TRequest request,
        Type handlerType,
        Func<Task<TResponse>> next,
        CancellationToken cancellationToken)
    {
        if (!this.policyCache.TryGetValue(handlerType, out var policyConfig) || policyConfig.Chaos == null)
        {
            return await next();
        }

        if (policyConfig.Chaos.InjectionRate <= 0)
        {
            this.Logger.LogDebug("{LogKey} chaos behavior skipped due to injection rate <= 0 (type={BehaviorType})", LogKey, this.GetType().Name);
            return await next();
        }

        this.Logger.LogDebug("{LogKey} applying chaos behavior with injection rate {InjectionRate} (type={BehaviorType})", LogKey, policyConfig.Chaos.InjectionRate, this.GetType().Name);

        var policy = MonkeyPolicy.InjectException(with =>
            with.Fault(new ChaosException("Chaos injection triggered"))
                .InjectionRate(policyConfig.Chaos.InjectionRate)
                .Enabled(policyConfig.Chaos.Enabled));

        return await policy.Execute(async context => await next().AnyContext(), cancellationToken).AnyContext();
    }

    /// <summary>
    /// Indicates that this behavior is handler-specific and should run for each handler.
    /// </summary>
    /// <returns><c>true</c> to indicate this is a handler-specific behavior.</returns>
    public override bool IsHandlerSpecific()
    {
        return true;
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class HandlerCircuitBreakerAttribute : Attribute
{
    public HandlerCircuitBreakerAttribute(int attempts, int breakDurationSeconds, int backoffMilliseconds, bool backoffExponential = false)
    {
        if (attempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attempts), "Attempts must be greater than 0.");
        }
        if (breakDurationSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(breakDurationSeconds), "Break duration must be non-negative.");
        }
        if (backoffMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(backoffMilliseconds), "Backoff milliseconds must be non-negative.");
        }

        this.Attempts = attempts;
        this.BreakDuration = TimeSpan.FromSeconds(breakDurationSeconds);
        this.Backoff = TimeSpan.FromMilliseconds(backoffMilliseconds);
        this.BackoffExponential = backoffExponential;
    }

    public int Attempts { get; }

    public TimeSpan BreakDuration { get; }

    public TimeSpan Backoff { get; }

    public bool BackoffExponential { get; }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class HandlerCacheInvalidateAttribute : Attribute
{
    public HandlerCacheInvalidateAttribute(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key), "Cache key cannot be null or empty.");
        }

        this.Key = key;
    }

    public string Key { get; }
}

public class ModuleNotEnabledException(string moduleName) : Exception($"Module '{moduleName}' is not enabled.")
{
    public string ModuleName { get; } = moduleName;
}

public class CircuitBreakerPipelineBehavior<TRequest, TResponse>(
    ILoggerFactory loggerFactory,
    ConcurrentDictionary<Type, PolicyConfig> policyCache) : PipelineBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class
    where TResponse : IResult
{
    private readonly ConcurrentDictionary<Type, PolicyConfig> policyCache = policyCache ?? throw new ArgumentNullException(nameof(policyCache));

    protected override bool CanProcess(TRequest request, Type handlerType)
    {
        return handlerType != null && this.policyCache.TryGetValue(handlerType, out var policyConfig) && policyConfig.CircuitBreaker != null;
    }

    protected override async Task<TResponse> Process(
        TRequest request,
        Type handlerType,
        Func<Task<TResponse>> next,
        CancellationToken cancellationToken)
    {
        if (!this.policyCache.TryGetValue(handlerType, out var policyConfig) || policyConfig.CircuitBreaker == null)
        {
            return await next();
        }

        var options = policyConfig.CircuitBreaker;
        var attempts = 1;

        var retryPolicy = options.BackoffExponential
            ? Policy.Handle<Exception>()
                .WaitAndRetryForeverAsync(
                    attempt => TimeSpan.FromMilliseconds(options.Backoff.Milliseconds * Math.Pow(2, attempt)),
                    (ex, wait) =>
                    {
                        this.Logger.LogError(ex, "{LogKey} circuit breaker behavior (attempt=#{Attempts}, wait={WaitMs} ms, type={BehaviorType}) {ErrorMessage}", LogKey, attempts, wait.TotalMilliseconds, this.GetType().Name, ex.Message);
                        attempts++;
                    })
            : Policy.Handle<Exception>()
                .WaitAndRetryForeverAsync(
                    attempt => options.Backoff,
                    (ex, wait) =>
                    {
                        this.Logger.LogError(ex, "{LogKey} circuit breaker behavior (attempt=#{Attempts}, wait={WaitMs} ms, type={BehaviorType}) {ErrorMessage}", LogKey, attempts, wait.TotalMilliseconds, this.GetType().Name, ex.Message);
                        attempts++;
                    });

        var circuitBreakerPolicy = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                options.Attempts,
                options.BreakDuration,
                (ex, wait) =>
                {
                    this.Logger.LogError(ex, "{LogKey} circuit breaker behavior (circuit=open, wait={WaitMs} ms, type={BehaviorType}) {ErrorMessage}", LogKey, wait.TotalMilliseconds, this.GetType().Name, ex.Message);
                },
                () => this.Logger.LogDebug("{LogKey} circuit breaker behavior (circuit=closed, type={BehaviorType})", LogKey, this.GetType().Name),
                () => this.Logger.LogDebug("{LogKey} circuit breaker behavior (circuit=halfopen, type={BehaviorType})", LogKey, this.GetType().Name));

        var policy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);

        return await policy.ExecuteAsync(async context => await next().AnyContext(), cancellationToken).AnyContext();
    }
}

public class CacheInvalidatePipelineBehavior<TRequest, TResponse>(
    ILoggerFactory loggerFactory,
    ConcurrentDictionary<Type, PolicyConfig> policyCache,
    ICacheProvider provider) : PipelineBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class
    where TResponse : IResult
{
    private readonly ConcurrentDictionary<Type, PolicyConfig> policyCache = policyCache ?? throw new ArgumentNullException(nameof(policyCache));
    private readonly ICacheProvider provider = provider ?? throw new ArgumentNullException(nameof(provider));

    protected override bool CanProcess(TRequest request, Type handlerType)
    {
        return handlerType != null && this.policyCache.TryGetValue(handlerType, out var policyConfig) && policyConfig.CacheInvalidate != null;
    }

    protected override async Task<TResponse> Process(
        TRequest request,
        Type handlerType,
        Func<Task<TResponse>> next,
        CancellationToken cancellationToken)
    {
        if (!this.policyCache.TryGetValue(handlerType, out var policyConfig) || policyConfig.CacheInvalidate == null)
        {
            return await next();
        }

        var key = policyConfig.CacheInvalidate.Key;
        if (string.IsNullOrEmpty(key))
        {
            return await next();
        }

        var result = await next(); // Continue pipeline

        this.Logger.LogDebug("{LogKey} cache invalidate behavior (key={CacheKey}*, type={BehaviorType})", LogKey, key, this.GetType().Name);
        await this.provider.RemoveStartsWithAsync(key, cancellationToken);

        return result;
    }
}

/// <summary>
/// A pipeline behavior that validates messages using FluentValidation.
/// </summary>
/// <typeparam name="TRequest">The type of the message (request or notification).</typeparam>
/// <typeparam name="TResponse">The type of the response, implementing <see cref="IResult"/>.</typeparam>
/// <remarks>
/// This behavior validates the message using a registered FluentValidation validator before passing it to the next behavior or handler.
/// If validation fails, it returns a failed result with validation errors wrapped in <see cref="FluentValidationError"/>; otherwise, it proceeds with the pipeline.
/// It runs once per message (not per handler) to avoid redundant validation.
/// </remarks>
/// <example>
/// <code>
/// services.AddRequester()
///     .AddHandlers(new[] { "^System\\..*" })
///     .WithBehavior<ValidationBehavior<,>>();
///
/// public class MyRequest : RequestBase<Unit>
/// {
///     public string Name { get; set; }
///
///     public class Validator : AbstractValidator<MyRequest>
///     {
///         public Validator()
///         {
///             RuleFor(x => x.Name).NotEmpty();
///         }
///     }
/// }
/// </code>
/// </example>
/// <remarks>
/// Initializes a new instance of the <see cref="ValidationPipelineBehavior{TRequest, TResponse}"/> class.
/// </remarks>
/// <param name="loggerFactory">The logger factory for creating loggers.</param>
/// <param name="validators">The collection of validators for the message type.</param>
public class ValidationPipelineBehavior<TRequest, TResponse>(ILoggerFactory loggerFactory, IEnumerable<IValidator<TRequest>> validators = null) : PipelineBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class
    where TResponse : IResult
{
    private readonly IValidator<TRequest>[] validators = validators?.ToArray();

    /// <summary>
    /// Indicates whether the behavior can process the specified message.
    /// </summary>
    /// <param name="request">The message to process.</param>
    /// <param name="handlerType">The type of the handler, if applicable.</param>
    /// <returns><c>true</c> if there are validators to apply; otherwise, <c>false</c>.</returns>
    protected override bool CanProcess(TRequest request, Type handlerType)
    {
        return this.validators.SafeAny();
    }

    /// <summary>
    /// Validates the message using FluentValidation and proceeds if validation passes.
    /// </summary>
    /// <param name="request">The message to process.</param>
    /// <param name="handlerType">The type of the handler, if applicable.</param>
    /// <param name="next">The delegate to invoke the next behavior or handler in the pipeline.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task representing the result of the processing, returning a <see cref="TResponse"/>.</returns>
    protected override async Task<TResponse> Process(TRequest request, Type handlerType, Func<Task<TResponse>> next, CancellationToken cancellationToken)
    {
        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(this.validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var errors = validationResults
            .Where(r => !r.IsValid)
            .Select(r => new FluentValidationError(r)).ToList();

        if (errors.Any())
        {
            // Determine the type of TResponse and create the appropriate failure result
            var responseType = typeof(TResponse);
            if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(IResult<>))
            {
                // TResponse is Result<TValue>
                var valueType = responseType.GetGenericArguments()[0];
                var resultType = typeof(Result<>).MakeGenericType(valueType);
                var failureMethod = resultType.GetMethod(nameof(Result<object>.Failure), new[] { typeof(IEnumerable<string>), typeof(IEnumerable<IResultError>) });
                var failureResult = failureMethod.Invoke(null, new object[] { null, errors });

                return (TResponse)failureResult;
            }
            else if (responseType == typeof(IResult))
            {
                // TResponse is Result (non-generic, for notifications)
                return (TResponse)(object)Result.Failure().WithErrors(errors);
            }
            else
            {
                throw new InvalidOperationException($"Unsupported response type '{responseType}' in ValidationBehavior.");
            }
        }

        return await next();
    }

    /// <summary>
    /// Indicates that this behavior should run once per message, not per handler.
    /// </summary>
    /// <returns><c>false</c> to indicate this is not a handler-specific behavior.</returns>
    public override bool IsHandlerSpecific()
    {
        return false;
    }
}