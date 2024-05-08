// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Queries;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Domain.Specifications;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using Microsoft.Extensions.Logging;

public class DinnerFindOneForHostQueryHandler : QueryHandlerBase<DinnerFindOneForHostQuery, Result<Dinner>>
{
    private readonly IGenericRepository<Dinner> repository;

    public DinnerFindOneForHostQueryHandler(ILoggerFactory loggerFactory, IGenericRepository<Dinner> repository)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(repository, nameof(repository));

        this.repository = repository;
    }

    public override async Task<QueryResponse<Result<Dinner>>> Process(DinnerFindOneForHostQuery query, CancellationToken cancellationToken)
    {
        var hostId = HostId.Create(query.HostId);
        var dinnerId = HostId.Create(query.DinnerId);

        return QueryResponse.For(
            await this.repository.FindOneResultAsync(
                specification: new Specification<Dinner>(e => e.HostId == hostId && e.Id == dinnerId),
                cancellationToken: cancellationToken).AnyContext());
    }
}