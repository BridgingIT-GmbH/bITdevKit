// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Application.Projection;

using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Commands;
using BridgingIT.DevKit.Application.Commands.EventSourcing;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using Domain.Model;
using Domain.Repositories;
using EnsureThat;
using Microsoft.Extensions.Logging;

public sealed class PersonCreatedProjectionCommandHandler :
        CommandHandlerBase<AggregateEventProjectionCommand<Person>, bool> // <1>
{
    private readonly IPersonOverviewRepository personOverviewRepository;
    private readonly IEntityMapper mapper;

    public PersonCreatedProjectionCommandHandler(ILoggerFactory loggerFactory, IPersonOverviewRepository personOverviewRepository, IEntityMapper mapper) // <2>
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(personOverviewRepository, nameof(personOverviewRepository));
        EnsureArg.IsNotNull(mapper, nameof(mapper));

        this.personOverviewRepository = personOverviewRepository;
        this.mapper = mapper;
    }

    public override async Task<CommandResponse<bool>> Process(AggregateEventProjectionCommand<Person> request, CancellationToken cancellationToken) // <3>
    {
        EnsureArg.IsNotNull(request, nameof(request));
        EnsureArg.IsNotNull(request.Aggregate, nameof(request.Aggregate));

        var aggregate = request.Aggregate;

        if (!aggregate.UserIsDeactivated)
        {
            var pov = this.mapper.Map<PersonOverview>(aggregate);
            await this.personOverviewRepository.UpsertAsync(pov, cancellationToken).AnyContext();
        }
        else
        {
            await this.personOverviewRepository.DeleteAsync(aggregate.Id, cancellationToken).AnyContext();
        }

        return new CommandResponse<bool>() { Result = true };
    }
}