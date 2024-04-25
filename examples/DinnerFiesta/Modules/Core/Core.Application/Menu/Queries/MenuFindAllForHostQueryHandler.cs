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
using BridgingIT.DevKit.Domain.Specifications;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using Microsoft.Extensions.Logging;

public class MenuFindAllForHostQueryHandler : QueryHandlerBase<MenuFindAllForHostQuery, Result<IEnumerable<Menu>>>
{
    private readonly IGenericRepository<Menu> repository;

    public MenuFindAllForHostQueryHandler(ILoggerFactory loggerFactory, IGenericRepository<Menu> repository)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(repository, nameof(repository));

        this.repository = repository;
    }

    public override async Task<QueryResponse<Result<IEnumerable<Menu>>>> Process(MenuFindAllForHostQuery query, CancellationToken cancellationToken)
    {
        var hostId = HostId.Create(query.HostId);

        return QueryResponse.For(await this.repository.FindAllResultAsync(
            specification: new Specification<Menu>(e => e.HostId == hostId),
            cancellationToken: cancellationToken).AnyContext());
    }
}