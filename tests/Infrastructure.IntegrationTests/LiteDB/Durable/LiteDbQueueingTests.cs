// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Durable;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
public class LiteDbQueueingTests
{
    [Fact]
    public async Task CompetingConsumers_ClaimAndComplete_ExecuteOneHandler()
    {
        await using var host = CreateHost();
        var sut = host.Services.GetRequiredService<LiteDbQueueBroker>();
        await sut.Subscribe<DurableQueueMessage, DurableQueueHandler>();
        await sut.Enqueue(new DurableQueueMessage { Timestamp = host.Clock.UtcNow });
        await Task.WhenAll(sut.ProcessPendingAsync(), sut.ProcessPendingAsync());
        host.Services.GetRequiredService<QueueDeliveryRecorder>().Count.ShouldBe(1);
        var row = (await sut.GetMessagesAsync()).Single();
        row.Status.ShouldBe(QueueMessageStatus.Succeeded);
        row.AttemptCount.ShouldBe(1);
        (await sut.GetSummaryAsync()).Capabilities.SupportsLeaseManagement.ShouldBeTrue();
    }

    [Fact]
    public async Task WaitingDelayAndPause_PreserveEligibilityUntilAllowed()
    {
        await using var host = CreateHost();
        var sut = host.Services.GetRequiredService<LiteDbQueueBroker>();
        await sut.Enqueue(new DurableQueueMessage { Timestamp = host.Clock.UtcNow.AddMinutes(1) });
        (await sut.GetWaitingMessagesAsync()).Count().ShouldBe(1);
        await sut.Subscribe<DurableQueueMessage, DurableQueueHandler>();
        await sut.ProcessPendingAsync();
        host.Services.GetRequiredService<QueueDeliveryRecorder>().Count.ShouldBe(0);
        var row = (await sut.GetMessagesAsync()).Single();
        await sut.PauseQueueAsync(row.QueueName);
        host.Clock.Advance(TimeSpan.FromMinutes(2));
        await sut.ProcessPendingAsync();
        host.Services.GetRequiredService<QueueDeliveryRecorder>().Count.ShouldBe(0);
        await sut.ResumeQueueAsync(row.QueueName);
        await sut.ProcessPendingAsync();
        host.Services.GetRequiredService<QueueDeliveryRecorder>().Count.ShouldBe(1);
    }

    [Fact]
    public async Task HandlerFailure_RetryAndArchive_PreserveDurableAttempts()
    {
        await using var host = CreateHost();
        var sut = host.Services.GetRequiredService<LiteDbQueueBroker>();
        var recorder = host.Services.GetRequiredService<QueueDeliveryRecorder>();
        recorder.Fail = true;
        await sut.Subscribe<DurableQueueMessage, DurableQueueHandler>();
        await sut.Enqueue(new DurableQueueMessage { Timestamp = host.Clock.UtcNow });
        await sut.ProcessPendingAsync();
        var row = (await sut.GetMessagesAsync()).Single();
        row.Status.ShouldBe(QueueMessageStatus.Failed);
        recorder.Fail = false;
        await sut.RetryMessageAsync(row.Id);
        await sut.ProcessPendingAsync();
        (await sut.GetMessageAsync(row.Id)).Status.ShouldBe(QueueMessageStatus.Succeeded);
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
            services.AddTransient<BlockingQueueHandler>();
            services.AddQueueing().WithLiteDbBroker(options => { options.MessageExpiration = null; options.MaxDeliveryAttempts = 1; });
        });
        var sut = host.Services.GetRequiredService<LiteDbQueueBroker>();
        var gate = host.Services.GetRequiredService<DurableDeliveryGate>();
        await sut.Subscribe<DurableQueueMessage, BlockingQueueHandler>();
        await sut.Enqueue(new DurableQueueMessage { Timestamp = host.Clock.UtcNow });
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
            while ((await sut.GetMessageAsync(row.Id)).Status != QueueMessageStatus.Processing && !replacement.IsCompleted)
            {
                await Task.Delay(10, timeout.Token);
            }

            gate.Release.TrySetResult();
            await Task.WhenAll(processing, replacement);
            (await sut.GetMessageAsync(row.Id)).Status.ShouldBe(QueueMessageStatus.DeadLettered);
            gate.Count.ShouldBe(2);
        }
        finally { gate.Release.TrySetResult(); await processing; }
    }

    [Fact]
    public async Task Reopen_PendingPublication_RedeliversUsingCurrentHandlerRegistration()
    {
        var directory = Directory.CreateTempSubdirectory("bdk-queueing-reopen-");
        try
        {
            var file = Path.Combine(directory.FullName, "delivery.db");
            await using (var first = CreateHost(file))
            {
                var initial = first.Services.GetRequiredService<LiteDbQueueBroker>();
                await initial.Enqueue(new DurableQueueMessage { Timestamp = first.Clock.UtcNow });
            }

            await using var reopened = CreateHost(file);
            var sut = reopened.Services.GetRequiredService<LiteDbQueueBroker>();
            await sut.Subscribe<DurableQueueMessage, DurableQueueHandler>();
            await sut.ProcessPendingAsync();
            (await sut.GetMessagesAsync()).Single().Status.ShouldBe(QueueMessageStatus.Succeeded);
        }
        finally { directory.Delete(true); }
    }

    private static LiteDbDurableTestHost CreateHost(string file = null) => new(s =>
    {
        s.AddSingleton<QueueDeliveryRecorder>();
        s.AddTransient<DurableQueueHandler>();
        s.AddQueueing().WithLiteDbBroker(o => { o.MessageExpiration = null; o.MaxDeliveryAttempts = 2; });
    }, file);
}

public class DurableQueueMessage : QueueMessageBase;

public class QueueDeliveryRecorder
{
    public int Count { get; set; }

    public bool Fail { get; set; }
}

public class DurableQueueHandler(QueueDeliveryRecorder recorder) : IQueueMessageHandler<DurableQueueMessage>
{
    public Task Handle(DurableQueueMessage message, CancellationToken cancellationToken)
    {
        if (recorder.Fail) { throw new InvalidOperationException("Retry fictional queue work."); }

        recorder.Count++;
        return Task.CompletedTask;
    }
}

public class BlockingQueueHandler(DurableDeliveryGate gate) : IQueueMessageHandler<DurableQueueMessage>
{
    public Task Handle(DurableQueueMessage message, CancellationToken cancellationToken) => gate.DeliverAsync();
}
