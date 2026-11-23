// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Durable;

using System.Text.Json;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Domain.Repositories;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
public class LiteDbDeliveryFailureRegressionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PayloadDeserializationFailure_ShouldStopAtDeliveryLimit(bool queueing)
    {
        await using var host = new LiteDbDurableTestHost(services =>
        {
            services.AddSingleton<MessageDeliveryRecorder>();
            services.AddSingleton<QueueDeliveryRecorder>();
            services.AddTransient<SuccessfulLedgerHandler>();
            services.AddTransient<DurableQueueHandler>();
            services.AddMessaging().WithLiteDbBroker(o => { o.MessageExpiration = null; o.MaxDeliveryAttempts = 1; o.Serializer = new UnreadableMessageSerializer(); });
            services.AddQueueing().WithLiteDbBroker(o => { o.MessageExpiration = null; o.MaxDeliveryAttempts = 1; o.Serializer = new UnreadableMessageSerializer(); });
        });
        if (queueing)
        {
            var sut = host.Services.GetRequiredService<LiteDbQueueBroker>();
            await sut.Subscribe<DurableQueueMessage, DurableQueueHandler>();
            await sut.Enqueue(new DurableQueueMessage { Timestamp = host.Clock.UtcNow });
            for (var i = 0; i < 3; i++)
            {
                await sut.ProcessPendingAsync();
                if (i == 0) { (await sut.GetMessagesAsync()).Single().Status.ShouldBe(QueueMessageStatus.DeadLettered); }

                host.Clock.Advance(TimeSpan.FromMinutes(1));
            }

            var row = (await sut.GetMessagesAsync()).Single();
            row.Status.ShouldBe(QueueMessageStatus.DeadLettered, $"Attempt count: {row.AttemptCount}; configured limit: 1");
        }
        else
        {
            var sut = host.Services.GetRequiredService<LiteDbMessageBroker>();
            await sut.Subscribe<DurableLedgerMessage, SuccessfulLedgerHandler>();
            await sut.Publish(new DurableLedgerMessage());
            for (var i = 0; i < 3; i++)
            {
                await sut.ProcessPendingAsync();
                if (i == 0) { (await sut.GetMessagesAsync()).Single().Status.ShouldBe(BrokerMessageStatus.DeadLettered); }

                host.Clock.Advance(TimeSpan.FromMinutes(1));
            }

            var row = (await sut.GetMessagesAsync()).Single();
            row.Status.ShouldBe(BrokerMessageStatus.DeadLettered, $"Attempt count: {row.AttemptCountSummary}; configured limit: 1");
        }
    }

    [Fact]
    public async Task OutgoingOutbox_ShouldApplyPublisherGuardBeforeCommittingEntityTransaction()
    {
        await using var host = new LiteDbDurableTestHost(services =>
        {
            services.AddLiteDbRepository<RepositoryRecord>();
            services.AddSingleton<IMessagePublisherBehavior, RejectingPublisherBehavior>();
            services.AddMessaging().WithLiteDbBroker(o => o.MessageExpiration = null).WithLiteDbOutbox();
        });
        var receiving = host.Services.GetRequiredService<LiteDbMessageBroker>();
        await Should.ThrowAsync<InvalidOperationException>(() => receiving.Publish(new DurableLedgerMessage()));
        await using var scope = host.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IMessageBroker>();
        var repository = scope.ServiceProvider.GetRequiredService<IGenericRepository<RepositoryRecord>>();
        var transaction = scope.ServiceProvider.GetRequiredService<IRepositoryTransaction<RepositoryRecord>>();
        var rejected = false;
        try
        {
            await transaction.ExecuteScopedAsync(async () =>
            {
                await repository.InsertAsync(new RepositoryRecord { Name = "must roll back when publication is rejected" });
                await publisher.Publish(new DurableLedgerMessage());
            });
        }
        catch (InvalidOperationException) { rejected = true; }

        rejected.ShouldBeTrue($"Repository count after rejected publication: {await repository.CountAsync()}");
        (await repository.CountAsync()).ShouldBe(0);
        var outbox = host.Services.GetRequiredService<LiteDbOutboxMessageWorker>();
        await outbox.ProcessAsync();
        (await outbox.Broker.GetMessagesAsync()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransientPayloadFailure_HitsPersistedLimitAndCanBeRetriedAfterRepair(bool queueing)
    {
        var serializer = new FaultingMessageSerializer { FailuresRemaining = 5 };
        await using var host = CreateHost(serializer);
        if (queueing)
        {
            var sut = host.Services.GetRequiredService<LiteDbQueueBroker>();
            await sut.Subscribe<DurableQueueMessage, DurableQueueHandler>();
            await sut.Enqueue(new DurableQueueMessage { Timestamp = host.Clock.UtcNow });
            for (var i = 0; i < 3; i++)
            {
                await sut.ProcessPendingAsync();
                host.Clock.Advance(TimeSpan.FromMinutes(1));
            }

            var row = (await sut.GetMessagesAsync()).Single();
            row.Status.ShouldBe(QueueMessageStatus.DeadLettered);
            serializer.Attempts.ShouldBe(2);
            serializer.FailuresRemaining = 0;
            await sut.RetryMessageAsync(row.Id);
            await sut.ProcessPendingAsync();
            (await sut.GetMessageAsync(row.Id)).Status.ShouldBe(QueueMessageStatus.Succeeded);
            host.Services.GetRequiredService<QueueDeliveryRecorder>().Count.ShouldBe(1);
        }
        else
        {
            var sut = host.Services.GetRequiredService<LiteDbMessageBroker>();
            await sut.Subscribe<DurableLedgerMessage, SuccessfulLedgerHandler>();
            await sut.Publish(new DurableLedgerMessage());
            for (var i = 0; i < 3; i++)
            {
                await sut.ProcessPendingAsync();
                host.Clock.Advance(TimeSpan.FromMinutes(1));
            }

            var row = (await sut.GetMessagesAsync()).Single();
            row.Status.ShouldBe(BrokerMessageStatus.DeadLettered);
            serializer.Attempts.ShouldBe(2);
            serializer.FailuresRemaining = 0;
            await sut.RetryMessageAsync(row.Id);
            await sut.ProcessPendingAsync();
            (await sut.GetMessageAsync(row.Id)).Status.ShouldBe(BrokerMessageStatus.Succeeded);
            host.Services.GetRequiredService<MessageDeliveryRecorder>().Successful.ShouldBe(1);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reopen_PreservesProcessingFailureBudget(bool queueing)
    {
        var directory = Directory.CreateTempSubdirectory("bdk-delivery-failure-reopen-");
        try
        {
            var file = Path.Combine(directory.FullName, "delivery.db");
            var serializer = new FaultingMessageSerializer { FailuresRemaining = 5 };
            await using (var first = CreateHost(serializer, file))
            {
                if (queueing)
                {
                    var broker = first.Services.GetRequiredService<LiteDbQueueBroker>();
                    await broker.Subscribe<DurableQueueMessage, DurableQueueHandler>();
                    await broker.Enqueue(new DurableQueueMessage { Timestamp = first.Clock.UtcNow });
                    await broker.ProcessPendingAsync();
                }
                else
                {
                    var broker = first.Services.GetRequiredService<LiteDbMessageBroker>();
                    await broker.Subscribe<DurableLedgerMessage, SuccessfulLedgerHandler>();
                    await broker.Publish(new DurableLedgerMessage());
                    await broker.ProcessPendingAsync();
                }
            }

            await using var reopened = CreateHost(serializer, file);
            reopened.Clock.Advance(TimeSpan.FromMinutes(1));
            if (queueing)
            {
                var sut = reopened.Services.GetRequiredService<LiteDbQueueBroker>();
                await sut.Subscribe<DurableQueueMessage, DurableQueueHandler>();
                await sut.ProcessPendingAsync();
                (await sut.GetMessagesAsync()).Single().Status.ShouldBe(QueueMessageStatus.DeadLettered);
            }
            else
            {
                var sut = reopened.Services.GetRequiredService<LiteDbMessageBroker>();
                await sut.Subscribe<DurableLedgerMessage, SuccessfulLedgerHandler>();
                await sut.ProcessPendingAsync();
                (await sut.GetMessagesAsync()).Single().Status.ShouldBe(BrokerMessageStatus.DeadLettered);
            }

            serializer.Attempts.ShouldBe(2);
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public async Task PublisherEnrichment_IsCapturedBeforeCommitAndRunsAgainAtDownstreamPublication()
    {
        var behavior = new EnrichingPublisherBehavior();
        await using var host = new LiteDbDurableTestHost(services =>
        {
            services.AddSingleton<IMessagePublisherBehavior>(behavior);
            services.AddMessaging().WithLiteDbBroker().WithLiteDbOutbox();
        });
        await using var scope = host.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IMessageBroker>();
        var message = new DurableLedgerMessage();
        await publisher.Publish(message);
        behavior.Calls.ShouldBe(1);
        var worker = host.Services.GetRequiredService<LiteDbOutboxMessageWorker>();
        var row = (await worker.Broker.GetMessagesAsync()).Single();
        (await worker.Broker.GetMessageContentAsync(row.Id)).Content.ShouldContain("captured enrichment");
        await worker.ProcessAsync(message.MessageId);
        behavior.Calls.ShouldBe(2);
        var receiving = host.Services.GetRequiredService<LiteDbMessageBroker>();
        (await receiving.GetMessagesAsync()).Single().Properties["PublisherStamp"].ShouldBe("captured enrichment");
    }

    [Fact]
    public async Task OutboxOptions_PublisherGuard_RunsBeforeCapture()
    {
        await using var host = new LiteDbDurableTestHost(services =>
            services.AddMessaging().WithLiteDbBroker().WithLiteDbOutbox(options => options.PublisherBehaviors = [new RejectingPublisherBehavior()]));
        await using var scope = host.Services.CreateAsyncScope();
        var sut = scope.ServiceProvider.GetRequiredService<IMessageBroker>();
        await Should.ThrowAsync<InvalidOperationException>(() => sut.Publish(new DurableLedgerMessage()));
        (await host.Services.GetRequiredService<LiteDbOutboxMessageWorker>().Broker.GetMessagesAsync()).ShouldBeEmpty();
    }

    private static LiteDbDurableTestHost CreateHost(ISerializer serializer, string file = null) => new(services =>
    {
        services.AddSingleton<MessageDeliveryRecorder>();
        services.AddSingleton<QueueDeliveryRecorder>();
        services.AddTransient<SuccessfulLedgerHandler>();
        services.AddTransient<DurableQueueHandler>();
        services.AddMessaging().WithLiteDbBroker(options => { options.MessageExpiration = null; options.MaxDeliveryAttempts = 2; options.Serializer = serializer; });
        services.AddQueueing().WithLiteDbBroker(options => { options.MessageExpiration = null; options.MaxDeliveryAttempts = 2; options.Serializer = serializer; });
    }, file);

}

public class UnreadableMessageSerializer : ISerializer
{
    private readonly ISerializer inner = new SystemTextJsonSerializer();

    public void Serialize(object value, Stream output) => this.inner.Serialize(value, output);

    public object Deserialize(Stream input, Type type) => throw new JsonException("The selected serializer cannot reconstruct this payload.");

    public T Deserialize<T>(Stream input) => this.inner.Deserialize<T>(input);
}

public class RejectingPublisherBehavior : IMessagePublisherBehavior
{
    public Task Publish<TMessage>(TMessage message, CancellationToken cancellationToken, MessagePublisherDelegate next) where TMessage : IMessage =>
        throw new InvalidOperationException("Publisher guard rejected the message.");
}

public class FaultingMessageSerializer : ISerializer
{
    private readonly ISerializer inner = new SystemTextJsonSerializer();

    public int FailuresRemaining { get; set; }

    public int Attempts { get; private set; }

    public void Serialize(object value, Stream output) => this.inner.Serialize(value, output);

    public object Deserialize(Stream input, Type type)
    {
        this.Attempts++;
        if (this.FailuresRemaining-- > 0) { throw new TimeoutException("Temporary payload reader failure."); }

        return this.inner.Deserialize(input, type);
    }

    public T Deserialize<T>(Stream input) => this.inner.Deserialize<T>(input);
}

public class EnrichingPublisherBehavior : IMessagePublisherBehavior
{
    public int Calls { get; private set; }

    public Task Publish<TMessage>(TMessage message, CancellationToken cancellationToken, MessagePublisherDelegate next) where TMessage : IMessage
    {
        this.Calls++;
        message.Properties["PublisherStamp"] = "captured enrichment";
        return next();
    }
}
