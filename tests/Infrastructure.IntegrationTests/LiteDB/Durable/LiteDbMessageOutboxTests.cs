// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Durable;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
public class LiteDbMessageOutboxTests
{
    [Fact]
    public async Task Capture_EntityTransactionRollback_ForwardsNothingAndPersistsNothing()
    {
        var receiving = Substitute.For<IMessageBroker>();
        receiving.Publish(Arg.Any<IMessage>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        await using var host = new LiteDbDurableTestHost(s =>
        {
            s.AddLiteDbRepository<RepositoryRecord>();
            var messaging = s.AddMessaging();
            s.AddSingleton(receiving);
            messaging.WithLiteDbOutbox();
        });
        await using var scope = host.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGenericRepository<RepositoryRecord>>();
        var transaction = scope.ServiceProvider.GetRequiredService<IRepositoryTransaction<RepositoryRecord>>();
        var publisher = scope.ServiceProvider.GetRequiredService<IMessageBroker>();
        await Should.ThrowAsync<InvalidOperationException>(async () => await transaction.ExecuteScopedAsync(async () =>
        {
            await repository.InsertAsync(new RepositoryRecord { Name = "rollback" });
            await publisher.Publish(new DurableLedgerMessage { Text = "rollback" });
            throw new InvalidOperationException("rollback");
        }));
        (await repository.CountAsync()).ShouldBe(0);
        await host.Services.GetRequiredService<IOutboxMessageWorker>().ProcessAsync();
        await receiving.DidNotReceive().Publish(Arg.Any<IMessage>(), Arg.Any<CancellationToken>());
        var message = new DurableLedgerMessage { Text = "commit" };
        await transaction.ExecuteScopedAsync(async () =>
        {
            await repository.InsertAsync(new RepositoryRecord { Name = "committed" });
            await publisher.Publish(message);
        });
        await host.Services.GetRequiredService<IOutboxMessageWorker>().ProcessAsync(message.MessageId);
        await receiving.Received(1).Publish(Arg.Is<IMessage>(m => m.MessageId == message.MessageId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExactMessageSelection_LeavesOtherCommittedPublicationsPending()
    {
        var receiving = Substitute.For<IMessageBroker>();
        receiving.Publish(Arg.Any<IMessage>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        await using var host = new LiteDbDurableTestHost(s =>
        {
            var messaging = s.AddMessaging();
            s.AddSingleton(receiving);
            messaging.WithLiteDbOutbox();
        });
        await using var scope = host.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IMessageBroker>();
        var first = new DurableLedgerMessage { Text = "first" };
        var second = new DurableLedgerMessage { Text = "second" };
        await publisher.Publish(first);
        await publisher.Publish(first);
        await publisher.Publish(second);
        await host.Services.GetRequiredService<IOutboxMessageWorker>().ProcessAsync(first.MessageId);
        await receiving.Received(1).Publish(Arg.Is<IMessage>(m => m.MessageId == first.MessageId), Arg.Any<CancellationToken>());
        await receiving.DidNotReceive().Publish(Arg.Is<IMessage>(m => m.MessageId == second.MessageId), Arg.Any<CancellationToken>());
    }
}
