// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Application;

using Common;
using DevKit.Application.Commands;
using DevKit.Domain;
using DevKit.Domain.Repositories;
using Domain;
using Microsoft.Extensions.Logging;

public class HostUpdateCommandHandler(
    ILoggerFactory loggerFactory,
    IGenericRepository<Host> repository)
    : CommandHandlerBase<HostUpdateCommand, Result<Host>>(loggerFactory)
{
    public override async Task<CommandResponse<Result<Host>>> Process(HostUpdateCommand command, CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(command, nameof(command));

        var hostResult = await repository.FindOneResultAsync(HostId.Create(command.Id), cancellationToken: cancellationToken);
        var host = hostResult.Value;
        if (hostResult.IsFailure)
        {
            return CommandResult.For(hostResult);
        }

        this.Logger.LogInformation("entity retrieved");

        await DomainRules.ApplyAsync([ /* add some rules*/], cancellationToken);

        host.ChangeName(command.FirstName, command.LastName)
            .ChangeProfileImage(command.ImageUrl is not null ? new Uri(command.ImageUrl) : null);

        await repository.UpdateAsync(host, cancellationToken);

        return CommandResult.Success(host); // was: CommandResponse.Success(host);
    }

    public async Task<CommandResponse<Result<Host>>> ProcessResult(HostUpdateCommand command, CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(command, nameof(command));

        return CommandResult.For(await
            (await repository.FindOneResultAsync(HostId.Create(command.Id), cancellationToken: cancellationToken))
            .Ensure(e => e != null, new EntityNotFoundError())
            .Tap(_ => this.Logger.LogInformation("entity retrieved"))
            .AndThen(e => DomainRules.Apply([ /* add some rules*/]))
            .AndThenAsync(async (e,ct) => await DomainRules.ApplyAsync([ /* add some rules*/], ct), cancellationToken)
            .BindAsync(async (e, ct) =>
                {
                   //await DomainRules.ApplyAsync([ /* add some rules*/], ct);

                    e // update the domain entity
                        .ChangeName(command.FirstName, command.LastName)
                        .ChangeProfileImage(command.ImageUrl is not null ? new Uri(command.ImageUrl) : null);

                    return await repository.UpdateResultAsync(e, ct); // update and return the entity
                },
                cancellationToken)
            .Ensure(e => e.Id != null, new EntityNotFoundError()));
    }
}