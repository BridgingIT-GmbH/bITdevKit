namespace BridgingIT.DevKit.Common.Utilities;

using System.Reflection;
using BridgingIT.DevKit.Common.Resiliancy;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provides a request/response pattern for handling commands and queries.
/// </summary>
/// <remarks>
/// Initializes a new instance of the SimpleRequester class with the specified settings.
/// </remarks>
/// <param name="logger">An optional logger to log errors if handleErrors is true. Defaults to null.</param>
/// <param name="handleErrors">If true, catches and logs exceptions from request handlers; otherwise, throws them. Defaults to false.</param>
/// <param name="pipelineBehaviors">A list of pipeline behaviors to execute before and after request handling, applied in reverse order. Defaults to an empty list.</param>
/// <param name="progress">An optional progress reporter for request operations. Defaults to null.</param>
/// <example>
/// <code>
/// var requester = new SimpleRequester(progress: new Progress<SimpleRequesterProgress>(p => Console.WriteLine($"Progress: {p.Status}, Request Type: {p.RequestType}")));
/// requester.RegisterHandler(new MyRequestHandler());
/// var response = await requester.SendAsync(new MyRequest { Data = "Test" }, CancellationToken.None);
/// Console.WriteLine(response.Result);
/// </code>
/// </example>
public class SimpleRequester(
    ILogger logger = null,
    bool handleErrors = false,
    IEnumerable<ISimpleRequestPipelineBehavior> pipelineBehaviors = null,
    IProgress<SimpleRequesterProgress> progress = null)
{
    private readonly Dictionary<Type, (ISimpleRequestHandler Handler, Type ResponseType)> handlers = [];
    private readonly ISimpleRequestPipelineBehavior[] pipelineBehaviors = pipelineBehaviors?.Reverse()?.ToArray() ?? [];
    private readonly ReaderWriterLockSlim lockObject = new(); // Upgraded to RW lock for potential concurrency
    private readonly IProgress<SimpleRequesterProgress> progress = progress;

    /// <summary>
    /// Registers a handler for a specific request type.
    /// </summary>
    /// <typeparam name="TRequest">The type of request to handle.</typeparam>
    /// <typeparam name="TResponse">The type of response returned by the handler.</typeparam>
    /// <param name="handler">The handler to register.</param>
    /// <exception cref="InvalidOperationException">Thrown if a handler for the request type is already registered.</exception>
    /// <example>
    /// <code>
    /// var requester = new SimpleRequester();
    /// requester.RegisterHandler(new MyRequestHandler());
    /// </code>
    /// </example>
    public void RegisterHandler<TRequest, TResponse>(ISimpleRequestHandler<TRequest, TResponse> handler)
        where TRequest : ISimpleRequest<TResponse>
    {
        this.lockObject.EnterWriteLock();
        try
        {
            var requestType = typeof(TRequest);
            if (this.handlers.ContainsKey(requestType))
            {
                throw new InvalidOperationException($"A handler for request type {requestType.Name} is already registered.");
            }
            this.handlers[requestType] = (handler, typeof(TResponse));
        }
        finally
        {
            this.lockObject.ExitWriteLock();
        }
    }

    /// <summary>
    /// Registers a handler function for a specific request type.
    /// </summary>
    /// <typeparam name="TRequest">The type of request to handle.</typeparam>
    /// <typeparam name="TResponse">The type of response returned by the handler.</typeparam>
    /// <param name="handlerFunc">The handler function to register.</param>
    /// <exception cref="InvalidOperationException">Thrown if a handler for the request type is already registered.</exception>
    /// <example>
    /// <code>
    /// var requester = new SimpleRequester();
    /// requester.RegisterHandler<MyRequest, MyResponse>(async (req, ct) => new MyResponse { Result = req.Data });
    /// </code>
    /// </example>
    public void RegisterHandler<TRequest, TResponse>(Func<TRequest, CancellationToken, ValueTask<TResponse>> handlerFunc)
        where TRequest : ISimpleRequest<TResponse>
    {
        var handler = new SimpleRequestHandler<TRequest, TResponse>(handlerFunc);
        this.RegisterHandler(handler);
    }

    /// <summary>
    /// Registers a handler for a specific request type.
    /// </summary>
    /// <param name="handler">The handler to register.</param>
    /// <exception cref="InvalidOperationException">Thrown if a handler for the request type is already registered or the handler does not implement ISimpleRequestHandler<TRequest, TResponse>.</exception>
    /// <example>
    /// <code>
    /// var requester = new SimpleRequester();
    /// requester.RegisterHandler(new MyRequestHandler());
    /// </code>
    /// </example>
    public void RegisterHandler(ISimpleRequestHandler handler)
    {
        var handlerType = handler.GetType();
        var interfaceType = handlerType.GetInterfaces().FirstOrDefault(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISimpleRequestHandler<,>));
        if (interfaceType == null)
        {
            throw new InvalidOperationException("The handler does not implement ISimpleRequestHandler<TRequest, TResponse>.");
        }

        var tRequest = interfaceType.GetGenericArguments()[0];
        var tResponse = interfaceType.GetGenericArguments()[1];
        var method = this.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).First(static m => m.Name == nameof(RegisterHandler) && m.GetGenericArguments().Length == 2 && m.GetParameters().Length == 1);
        var genericMethod = method.MakeGenericMethod(tRequest, tResponse);
        genericMethod.Invoke(this, [handler]);
    }

    /// <summary>
    /// Sends a request to its registered handler and returns the response.
    /// </summary>
    /// <typeparam name="TRequest">The type of request to send.</typeparam>
    /// <typeparam name="TResponse">The type of response expected from the handler.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="progress">An optional progress reporter for request operations. Defaults to null.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, returning the response from the handler.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no handler is registered for the request type.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
    /// <example>
    /// <code>
    /// var cts = new CancellationTokenSource();
    /// var progress = new Progress<SimpleRequesterProgress>(p => Console.WriteLine($"Progress: {p.Status}, Request Type: {p.RequestType}"));
    /// var requester = new SimpleRequester();
    /// requester.RegisterHandler(new MyRequestHandler());
    /// var response = await requester.SendAsync(new MyRequest { Data = "Test" }, cts.Token, progress);
    /// Console.WriteLine(response.Result);
    /// cts.Cancel(); // Cancel the operation if needed
    /// </code>
    /// </example>
    public async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, IProgress<SimpleRequesterProgress> progress = null, CancellationToken cancellationToken = default)
        where TRequest : ISimpleRequest<TResponse>
    {
        progress ??= this.progress;
        var requestType = typeof(TRequest);
        (ISimpleRequestHandler Handler, Type ResponseType) handlerInfo;
        this.lockObject.EnterReadLock();
        try
        {
            if (!this.handlers.TryGetValue(requestType, out handlerInfo))
            {
                throw new InvalidOperationException($"No handler registered for request type {requestType.Name}.");
            }
        }
        finally
        {
            this.lockObject.ExitReadLock();
        }

        progress?.Report(new SimpleRequesterProgress(requestType.Name, $"Processing request of type {requestType.Name}"));
        try
        {
            if (this.pipelineBehaviors.Length == 0)
            {
                return await ((ISimpleRequestHandler<TRequest, TResponse>)handlerInfo.Handler).HandleAsync(request, cancellationToken);
            }

            RequestHandlerDelegate<TResponse> next = () => ((ISimpleRequestHandler<TRequest, TResponse>)handlerInfo.Handler).HandleAsync(request, cancellationToken);
            for (var i = this.pipelineBehaviors.Length - 1; i >= 0; i--)
            {
                var behavior = this.pipelineBehaviors[i];
                var currentNext = next;
                next = () => behavior.HandleAsync(request, currentNext, cancellationToken);
            }
            return await next();
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
public interface ISimpleRequest;

/// <summary>
/// Defines a request that returns a response.
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the request.</typeparam>
public interface ISimpleRequest<TResponse> : ISimpleRequest;

/// <summary>
/// Non-generic base interface for request handlers.
/// </summary>
public interface ISimpleRequestHandler
{
    ValueTask<object> HandleAsync(object request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines a handler for a specific request type.
/// </summary>
/// <typeparam name="TRequest">The type of request to handle.</typeparam>
/// <typeparam name="TResponse">The type of response returned by the handler.</typeparam>
public interface ISimpleRequestHandler<in TRequest, TResponse> : ISimpleRequestHandler
    where TRequest : ISimpleRequest<TResponse>
{
    ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);

#pragma warning disable CS1066 // The default value specified will have no effect because it applies to a member that is used in contexts that do not allow optional arguments
    ValueTask<object> ISimpleRequestHandler.HandleAsync(object request, CancellationToken cancellationToken = default)
#pragma warning restore CS1066 // The default value specified will have no effect because it applies to a member that is used in contexts that do not allow optional arguments
    {
        if (request is TRequest typedRequest)
        {
            return new ValueTask<object>(this.HandleAsync(typedRequest, cancellationToken).AsTask().ContinueWith(static t => t.Result));
        }

        throw new InvalidOperationException($"Request type {request.GetType().Name} does not match expected type {typeof(TRequest).Name}.");
    }
}

/// <summary>
/// Implements a handler for a specific request type using a function.
/// </summary>
/// <typeparam name="TRequest">The type of request to handle.</typeparam>
/// <typeparam name="TResponse">The type of response returned by the handler.</typeparam>
public class SimpleRequestHandler<TRequest, TResponse>(Func<TRequest, CancellationToken, ValueTask<TResponse>> handlerFunc) : ISimpleRequestHandler<TRequest, TResponse>
    where TRequest : ISimpleRequest<TResponse>
{
    private readonly Func<TRequest, CancellationToken, ValueTask<TResponse>> handlerFunc = handlerFunc;

    public ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        return this.handlerFunc(request, cancellationToken);
    }
}

/// <summary>
/// Defines a pipeline behavior for pre- and post-processing of requests.
/// </summary>
public interface ISimpleRequestPipelineBehavior
{
    ValueTask<TResponse> HandleAsync<TResponse>(ISimpleRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default);
}

/// <summary>
/// Delegate for request handler in pipeline.
/// </summary>
public delegate ValueTask<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>
/// A sample pipeline behavior that logs request handling.
/// </summary>
public class LoggingRequestPipelineBehavior(ILogger logger) : ISimpleRequestPipelineBehavior
{
    public async ValueTask<TResponse> HandleAsync<TResponse>(ISimpleRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
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
/// A fluent builder for configuring and creating a SimpleRequester instance.
/// </summary>
public class SimpleRequesterBuilder
{
    private bool handleErrors;
    private ILogger logger;
    private readonly List<ISimpleRequestPipelineBehavior> pipelineBehaviors = [];
    private IProgress<SimpleRequesterProgress> progress;

    /// <summary>
    /// Initializes a new instance of the SimpleRequesterBuilder.
    /// </summary>
    public SimpleRequesterBuilder()
    {
        this.handleErrors = false;
        this.logger = null;
        this.progress = null;
    }

    /// <summary>
    /// Configures the requester to handle errors by logging them instead of throwing.
    /// </summary>
    /// <param name="logger">The logger to use for error logging. If null, errors are silently ignored.</param>
    /// <returns>The SimpleRequesterBuilder instance for chaining.</returns>
    public SimpleRequesterBuilder HandleErrors(ILogger logger = null)
    {
        this.handleErrors = true;
        this.logger = logger;
        return this;
    }

    /// <summary>
    /// Adds a pipeline behavior to the requester for pre- and post-processing of requests.
    /// </summary>
    /// <param name="behavior">The pipeline behavior to add.</param>
    /// <returns>The SimpleRequesterBuilder instance for chaining.</returns>
    public SimpleRequesterBuilder AddPipelineBehavior(ISimpleRequestPipelineBehavior behavior)
    {
        this.pipelineBehaviors.Add(behavior);
        return this;
    }

    /// <summary>
    /// Configures the requester to report progress using the specified progress reporter.
    /// </summary>
    /// <param name="progress">The progress reporter to use for request operations.</param>
    /// <returns>The SimpleRequesterBuilder instance for chaining.</returns>
    public SimpleRequesterBuilder WithProgress(IProgress<SimpleRequesterProgress> progress)
    {
        this.progress = progress;
        return this;
    }

    /// <summary>
    /// Builds and returns a configured SimpleRequester instance.
    /// </summary>
    /// <returns>A configured SimpleRequester instance.</returns>
    public SimpleRequester Build()
    {
        return new SimpleRequester(
            this.logger,
            this.handleErrors,
            this.pipelineBehaviors,
            this.progress);
    }
}