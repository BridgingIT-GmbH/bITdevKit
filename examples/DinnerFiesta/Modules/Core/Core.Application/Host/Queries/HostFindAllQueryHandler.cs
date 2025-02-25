﻿// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

public class HostFindAllQueryHandler(ILoggerFactory loggerFactory, IGenericRepository<Host> repository)
    : QueryHandlerBase<HostFindAllQuery, Result<IEnumerable<Host>>>(loggerFactory)
{
    public override async Task<QueryResponse<Result<IEnumerable<Host>>>> Process(
        HostFindAllQuery query,
        CancellationToken cancellationToken)
    {
        return QueryResult.For(await repository.FindAllResultAsync(cancellationToken: cancellationToken)
            .AnyContext());
    }
}