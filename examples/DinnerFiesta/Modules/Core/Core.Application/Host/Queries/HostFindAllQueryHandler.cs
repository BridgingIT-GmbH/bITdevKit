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

public class HostFindAllQueryHandler(ILoggerFactory loggerFactory, IGenericRepository<Host> repository) : QueryHandlerBase<HostFindAllQuery, Result<IEnumerable<Host>>>(loggerFactory)
{
    public override async Task<QueryResponse<Result<IEnumerable<Host>>>> Process(HostFindAllQuery query, CancellationToken cancellationToken)
    {
        return QueryResponse.Create(
            await repository.FindAllResultAsync(cancellationToken: cancellationToken).AnyContext());
    }
}