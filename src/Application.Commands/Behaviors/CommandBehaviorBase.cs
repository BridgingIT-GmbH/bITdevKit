// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using MediatR;
using Microsoft.Extensions.Logging;

// https://github.com/jbogard/MediatR/wiki/Behaviors
public abstract class CommandBehaviorBase<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>, ICommandBehavior<TRequest, TResponse>
    where TRequest : class, IRequest<TResponse>
{
    protected CommandBehaviorBase(ILoggerFactory loggerFactory)
    {
        this.Logger = loggerFactory.CreateLogger(this.GetType());
    }

    protected ILogger Logger { get; }

    public virtual async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!this.CanProcess(request) ||
            !(request.GetType().ImplementsInterface(typeof(ICommandRequest)) || request.GetType().ImplementsInterface(typeof(ICommandRequest<>))))
        {
            return await next().AnyContext();
        }

        //try
        //{
        this.Logger.LogDebug("{LogKey} behavior processing (type={BehaviorType})", Constants.LogKey, this.GetType().Name);

        var watch = ValueStopwatch.StartNew();
        var response = await this.Process(request, next, cancellationToken).AnyContext();

        this.Logger.LogDebug("{LogKey} behavior processed (type={BehaviorType}) -> took {TimeElapsed:0.0000} ms", Constants.LogKey, this.GetType().Name, watch.GetElapsedMilliseconds());

        return response;
        //}
        //catch (Exception ex)
        //{
        //    this.Logger.LogError(ex, "{LogKey} behavior processing error (type={BehaviorType}): {ErrorMessage}", Constants.LogKey, this.GetType().Name, ex.Message);
        //    throw;
        //}
    }

    protected abstract bool CanProcess(TRequest request);

    protected abstract Task<TResponse> Process(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}