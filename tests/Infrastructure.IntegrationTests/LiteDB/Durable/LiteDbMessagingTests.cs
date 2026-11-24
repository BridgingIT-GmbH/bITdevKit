// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Durable;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
public class LiteDbMessagingTests
{
    [Fact]
    public async Task PublishAndProcess_FanOutAndRetry_PreserveSuccessfulHandlerProgress()
    {
        await using var host = CreateHost();
        var sut = host.Services.GetRequiredService<LiteDbMessageBroker>();
        await sut.Subscribe<DurableLedgerMessage, SuccessfulLedgerHandler>();
        await sut.Subscribe<DurableLedgerMessage, FailingLedgerHandler>();
        var message = new DurableLedgerMessage { Text = "ledger" };
        await sut.Publish(message);
        await sut.ProcessPendingAsync();
        var row = (await sut.GetMessagesAsync(includeHandlers: true)).Single();
        row.Handlers.Single(h => h.HandlerType == typeof(SuccessfulLedgerHandler).FullName).Status.ShouldBe(BrokerMessageHandlerStatus.Succeeded);
        row.Handlers.Single(h => h.HandlerType == typeof(FailingLedgerHandler).FullName).AttemptCount.ShouldBe(1);
        host.Services.GetRequiredService<MessageDeliveryRecorder>().Successful.ShouldBe(1);
        host.Clock.Advance(TimeSpan.FromMinutes(1));
        await sut.ProcessPendingAsync();
        host.Services.GetRequiredService<MessageDeliveryRecorder>().Successful.ShouldBe(1);
        (await sut.GetMessagesAsync(includeHandlers: true)).Single().Status.ShouldBe(BrokerMessageStatus.DeadLettered);
    }

    [Fact]
    public async Task NoHandler_WaitsDurablyUntilSubscriptionAppears()
    {
        await using var host = CreateHost();
        var sut = host.Services.GetRequiredService<LiteDbMessageBroker>();
        await sut.Publish(new DurableLedgerMessage { Text = "late" });
        (await sut.GetWaitingMessagesAsync()).Count().ShouldBe(1);
        await sut.Subscribe<DurableLedgerMessage, SuccessfulLedgerHandler>();
        await sut.ProcessPendingAsync();
        (await sut.GetWaitingMessagesAsync()).ShouldBeEmpty();
        (await sut.GetMessageStatsAsync()).Succeeded.ShouldBe(1);
    }

