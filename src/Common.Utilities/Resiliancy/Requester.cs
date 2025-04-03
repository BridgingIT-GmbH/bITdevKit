namespace BridgingIT.DevKit.Common.Utilities;

using BridgingIT.DevKit.Common.Resiliancy;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides a request/response pattern for handling commands and queries.
/// </summary>
/// <remarks>
/// Initializes a new instance of the Requester class with the specified settings.
/// </remarks>
/// <param name="handleErrors">If true, catches and logs exceptions from request handlers; otherwise, throws them. Defaults to false.</param>
/// <param name="logger">An optional logger to log errors if handleErrors is true. Defaults to null.</param>
/// <param name="pipelineBehaviors">A list of pipeline behaviors to execute before and after request handling, applied in reverse order. Defaults to an empty list.</param>
/// <param name="progress">An optional progress reporter for request operations. Defaults to null.</param>
/// <example>
/// <code>
/// var requester = new Requester(progress: new Progress<ResiliencyProgress>(p => Console.WriteLine(p.Status)));
/// requester.RegisterHandler(new MyRequestHandler());
/// var response = await requester.SendAsync(new MyRequest { Data = "Test" }, CancellationToken.None);
/// Console.WriteLine(response.Result);
/// </code>
/// </example>
public class Requester(
    bool handleErrors = false,
    ILogger logger = null,
    IEnumerable<IRequestPipelineBehavior> pipelineBehaviors = null,
    IProgress<ResiliencyProgress> progress = null)
{
    private readonly Dictionary<Type, (IRequestHandler Handler, Type ResponseType)> handlers = [];
    private readonly List<IRequestPipelineBehavior> pipelineBehaviors = pipelineBehaviors?.Reverse().ToList() ?? [];
    private readonly Lock lockObject = new();
    private readonly IProgress<ResiliencyProgress> progress = progress;

    /// <summary>
    /// Registers a handler for a specific request type.
    /// </summary>
    /// <typeparam name="TRequest">The type of request to handle.</typeparam>
    /// <typeparam name="TResponse">The type of response returned by the handler.</typeparam>
    /// <param name="handler">The handler to register.</param>
    /// <exception cref="InvalidOperationException">Thrown if a handler for the request type is already registered.</exception>
    /// <example>
    /// <code>
    /// var requester = new Requester();
    /// requester.RegisterHandler(new MyRequestHandler());
    /// </code>
    /// </example>
    public void RegisterHandler<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> handler)
        where TRequest : IRequest<TResponse>
    {
        lock (this.lockObject)
        {
            var requestType = typeof(TRequest);
            if (this.handlers.ContainsKey(requestType))
            {
                throw new InvalidOperationException($"A handler for request type {requestType.Name} is already registered.");
            }
            this.handlers[requestType] = (handler, typeof(TResponse));
        }
    }

    /// <summary>
    /// Sends a request to its registered handler and returns the response.
    /// </summary>
    /// <typeparam name="TRequest">The type of request to send.</typeparam>
    /// <typeparam name="TResponse">The type of response expected from the handler.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <param name="progress">An optional progress reporter for request operations. Defaults to null.</param>
    /// <returns>A task representing the asynchronous operation, returning the response from the handler.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no handler is registered for the request type.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
    /// <example>
    /// <code>
    /// var cts = new CancellationTokenSource();
    /// var progress = new Progress<ResiliencyProgress>(p => Console.WriteLine($"Progress: {p.Status}"));
    /// var requester = new Requester();
    /// requester.RegisterHandler(new MyRequestHandler());
    /// var response = await requester.SendAsync(new MyRequest { Data = "Test" }, cts.Token, progress);
    /// Console.WriteLine(response.Result);
    /// cts.Cancel(); // Cancel the operation if needed
    /// </code>
    /// </example>
    public async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default, IProgress<ResiliencyProgress> progress = null)
        where TRequest : IRequest<TResponse>
    {
        progress ??= this.progress; // Use instance-level progress if provided
        var requestType = typeof(TRequest);
        (IRequestHandler Handler, Type ResponseType) handlerInfo;
        lock (this.lockObject)
        {
            if (!this.handlers.TryGetValue(requestType, out handlerInfo))
            {
                throw new InvalidOperationException($"No handler registered for request type {requestType.Name}.");
            }
        }

        progress?.Report(new RequesterProgress(requestType.Name, $"Processing request of type {requestType.Name}"));
        try
        {
            // Apply pipeline behaviors
            Func<Task<object>> next = async () => await ((IRequestHandler<TRequest, TResponse>)handlerInfo.Handler).HandleAsync(request, cancellationToken);
            foreach (var behavior in this.pipelineBehaviors)
            {
                var currentNext = next;
                next = () => behavior.HandleAsync(request, cancellationToken, currentNext);
            }
            var result = await next();
            return (TResponse)result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (handleErrors)
        {
            logger?.LogError(ex, $"Failed to handle request of type {typeof(TRequest).Name}.");
            return default;
        }
    }
}

