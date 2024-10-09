// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.EventSourcing.Outbox;

using AggregatePublish;
using Common;
using Common.Options;
using Domain.Outbox;
using Domain.Repositories;
using Microsoft.Extensions.Logging;
using BridgingIT.DevKit.Domain;

public class OutboxWorkerService(
    IOutboxMessageWorkerRepository repository,
    IAggregateEventOutboxReceiver receiver,
    ILoggerOptions loggerOptions) : IOutboxWorkerService
{
    private readonly IOutboxMessageWorkerRepository repository = repository;
    private readonly IAggregateEventOutboxReceiver receiver = receiver;
    private readonly ILogger<OutboxWorkerService> logger = loggerOptions.CreateLogger<OutboxWorkerService>();

    public async Task DoWorkAsync()
    {
        foreach (var message in await this.repository.FindAllAsync(
                         new Specification<OutboxMessage>(m => !m.IsProcessed),
                         new FindOptions<OutboxMessage>(0, 0, new OrderOption<OutboxMessage>(o => o.TimeStamp)))
                     .AnyContext())
        {
            try
            {
                var result = await this.receiver.ReceiveAndPublishAsync(message).AnyContext();
                if (result.projectionSended && result.eventOccuredNotified && result.eventOccuredSended)
                {
                    message.IsProcessed = true;
                }
                else
                {
                    message.RetryAttempt++;
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"Error processing message {message.Id}. The error was: {ex.Message}");
                message.RetryAttempt++;
            }

            await this.repository.UpdateAsync(message).AnyContext();
        }
    }
}