    [Fact]
    public async Task PauseRetryArchiveAndPurge_UsePersistedOperations()
    {
        await using var host = CreateHost();
        var sut = host.Services.GetRequiredService<LiteDbMessageBroker>();
        await sut.Subscribe<DurableLedgerMessage, SuccessfulLedgerHandler>();
        await sut.Publish(new DurableLedgerMessage { Text = "paused" });
        var row = (await sut.GetMessagesAsync()).Single();
        await sut.PauseMessageTypeAsync(row.Type);
        await sut.ProcessPendingAsync();
        (await sut.GetMessageAsync(row.Id)).Status.ShouldBe(BrokerMessageStatus.Pending);
        (await sut.GetSummaryAsync()).PausedTypes.ShouldContain(row.Type);
        await sut.ResumeMessageTypeAsync(row.Type);
        await sut.ProcessPendingAsync();
        (await sut.GetMessageContentAsync(row.Id)).Content.ShouldContain("paused");
        await sut.ArchiveMessageAsync(row.Id);
        (await sut.GetMessagesAsync()).ShouldBeEmpty();
        await sut.PurgeMessagesAsync(isArchived: true);
        (await sut.GetMessageAsync(row.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task Lease_ReleasedDuringDelivery_FencesStaleCompletionAndProtectsLiveWorkFromPurge()
    {
        await using var host = new LiteDbDurableTestHost(services =>
        {
            services.AddSingleton<DurableDeliveryGate>();
            services.AddTransient<BlockingLedgerHandler>();
            services.AddMessaging().WithLiteDbBroker(options => { options.MessageExpiration = null; options.MaxDeliveryAttempts = 1; });
        });
        var sut = host.Services.GetRequiredService<LiteDbMessageBroker>();
        var gate = host.Services.GetRequiredService<DurableDeliveryGate>();
        await sut.Subscribe<DurableLedgerMessage, BlockingLedgerHandler>();
        await sut.Publish(new DurableLedgerMessage());
        var processing = sut.ProcessPendingAsync();
        try
        {
            await gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var row = (await sut.GetMessagesAsync()).Single();
            await sut.PurgeMessagesAsync();
            (await sut.GetMessageAsync(row.Id)).ShouldNotBeNull();
            await sut.ReleaseLeaseAsync(row.Id);
            var replacement = sut.ProcessPendingAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while ((await sut.GetMessageAsync(row.Id)).Status != BrokerMessageStatus.Processing && !replacement.IsCompleted)
            {
                await Task.Delay(10, timeout.Token);
            }

            gate.Release.TrySetResult();
            await Task.WhenAll(processing, replacement);
            (await sut.GetMessageAsync(row.Id)).Status.ShouldBe(BrokerMessageStatus.DeadLettered);
            gate.Count.ShouldBe(2);
        }
        finally { gate.Release.TrySetResult(); await processing; }
    }

    [Fact]
    public async Task Reopen_PendingPublication_RedeliversUsingCurrentHandlerRegistration()
    {
        var directory = Directory.CreateTempSubdirectory("bdk-messaging-reopen-");
        try
        {
            var file = Path.Combine(directory.FullName, "delivery.db");
            await using (var first = CreateHost(file))
            {
                var initial = first.Services.GetRequiredService<LiteDbMessageBroker>();
                await initial.Publish(new DurableLedgerMessage { Text = "restart" });
            }

            await using var reopened = CreateHost(file);
            var sut = reopened.Services.GetRequiredService<LiteDbMessageBroker>();
            await sut.Subscribe<DurableLedgerMessage, SuccessfulLedgerHandler>();
            await sut.ProcessPendingAsync();
            (await sut.GetMessagesAsync()).Single().Status.ShouldBe(BrokerMessageStatus.Succeeded);
        }
        finally { directory.Delete(true); }
    }

    private static LiteDbDurableTestHost CreateHost(string file = null) => new(s =>
    {
        s.AddSingleton<MessageDeliveryRecorder>();
        s.AddTransient<SuccessfulLedgerHandler>();
        s.AddTransient<FailingLedgerHandler>();
        s.AddMessaging().WithLiteDbBroker(o => { o.MessageExpiration = null; o.MaxDeliveryAttempts = 2; });
    }, file);
}

public class DurableLedgerMessage : MessageBase
{
    public DurableLedgerMessage() { }

    public DurableLedgerMessage(DateTimeOffset timestamp) => this.Timestamp = timestamp;

    public string Text { get; set; }
}

public class MessageDeliveryRecorder
{
    public int Successful { get; set; }
}

public class SuccessfulLedgerHandler(MessageDeliveryRecorder recorder) : IMessageHandler<DurableLedgerMessage>
{
    public Task Handle(DurableLedgerMessage message, CancellationToken cancellationToken)
    {
        recorder.Successful++;
        return Task.CompletedTask;
    }
}

public class FailingLedgerHandler : IMessageHandler<DurableLedgerMessage>
{
    public Task Handle(DurableLedgerMessage message, CancellationToken cancellationToken) => throw new InvalidOperationException("Retry this fictional handler.");
}

public class BlockingLedgerHandler(DurableDeliveryGate gate) : IMessageHandler<DurableLedgerMessage>
{
    public Task Handle(DurableLedgerMessage message, CancellationToken cancellationToken) => gate.DeliverAsync();
}

public class DurableDeliveryGate
{
    private int count;

    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int Count => this.count;

    public async Task DeliverAsync()
    {
        if (Interlocked.Increment(ref this.count) == 1)
        {
            this.Started.TrySetResult();
            await this.Release.Task;
            return;
        }

        throw new InvalidOperationException("The replacement owner failed.");
    }
}
