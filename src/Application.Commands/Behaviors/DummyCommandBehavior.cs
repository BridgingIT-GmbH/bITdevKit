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

public class DummyCommandBehavior<TRequest, TResponse> : CommandBehaviorBase<TRequest, TResponse>
    where TRequest : class, IRequest<TResponse>
{
    public DummyCommandBehavior(ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
    }

    protected override bool CanProcess(TRequest request)
    {
        return true;
    }

    protected override async Task<TResponse> Process(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        this.Logger.LogInformation("{LogKey} dummy command", Constants.LogKey);

        return await next().AnyContext(); // continue pipeline
    }
}