// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.EventSourcingDemo.Application.Handlers;

using System.Diagnostics;
using DevKit.Application.Commands;
using DevKit.Application.Commands.EventSourcing;
using DevKit.Domain.Repositories;
using Domain.Model;
using Domain.Repositories;
using Microsoft.Extensions.Logging;

public sealed class
    PersonEventOccuredCommandHandler : CommandHandlerBase<AggregateEventOccuredCommand<Person>, bool> // <1>
{
    private readonly IPersonOverviewRepository personOverviewRepository;
    private readonly IEntityMapper mapper;

    public PersonEventOccuredCommandHandler(
        ILoggerFactory loggerFactory,
        IPersonOverviewRepository personOverviewRepository,
        IEntityMapper mapper)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(personOverviewRepository, nameof(personOverviewRepository));
        EnsureArg.IsNotNull(mapper, nameof(mapper));

        this.personOverviewRepository = personOverviewRepository;
        this.mapper = mapper;
    }

    public override Task<CommandResponse<bool>> Process(
        AggregateEventOccuredCommand<Person> request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Task.FromResult(new CommandResponse<bool> { Result = false });
        }

        Debug.WriteLine("Do Event propagation for " + request.Aggregate.Id);

        return Task.FromResult(new CommandResponse<bool> { Result = true });
    }
}