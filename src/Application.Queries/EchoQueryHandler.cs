﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Queries;

public class EchoQueryHandler(ILoggerFactory loggerFactory) : QueryHandlerBase<EchoQuery, string>(loggerFactory)
{
    public override async Task<QueryResponse<string>> Process(EchoQuery request, CancellationToken cancellationToken)
    {
        return await Task.FromResult(new QueryResponse<string> { Result = "echo" }).AnyContext();
    }
}