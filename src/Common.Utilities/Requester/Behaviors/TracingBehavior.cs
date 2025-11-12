// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public class TracingBehavior<TRequest, TResponse>(
    ILoggerFactory loggerFactory,
    ActivitySource activitySource = null) : PipelineBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class
    where TResponse : IResult
{
    private readonly ActivitySource activitySource = activitySource;

    protected override bool CanProcess(TRequest request, Type handlerType)
    {
        return true; // Always process, no attribute required
    }

    protected override async Task<TResponse> Process(
        TRequest request,
        Type handlerType,
        Func<Task<TResponse>> next,
        CancellationToken cancellationToken)
    {
        var requestType = typeof(TRequest).PrettyName();

        return await this.activitySource.StartActvity($"REQUEST {requestType}",
            async (a, c) =>
            {
                a?.AddEvent(new ActivityEvent($"processing (type={requestType}, id={handlerType})"));

                return await next();
            },
            tags: new Dictionary<string, string>
            {
                //["command.request_id"] = command.RequestId.ToString("N"),
                ["command.request_type"] = requestType
            },
            baggages: new Dictionary<string, string>
            {
                //["command.id"] = command.RequestId.ToString("N"),
                ["command.type"] = requestType
            },
            cancellationToken: cancellationToken);
    }
}