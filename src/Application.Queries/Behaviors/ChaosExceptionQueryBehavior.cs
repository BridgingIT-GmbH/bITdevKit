// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;

using Polly.Contrib.Simmy;
using Polly.Contrib.Simmy.Outcomes;

public class ChaosExceptionQueryBehavior<TRequest, TResponse>(ILoggerFactory loggerFactory)
    : QueryBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class, IRequest<TResponse>
{
    protected override bool CanProcess(TRequest request)
    {
        return request is IChaosExceptionQuery;
    }

    protected override async Task<TResponse> Process(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // chaos only if implements interface
        if (request is not IChaosExceptionQuery instance)
        {
            return await next().AnyContext();
        }

        if (instance.Options.InjectionRate <= 0)
        {
            return await next().AnyContext();
        }

        // https://github.com/Polly-Contrib/Simmy#Inject-exception
        var policy = MonkeyPolicy.InjectException(with =>
            with.Fault(instance.Options.Fault ?? new ChaosException())
                .InjectionRate(instance.Options.InjectionRate)
                .Enabled());

        return await policy.Execute(async context => await next().AnyContext(), cancellationToken);
    }
}