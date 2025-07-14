// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;

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
