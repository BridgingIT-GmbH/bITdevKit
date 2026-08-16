// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;

/// <summary>
///     Provides conditional execution and lifecycle logging for requester and notifier pipeline behaviors.
/// </summary>
/// <typeparam name="TRequest">The request or notification type.</typeparam>
/// <typeparam name="TResponse">The result response type.</typeparam>
/// <param name="loggerFactory">The factory used to create the behavior logger.</param>
public abstract class PipelineBehaviorBase<TRequest, TResponse>(ILoggerFactory loggerFactory) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class
    where TResponse : IResult
{
    /// <summary>Gets the structured log key used by pipeline behaviors.</summary>
    protected const string LogKey = "APP";

    /// <summary>Gets the logger used for pipeline lifecycle messages.</summary>
    protected ILogger<PipelineBehaviorBase<TRequest, TResponse>> Logger { get; } = loggerFactory?.CreateLogger<PipelineBehaviorBase<TRequest, TResponse>>() ?? throw new ArgumentNullException(nameof(loggerFactory));

    /// <inheritdoc/>
    public async Task<TResponse> HandleAsync(
        TRequest request,
        object options,
        Type handlerType,
        Func<Task<TResponse>> next,
        CancellationToken cancellationToken = default)
    {
        if (!this.CanProcess(request, handlerType))
        {
            this.Logger.LogDebug("[{LogKey}] behavior skipped (type={BehaviorType})", LogKey, this.GetType().Name);
            return await next();
        }

        this.Logger.LogDebug("[{LogKey}] behavior started (type={BehaviorType})", LogKey, this.GetType().Name);
        var response = await this.Process(request, handlerType, next, cancellationToken);
        this.Logger.LogDebug("[{LogKey}] behavior finished (type={BehaviorType})", LogKey, this.GetType().Name);
        return response;
    }

    /// <summary>
    ///     Determines whether this behavior applies to the current request and handler.
    /// </summary>
    /// <param name="request">The request or notification.</param>
    /// <param name="handlerType">The concrete handler type.</param>
    /// <returns><see langword="true"/> when the behavior should execute; otherwise, <see langword="false"/>.</returns>
    protected abstract bool CanProcess(TRequest request, Type handlerType);

    /// <summary>
    ///     Executes behavior logic around the next pipeline delegate.
    /// </summary>
    /// <param name="request">The request or notification.</param>
    /// <param name="handlerType">The concrete handler type.</param>
    /// <param name="next">The next pipeline delegate.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The pipeline response.</returns>
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
