// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

public class HostCreateCommandHandler(
    ILoggerFactory loggerFactory,
    IGenericRepository<Host> repository) : CommandHandlerBase<HostCreateCommand, Result<Host>>(loggerFactory)
{
    public override async Task<CommandResponse<Result<Host>>> Process(
        HostCreateCommand command,
        CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(command, nameof(command));

        var host = Host.Create(command.FirstName,
            command.LastName,
            UserId.Create(command.UserId),
            command.ImageUrl is not null ? new Uri(command.ImageUrl) : null);

        await Rule.Add(
                HostRules.UserMustBeUnique(repository, host.UserId))
            .CheckAsync(cancellationToken: cancellationToken);

        await repository.InsertAsync(host, cancellationToken);

        return CommandResult.Success(host);
    }
}