// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Application;

using Common;
using DevKit.Application.Commands;
using DevKit.Domain.Repositories;
using Domain;
using Microsoft.Extensions.Logging;

public class CustomerCreateCommandHandler(
    ILoggerFactory loggerFactory,
    IGenericRepository<Customer> repository)
    : CommandHandlerBase<CustomerCreateCommand, Result<Customer>>(loggerFactory)
{
    public override async Task<CommandResponse<Result<Customer>>> Process(
        CustomerCreateCommand command,
        CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(command, nameof(command));

        var customer = Customer.Create(command.FirstName,
            command.LastName,
            command.Email);

        Rule.Add().Check();

        var existingCustomer = (await repository.FindAllAsync(
                new CustomerForEmailSpecification(customer.Email),
                cancellationToken: cancellationToken)
            .AnyContext()).FirstOrDefault();
        if (existingCustomer is null) // only insert new customers
        {
            await repository.InsertAsync(customer, cancellationToken);

            return new CommandResponse<Result<Customer>> { Result = Result<Customer>.Success(customer) };
        }

        return new CommandResponse<Result<Customer>> { Result = Result<Customer>.Success(existingCustomer) };
    }
}