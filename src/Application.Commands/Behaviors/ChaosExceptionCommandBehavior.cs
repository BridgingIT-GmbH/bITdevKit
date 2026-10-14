// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

using Polly;
using Polly.Simmy;
using Polly.Simmy.Fault;

/// <summary>
/// Provides chaos exception command behavior.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <param name="loggerFactory">The factory used to create loggers.</param>
public class ChaosExceptionCommandBehavior<TRequest, TResponse>(ILoggerFactory loggerFactory)
    : CommandBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class, MediatR.IRequest<TResponse>
{
    /// <inheritdoc/>
    protected override bool CanProcess(TRequest request)
    {
        return request is IChaosExceptionCommand;
    }

    /// <inheritdoc/>
    protected override async Task<TResponse> Process(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // chaos only if implements interface
        if (request is not IChaosExceptionCommand instance)
        {
            return await next().AnyContext();
        }

        if (instance.Options.InjectionRate <= 0)
        {
            return await next().AnyContext();
        }

        // https://www.pollydocs.org/chaos/fault.html
        var fault = instance.Options.Fault ?? new ChaosException();
        var pipeline = new ResiliencePipelineBuilder()
            .AddChaosFault(new ChaosFaultStrategyOptions
            {
                FaultGenerator = _ => new ValueTask<Exception>(fault),
                InjectionRate = instance.Options.InjectionRate,
                Enabled = true
            })
            .Build();

        return await pipeline.ExecuteAsync(async _ => await next().AnyContext(), cancellationToken);
    }
}
