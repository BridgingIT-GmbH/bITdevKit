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

public class HostCreateCommandHandler : CommandHandlerBase<HostCreateCommand, Result<Host>>
{
    private readonly IGenericRepository<Host> repository;

    public HostCreateCommandHandler(
        ILoggerFactory loggerFactory,
        IGenericRepository<Host> repository)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(repository, nameof(repository));

        this.repository = repository;
    }

    public override async Task<CommandResponse<Result<Host>>> Process(HostCreateCommand command, CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(command, nameof(command));

        var host = Host.Create(
            command.FirstName,
            command.LastName,
            UserId.Create(command.UserId),
            command.ImageUrl is not null ? new Uri(command.ImageUrl) : null);

        Check.Throw(new IBusinessRule[]
        {
            new HostUserMustBeUniqueRule(this.repository, host.UserId),
        });

        await this.repository.InsertAsync(host, cancellationToken);

        return CommandResponse.Success(host);
    }
}
