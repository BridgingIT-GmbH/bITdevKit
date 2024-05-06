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

public class HostFindAllQueryHandler : QueryHandlerBase<HostFindAllQuery, Result<IEnumerable<Host>>>
{
    private readonly IGenericRepository<Host> repository;

    public HostFindAllQueryHandler(ILoggerFactory loggerFactory, IGenericRepository<Host> repository)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(repository, nameof(repository));

        this.repository = repository;
    }

    public override async Task<QueryResponse<Result<IEnumerable<Host>>>> Process(HostFindAllQuery query, CancellationToken cancellationToken)
    {
        return QueryResponse.For(
            await this.repository.FindAllResultAsync(cancellationToken: cancellationToken).AnyContext());
    }
}