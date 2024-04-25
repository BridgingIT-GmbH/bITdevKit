// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Commands.Outbox;

using System;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Commands;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Outbox;
using EnsureThat;
using Microsoft.Extensions.Logging;

public class OutboxMessageCommandHandler : CommandHandlerBase<OutboxMessageCommand, OutboxMessageCommandResult>
{
    private readonly IOutboxMessageWriterRepository repository;

    public OutboxMessageCommandHandler(
        ILoggerFactory loggerFactory,
        IOutboxMessageWriterRepository repository)
        : base(loggerFactory)
    {
        EnsureArg.IsNotNull(repository, nameof(repository));

        this.repository = repository;
    }

    public override async Task<CommandResponse<OutboxMessageCommandResult>> Process(OutboxMessageCommand request, CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(request, nameof(request));

        if (await this.repository.FindOneAsync(
            new OutboxMessageMessageIdSpecification(request.MessageId), cancellationToken: cancellationToken).AnyContext() is not null)
        {
            return new CommandResponse<OutboxMessageCommandResult>("Already in outbox")
            {
                Result = new OutboxMessageCommandResult(OutboxMessageCommandResultErrorCodes.DuplicatedMessage)
            };
        }

        await this.repository.InsertAsync(
            new OutboxMessage()
            {
                MessageId = request.MessageId,
                AggregateId = request.AggregateId,
                Aggregate = request.Aggregate,
                AggregateEvent = request.AggregateEvent,
                AggregateType = request.AggregateType,
                EventType = request.EventType,
                IsProcessed = false,
                RetryAttempt = 0,
                TimeStamp = DateTime.Now
            }, cancellationToken).AnyContext();

        return new CommandResponse<OutboxMessageCommandResult>();
    }
}