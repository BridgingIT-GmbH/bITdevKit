// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Polly.Contrib.Simmy;
using Polly.Contrib.Simmy.Outcomes;

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
