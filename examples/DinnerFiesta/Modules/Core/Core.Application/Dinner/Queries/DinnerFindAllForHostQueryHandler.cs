// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using System.Collections.Generic;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Queries;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using Microsoft.Extensions.Logging;

public class DinnerFindAllForHostQueryHandler : QueryHandlerBase<DinnerFindAllForHostQuery, Result<IEnumerable<Dinner>>>
{
    private readonly IGenericRepository<Dinner> repository;

    public DinnerFindAllForHostQueryHandler(ILoggerFactory loggerFactory, IGenericRepository<Dinner> repository)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(repository, nameof(repository));

        this.repository = repository;
    }

    public override async Task<QueryResponse<Result<IEnumerable<Dinner>>>> Process(DinnerFindAllForHostQuery query, CancellationToken cancellationToken)
    {
        return QueryResponse.For(
            await this.repository.FindAllResultAsync(
                specification: new DinnerForHostSpecification(HostId.Create(query.HostId)),
                cancellationToken: cancellationToken).AnyContext());
    }
}