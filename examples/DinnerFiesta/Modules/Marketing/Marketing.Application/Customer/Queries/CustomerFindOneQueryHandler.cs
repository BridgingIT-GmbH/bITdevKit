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

public class CustomerFindOneQueryHandler(ILoggerFactory loggerFactory, IGenericRepository<Customer> repository)
    : QueryHandlerBase<CustomerFindOneQuery, Result<Customer>>(loggerFactory)
{
    public override async Task<QueryResponse<Result<Customer>>> Process(
        CustomerFindOneQuery query,
        CancellationToken cancellationToken)
    {
        var customerId = CustomerId.Create(query.CustomerId);
        var customer = await repository.FindOneAsync(customerId, cancellationToken: cancellationToken).AnyContext();

        if (customer is null)
        {
            return QueryResult.Failure<Customer, NotFoundError>();
        }

        return QueryResult.Success(customer);
    }
}