// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Application;

using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Queries;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Domain;
using Microsoft.Extensions.Logging;

public class CustomerFindOneQueryHandler(ILoggerFactory loggerFactory, IGenericRepository<Customer> repository) : QueryHandlerBase<CustomerFindOneQuery, Result<Customer>>(loggerFactory)
{
    public override async Task<QueryResponse<Result<Customer>>> Process(CustomerFindOneQuery query, CancellationToken cancellationToken)
    {
        var customerId = CustomerId.Create(query.CustomerId);
        var customer = await repository.FindOneAsync(customerId, cancellationToken: cancellationToken).AnyContext();

        if (customer is null)
        {
            return QueryResponse.Fail<Customer, NotFoundResultError>();
        }

        return QueryResponse.Success(customer);
    }
}