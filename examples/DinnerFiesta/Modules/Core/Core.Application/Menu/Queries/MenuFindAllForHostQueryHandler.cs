﻿// MIT-License
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

public class MenuFindAllForHostQueryHandler(ILoggerFactory loggerFactory, IGenericRepository<Menu> repository) : QueryHandlerBase<MenuFindAllForHostQuery, Result<IEnumerable<Menu>>>(loggerFactory)
{
    public override async Task<QueryResponse<Result<IEnumerable<Menu>>>> Process(MenuFindAllForHostQuery query, CancellationToken cancellationToken)
    {
        var hostId = HostId.Create(query.HostId);

        var result = await repository.FindAllResultAsync(
            specification: new Specification<Menu>(e => e.HostId == hostId),
            cancellationToken: cancellationToken).AnyContext();

        return QueryResponse.For(result);
    }
}