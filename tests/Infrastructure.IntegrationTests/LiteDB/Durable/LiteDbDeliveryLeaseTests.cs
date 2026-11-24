// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Durable;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
public class LiteDbDeliveryLeaseTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delivery_RenewsCurrentLease_PreventsConcurrentHandler(bool queueing)
    {
        await using var host = new LiteDbDurableTestHost(services =>
        {
            services.AddSingleton<DurableDeliveryGate>();
            services.AddTransient<BlockingLedgerHandler>();
            services.AddTransient<BlockingQueueHandler>();
            services.AddMessaging().WithLiteDbBroker(o => { o.MessageExpiration = null; o.LeaseRenewalInterval = TimeSpan.FromMilliseconds(50); });
            services.AddQueueing().WithLiteDbBroker(o => { o.MessageExpiration = null; o.LeaseRenewalInterval = TimeSpan.FromMilliseconds(50); });
        });
        var messages = host.Services.GetRequiredService<LiteDbMessageBroker>();
        var queues = host.Services.GetRequiredService<LiteDbQueueBroker>();
        var gate = host.Services.GetRequiredService<DurableDeliveryGate>();
        if (queueing)
        {
            await queues.Subscribe<DurableQueueMessage, BlockingQueueHandler>();
            await queues.Enqueue(new DurableQueueMessage { Timestamp = host.Clock.UtcNow });
        }
        else
        {
            await messages.Subscribe<DurableLedgerMessage, BlockingLedgerHandler>();
            await messages.Publish(new DurableLedgerMessage(host.Clock.UtcNow));
        }

        var processing = queueing ? queues.ProcessPendingAsync() : messages.ProcessPendingAsync();
        try
        {
            await gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var initial = await LockedUntilAsync();
            host.Clock.Advance(TimeSpan.FromSeconds(10));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (await LockedUntilAsync() <= initial)
            {
                await Task.Delay(20, timeout.Token);
            }

            if (queueing) { await queues.ProcessPendingAsync(); }
            else { await messages.ProcessPendingAsync(); }

            gate.Count.ShouldBe(1);
            gate.Release.TrySetResult();
            await processing;
            if (queueing) { (await queues.GetMessagesAsync()).Single().Status.ShouldBe(QueueMessageStatus.Succeeded); }
            else { (await messages.GetMessagesAsync()).Single().Status.ShouldBe(BrokerMessageStatus.Succeeded); }
        }
        finally { gate.Release.TrySetResult(); await processing; }

        async Task<DateTimeOffset?> LockedUntilAsync() => queueing
            ? (await queues.GetMessagesAsync()).Single().LockedUntil
            : (await messages.GetMessagesAsync()).Single().LockedUntil;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpiredWork_IsRetainedWithoutInvokingHandler(bool queueing)
    {
        await using var host = new LiteDbDurableTestHost(services =>
        {
            services.AddSingleton<MessageDeliveryRecorder>();
            services.AddSingleton<QueueDeliveryRecorder>();
            services.AddTransient<SuccessfulLedgerHandler>();
            services.AddTransient<DurableQueueHandler>();
            services.AddMessaging().WithLiteDbBroker(o => o.MessageExpiration = TimeSpan.FromMinutes(1));
            services.AddQueueing().WithLiteDbBroker(o => o.MessageExpiration = TimeSpan.FromMinutes(1));
        });
        if (queueing)
        {
            var sut = host.Services.GetRequiredService<LiteDbQueueBroker>();
            await sut.Subscribe<DurableQueueMessage, DurableQueueHandler>();
            await sut.Enqueue(new DurableQueueMessage { Timestamp = host.Clock.UtcNow });
            host.Clock.Advance(TimeSpan.FromMinutes(2));
            await sut.ProcessPendingAsync();
            (await sut.GetMessagesAsync()).Single().Status.ShouldBe(QueueMessageStatus.Expired);
            host.Services.GetRequiredService<QueueDeliveryRecorder>().Count.ShouldBe(0);
        }
        else
        {
            var sut = host.Services.GetRequiredService<LiteDbMessageBroker>();
            await sut.Subscribe<DurableLedgerMessage, SuccessfulLedgerHandler>();
            await sut.Publish(new DurableLedgerMessage(host.Clock.UtcNow));
            host.Clock.Advance(TimeSpan.FromMinutes(2));
            await sut.ProcessPendingAsync();
            (await sut.GetMessagesAsync()).Single().Status.ShouldBe(BrokerMessageStatus.Expired);
            host.Services.GetRequiredService<MessageDeliveryRecorder>().Successful.ShouldBe(0);
        }
    }
}
