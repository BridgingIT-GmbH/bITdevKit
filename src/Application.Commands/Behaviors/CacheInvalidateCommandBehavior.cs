// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands;

using System;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using MediatR;
using Microsoft.Extensions.Logging;

public class CacheInvalidateCommandBehavior<TRequest, TResponse>(ILoggerFactory loggerFactory, ICacheProvider provider) : CommandBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class, IRequest<TResponse>
{
    private readonly ICacheProvider provider = provider ?? throw new ArgumentNullException(nameof(provider));

    protected override bool CanProcess(TRequest request)
    {
        return request is ICacheInvalidateCommand;
    }

    protected override async Task<TResponse> Process(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // cache only if implements interface
        if (request is not ICacheInvalidateCommand instance)
        {
            return await next().AnyContext();
        }

        if (instance.Options.Key.IsNullOrEmpty())
        {
            return await next().AnyContext();
        }

        var result = await next().AnyContext(); // continue pipeline

        if (!string.IsNullOrEmpty(instance.Options.Key))
        {
            this.Logger.LogDebug("{LogKey} command cache invalidate behavior (key={CacheKey}*, type={BehaviorType})", Constants.LogKey, instance.Options.Key, this.GetType().Name);
            this.provider.RemoveStartsWith(instance.Options.Key);
        }

        return result;
    }
}