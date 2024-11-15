// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

public class DinnerFindAllForHostQueryHandler(ILoggerFactory loggerFactory, IGenericRepository<Dinner> repository)
    : QueryHandlerBase<DinnerFindAllForHostQuery, Result<IEnumerable<Dinner>>>(loggerFactory)
{
    public override async Task<QueryResponse<Result<IEnumerable<Dinner>>>> Process(
        DinnerFindAllForHostQuery query,
        CancellationToken cancellationToken)
    {
        return QueryResult.For(await repository.FindAllResultAsync(
                DinnerSpecifications.ForHost(HostId.Create(query.HostId)),
                cancellationToken: cancellationToken)
            .AnyContext());
    }
}