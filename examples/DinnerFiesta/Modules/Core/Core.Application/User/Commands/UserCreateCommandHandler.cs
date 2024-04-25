// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Commands;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Domain;
using Microsoft.Extensions.Logging;

public class UserCreateCommandHandler : CommandHandlerBase<UserCreateCommand, Result<User>>
{
    private readonly IGenericRepository<User> repository;

    public UserCreateCommandHandler(ILoggerFactory loggerFactory, IGenericRepository<User> repository)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(repository, nameof(repository));

        this.repository = repository;
    }

    public override async Task<CommandResponse<Result<User>>> Process(UserCreateCommand command, CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(command, nameof(command));

        var user = User.Create(
            command.FirstName,
            command.LastName,
            command.Email,
            command.Password);

        Check.Throw(new IBusinessRule[]
        {
            new UserEmailMustBeUniqueRule(this.repository, user),
        });

        await this.repository.InsertAsync(user, cancellationToken);

        return new CommandResponse<Result<User>>
        {
            Result = Result<User>.Success(user)
        };
    }
}