/// <summary>
/// Defines a non-generic base interface for all requests.
/// </summary>
public interface IRequest;

/// <summary>
/// Defines a request that returns a response.
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the request.</typeparam>
public interface IRequest<TResponse> : IRequest;

/// <summary>
/// Non-generic base interface for request handlers.
/// </summary>
public interface IRequestHandler
{
    Task<object> HandleAsync(object request, CancellationToken cancellationToken);
}

/// <summary>
/// Defines a handler for a specific request type.
/// </summary>
/// <typeparam name="TRequest">The type of request to handle.</typeparam>
/// <typeparam name="TResponse">The type of response returned by the handler.</typeparam>
public interface IRequestHandler<in TRequest, TResponse> : IRequestHandler
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);

    async Task<object> IRequestHandler.HandleAsync(object request, CancellationToken cancellationToken)
    {
        if (request is TRequest typedRequest)
        {
            return await this.HandleAsync(typedRequest, cancellationToken);
        }
        throw new InvalidOperationException($"Request type {request.GetType().Name} does not match expected type {typeof(TRequest).Name}.");
    }
}

/// <summary>
/// Defines a pipeline behavior for pre- and post-processing of requests.
/// </summary>
public interface IRequestPipelineBehavior
{
    Task<object> HandleAsync(IRequest request, CancellationToken cancellationToken, Func<Task<object>> next);
}

/// <summary>
/// A sample pipeline behavior that logs request handling.
/// </summary>
public class LoggingRequestPipelineBehavior(ILogger logger) : IRequestPipelineBehavior
{
    public async Task<object> HandleAsync(IRequest request, CancellationToken cancellationToken, Func<Task<object>> next)
    {
        logger?.LogInformation($"Handling request of type {request.GetType().Name}");
        try
        {
            var result = await next();
            logger?.LogInformation($"Successfully handled request of type {request.GetType().Name}");
            return result;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, $"Failed to handle request of type {request.GetType().Name}");
            throw;
        }
    }
}

/// <summary>
/// A fluent builder for configuring and creating a Requester instance.
/// </summary>
public class RequesterBuilder
{
    private bool handleErrors;
    private ILogger logger;
    private readonly List<IRequestPipelineBehavior> pipelineBehaviors;
    private IProgress<ResiliencyProgress> progress;

    /// <summary>
    /// Initializes a new instance of the RequesterBuilder.
    /// </summary>
    public RequesterBuilder()
    {
        this.handleErrors = false;
        this.logger = null;
        this.pipelineBehaviors = [];
        this.progress = null;
    }

    /// <summary>
    /// Configures the requester to handle errors by logging them instead of throwing.
    /// </summary>
    /// <param name="logger">The logger to use for error logging. If null, errors are silently ignored.</param>
    /// <returns>The RequesterBuilder instance for chaining.</returns>
    public RequesterBuilder HandleErrors(ILogger logger = null)
    {
        this.handleErrors = true;
        this.logger = logger;
        return this;
    }

    /// <summary>
    /// Adds a pipeline behavior to the requester for pre- and post-processing of requests.
    /// </summary>
    /// <param name="behavior">The pipeline behavior to add.</param>
    /// <returns>The RequesterBuilder instance for chaining.</returns>
    public RequesterBuilder AddPipelineBehavior(IRequestPipelineBehavior behavior)
    {
        this.pipelineBehaviors.Add(behavior);
        return this;
    }

    /// <summary>
    /// Configures the requester to report progress using the specified progress reporter.
    /// </summary>
    /// <param name="progress">The progress reporter to use for request operations.</param>
    /// <returns>The RequesterBuilder instance for chaining.</returns>
    public RequesterBuilder WithProgress(IProgress<ResiliencyProgress> progress)
    {
        this.progress = progress;
        return this;
    }

    /// <summary>
    /// Builds and returns a configured Requester instance.
    /// </summary>
    /// <returns>A configured Requester instance.</returns>
    public Requester Build()
    {
        return new Requester(
            this.handleErrors,
            this.logger,
            this.pipelineBehaviors,
            this.progress);
    }
}