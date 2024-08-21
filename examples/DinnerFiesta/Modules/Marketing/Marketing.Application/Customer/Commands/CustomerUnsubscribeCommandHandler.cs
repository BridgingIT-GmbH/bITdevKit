// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Application;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Commands;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Domain;
using Microsoft.Extensions.Logging;

public class CustomerUnsubscribeCommandHandler(
    ILoggerFactory loggerFactory,
    IGenericRepository<Customer> repository) : CommandHandlerBase<CustomerUnsubscribeCommand, Result>(loggerFactory)
{
    public override async Task<CommandResponse<Result>> Process(CustomerUnsubscribeCommand command, CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(command, nameof(command));

        DomainRules.Apply(Array.Empty<IDomainRule>());

        var customer = await repository.FindOneAsync(CustomerId.Create(command.CustomerId), cancellationToken: cancellationToken).AnyContext();
        if (customer is not null)
        {
            customer.Unsubscribe();
            await repository.UpsertAsync(customer, cancellationToken);

            return new CommandResponse<Result>
            {
                Result = Result.Success()
            };
        }
        else
        {
            return new CommandResponse<Result>
            {
                Result = Result.Failure<NotFoundResultError>()
            };
        }
    }
}