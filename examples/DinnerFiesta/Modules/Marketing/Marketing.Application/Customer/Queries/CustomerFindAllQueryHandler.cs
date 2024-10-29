// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Application;

using Common;
using DevKit.Application.Queries;
using DevKit.Domain.Repositories;
using Domain;
using Microsoft.Extensions.Logging;

public class CustomerFindAllQueryHandler(ILoggerFactory loggerFactory, IGenericRepository<Customer> repository)
    : QueryHandlerBase<CustomerFindAllQuery, Result<IEnumerable<Customer>>>(loggerFactory)
{
    public override async Task<QueryResponse<Result<IEnumerable<Customer>>>> Process(
        CustomerFindAllQuery query,
        CancellationToken cancellationToken)
    {
        return QueryResult.For(await repository.FindAllResultAsync(cancellationToken: cancellationToken)
            .AnyContext());
    }
}