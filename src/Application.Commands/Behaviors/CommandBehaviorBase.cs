// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

using Microsoft.Extensions.Logging.Abstractions;

// https://github.com/jbogard/MediatR/wiki/Behaviors
/// <summary>
/// Represents command behavior base.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public abstract class CommandBehaviorBase<TRequest, TResponse>
    : MediatR.IPipelineBehavior<TRequest, TResponse>, ICommandBehavior<TRequest, TResponse>
    where TRequest : class, MediatR.IRequest<TResponse>
{
    /// <summary>
    /// Initializes a new instance of the <c>CommandBehaviorBase</c> class.
    /// </summary>
    /// <param name="loggerFactory">The factory used to create loggers.</param>
    protected CommandBehaviorBase(ILoggerFactory loggerFactory)
    {
        this.Logger = loggerFactory?.CreateLogger(this.GetType()) ??
            NullLoggerFactory.Instance.CreateLogger(this.GetType());
    }

    /// <summary>
    /// Gets the logger.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Handles .
    /// </summary>
    /// <param name="request">The request used by the operation.</param>
    /// <param name="next">The next used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!this.CanProcess(request) ||
            !(request.GetType().ImplementsInterface(typeof(ICommandRequest)) ||
                request.GetType().ImplementsInterface(typeof(ICommandRequest<>))))
        {
            return await next().AnyContext();
        }

        //try
        //{
        this.Logger.LogDebug("[{LogKey}] behavior processing (type={BehaviorType})", Constants.LogKey, this.GetType().Name);

        var watch = ValueStopwatch.StartNew();
        var response = await this.Process(request, next, cancellationToken).AnyContext();

        this.Logger.LogDebug("[{LogKey}] behavior processed (type={BehaviorType}) -> took {TimeElapsed:0.0000} ms", Constants.LogKey, this.GetType().Name, watch.GetElapsedMilliseconds());

        return response;
        //}
        //catch (Exception ex)
        //{
        //    this.Logger.LogError(ex, "[{LogKey}] behavior processing error (type={BehaviorType}): {ErrorMessage}", Constants.LogKey, this.GetType().Name, ex.Message);
        //    throw;
        //}
    }

    /// <summary>
    /// Determines whether can process.
    /// </summary>
    /// <param name="request">The request used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    protected abstract bool CanProcess(TRequest request);

    /// <summary>
    /// Executes the process operation.
    /// </summary>
    /// <param name="request">The request used by the operation.</param>
    /// <param name="next">The next used by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task<TResponse> Process(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}
