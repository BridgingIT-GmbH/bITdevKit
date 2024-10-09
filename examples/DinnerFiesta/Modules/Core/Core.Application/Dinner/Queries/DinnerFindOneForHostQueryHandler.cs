// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using Common;
using DevKit.Application.Queries;
using DevKit.Domain.Repositories;
using BridgingIT.DevKit.Domain;
using Domain;
using Microsoft.Extensions.Logging;

public class DinnerFindOneForHostQueryHandler(ILoggerFactory loggerFactory, IGenericRepository<Dinner> repository)
    : QueryHandlerBase<DinnerFindOneForHostQuery, Result<Dinner>>(loggerFactory)
{
    public override async Task<QueryResponse<Result<Dinner>>> Process(
        DinnerFindOneForHostQuery query,
        CancellationToken cancellationToken)
    {
        var hostId = HostId.Create(query.HostId);
        var dinnerId = HostId.Create(query.DinnerId);

        return QueryResponse.For(await repository.FindOneResultAsync(
                new Specification<Dinner>(e => e.HostId == hostId && e.Id == dinnerId),
                cancellationToken: cancellationToken)
            .AnyContext());
    }
}