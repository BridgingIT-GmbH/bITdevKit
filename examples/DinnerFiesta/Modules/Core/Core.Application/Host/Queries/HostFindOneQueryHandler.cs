// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Queries;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using Microsoft.Extensions.Logging;

public class HostFindOneQueryHandler(ILoggerFactory loggerFactory, IGenericRepository<Host> repository) : QueryHandlerBase<HostFindOneQuery, Result<Host>>(loggerFactory)
{
    public override async Task<QueryResponse<Result<Host>>> Process(HostFindOneQuery query, CancellationToken cancellationToken)
    {
        return QueryResponse.For(
            await repository.FindOneResultAsync(
                HostId.Create(query.HostId),
                cancellationToken: cancellationToken).AnyContext());
    }
}