// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Durable;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Application.Notifications;
using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
public class LiteDbDeliveryRegressionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pause_SubscriptionIdentifier_ShouldPreventDelivery(bool queueing)
    {
        await using var host = new LiteDbDurableTestHost(services =>
        {
            services.AddSingleton<MessageDeliveryRecorder>();
            services.AddSingleton<QueueDeliveryRecorder>();
            services.AddTransient<SuccessfulLedgerHandler>();
            services.AddTransient<DurableQueueHandler>();
            services.AddMessaging().WithLiteDbBroker(o => o.MessageExpiration = null);
            services.AddQueueing().WithLiteDbBroker(o => o.MessageExpiration = null);
        });
        if (queueing)
        {
            var sut = host.Services.GetRequiredService<LiteDbQueueBroker>();
            await sut.Subscribe<DurableQueueMessage, DurableQueueHandler>();
            await sut.Enqueue(new DurableQueueMessage { Timestamp = host.Clock.UtcNow });
            var subscription = (await sut.GetSubscriptionsAsync()).Single();
            await sut.PauseMessageTypeAsync(subscription.MessageType);
            await sut.ProcessPendingAsync();
            host.Services.GetRequiredService<QueueDeliveryRecorder>().Count.ShouldBe(0, subscription.MessageType);
            (await sut.GetSubscriptionsAsync()).Single().IsMessageTypePaused.ShouldBeTrue();
            await sut.ResumeMessageTypeAsync(subscription.MessageType);
            await sut.ProcessPendingAsync();
            host.Services.GetRequiredService<QueueDeliveryRecorder>().Count.ShouldBe(1);
        }
        else
        {
            var sut = host.Services.GetRequiredService<LiteDbMessageBroker>();
            await sut.Subscribe<DurableLedgerMessage, SuccessfulLedgerHandler>();
            await sut.Publish(new DurableLedgerMessage());
            var subscription = (await sut.GetSubscriptionsAsync()).Single();
            await sut.PauseMessageTypeAsync(subscription.MessageType);
            await sut.ProcessPendingAsync();
            host.Services.GetRequiredService<MessageDeliveryRecorder>().Successful.ShouldBe(0, subscription.MessageType);
            (await sut.GetSubscriptionsAsync()).Single().IsMessageTypePaused.ShouldBeTrue();
            await sut.ResumeMessageTypeAsync(subscription.MessageType);
            await sut.ProcessPendingAsync();
            host.Services.GetRequiredService<MessageDeliveryRecorder>().Successful.ShouldBe(1);
        }
    }

    [Fact]
    public async Task PurgeProcessed_ShouldPreserveUnsentEmail()
    {
        await using var host = NotificationsHost();
        var storage = host.Services.GetRequiredService<INotificationStorageProvider>();
        var email = Email(host.Clock.UtcNow);
        (await storage.SaveAsync(email, default)).IsSuccess.ShouldBeTrue();
        await host.Services.GetRequiredService<IOutboxNotificationEmailWorker>().PurgeAsync(processedOnly: true);
        var row = await host.Services.GetRequiredService<INotificationEmailOutboxService>().GetMessageAsync(email.Id);
        row.IsArchived.ShouldBeFalse("Unsent mail must survive processed-only startup purge");
    }

    [Fact]
    public async Task SequentialBatch_EachSendWithinLease_ShouldNotRedeliverSuccessfulEmail()
    {
        await using var host = NotificationsHost();
        var storage = host.Services.GetRequiredService<INotificationStorageProvider>();
        await storage.SaveAsync(Email(host.Clock.UtcNow), default);
        await storage.SaveAsync(Email(host.Clock.UtcNow.AddTicks(1)), default);
        var transport = host.Services.GetRequiredService<INotificationService<EmailMessage>>();
        var deliveries = 0;
        transport.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<NotificationSendOptions>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            deliveries++;
            host.Clock.Advance(TimeSpan.FromSeconds(3));
            var email = call.ArgAt<EmailMessage>(0);
            email.Status = EmailMessageStatus.Sent;
            email.SentAt = host.Clock.UtcNow;
            return await storage.UpdateAsync(email, default);
        });
        var sut = host.Services.GetRequiredService<IOutboxNotificationEmailWorker>();
        await sut.ProcessAsync();
        await sut.ProcessAsync();
        deliveries.ShouldBe(2, "Each 3-second send fits its 5-second lease, but the second claim expires while queued in the batch");
    }

    [Fact]
    public async Task ProcessedPurge_RemovesTerminalEmailsAndPreservesPendingAndLiveClaims()
    {
        await using var host = NotificationsHost();
        var storage = host.Services.GetRequiredService<INotificationStorageProvider>();
        var pending = Email(host.Clock.UtcNow);
        var sent = Email(host.Clock.UtcNow);
        sent.Status = EmailMessageStatus.Sent;
        var failed = Email(host.Clock.UtcNow);
        failed.Status = EmailMessageStatus.Failed;
        await storage.SaveAsync(pending, default);
        var leased = (await storage.GetPendingAsync<EmailMessage>(1, default)).Value.Single();
        var waiting = Email(host.Clock.UtcNow.AddSeconds(1));
        await storage.SaveAsync(waiting, default);
        await storage.SaveAsync(sent, default);
        await storage.SaveAsync(failed, default);
        await host.Services.GetRequiredService<IOutboxNotificationEmailWorker>().PurgeAsync(processedOnly: true);
        var operations = host.Services.GetRequiredService<INotificationEmailOutboxService>();
        (await operations.GetMessageAsync(leased.Id)).LockedUntil.ShouldNotBeNull();
        (await operations.GetMessageAsync(waiting.Id)).IsArchived.ShouldBeFalse();
        (await operations.GetMessageAsync(sent.Id)).ShouldBeNull();
        (await operations.GetMessageAsync(failed.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task Delivery_RenewsClaimAndPreventsCompetingClaimsPastOriginalExpiry()
    {
        await using var host = NotificationsHost();
        var storage = host.Services.GetRequiredService<INotificationStorageProvider>();
        var operations = host.Services.GetRequiredService<INotificationEmailOutboxService>();
        var email = Email(host.Clock.UtcNow);
        await storage.SaveAsync(email, default);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = host.Services.GetRequiredService<INotificationService<EmailMessage>>();
        transport.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<NotificationSendOptions>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(call.ArgAt<CancellationToken>(2));
            var delivered = call.ArgAt<EmailMessage>(0);
            delivered.Status = EmailMessageStatus.Sent;
            return await storage.UpdateAsync(delivered, default);
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var processing = host.Services.GetRequiredService<IOutboxNotificationEmailWorker>().ProcessAsync(cancellationToken: timeout.Token);
        try
        {
            await started.Task.WaitAsync(timeout.Token);
            var initial = (await operations.GetMessageAsync(email.Id)).LockedUntil;
            host.Clock.Advance(TimeSpan.FromSeconds(2));
            while ((await operations.GetMessageAsync(email.Id)).LockedUntil <= initial)
            {
                await Task.Delay(20, timeout.Token);
            }

            host.Clock.Advance(TimeSpan.FromSeconds(4));
            (await storage.GetPendingAsync<EmailMessage>(1, default)).Value.ShouldBeEmpty();
            release.TrySetResult();
            await processing;
            (await operations.GetMessageAsync(email.Id)).Status.ShouldBe(EmailMessageStatus.Sent);
        }
        finally { release.TrySetResult(); await processing; }
    }

    [Fact]
    public async Task Delivery_ReplacedClaim_CancelsTransportAndFencesOriginalOwner()
    {
        await using var host = NotificationsHost();
        var storage = host.Services.GetRequiredService<INotificationStorageProvider>();
        var operations = host.Services.GetRequiredService<INotificationEmailOutboxService>();
        var email = Email(host.Clock.UtcNow);
        await storage.SaveAsync(email, default);
        var started = new TaskCompletionSource<EmailMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = host.Services.GetRequiredService<INotificationService<EmailMessage>>();
        transport.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<NotificationSendOptions>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            started.TrySetResult(call.ArgAt<EmailMessage>(0));
            await Task.Delay(Timeout.InfiniteTimeSpan, call.ArgAt<CancellationToken>(2));
            return Result.Success();
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var processing = host.Services.GetRequiredService<IOutboxNotificationEmailWorker>().ProcessAsync(cancellationToken: timeout.Token);
        var original = await started.Task.WaitAsync(timeout.Token);
        await operations.RetryMessageAsync(email.Id);
        var replacement = (await storage.GetPendingAsync<EmailMessage>(1, default)).Value.Single();
        await Should.ThrowAsync<OperationCanceledException>(() => processing.WaitAsync(timeout.Token));
        original.Status = EmailMessageStatus.Sent;
        (await storage.UpdateAsync(original, default)).IsFailure.ShouldBeTrue();
        replacement.Status = EmailMessageStatus.Sent;
        (await storage.UpdateAsync(replacement, default)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void TypeResolution_UnknownOrAmbiguousNames_FailsInsteadOfPausingAnotherType()
    {
        Should.Throw<ArgumentException>(() => LiteDbMessageTypeResolver.Resolve("missing", [typeof(DurableLedgerMessage)]));
        Should.Throw<ArgumentException>(() => LiteDbMessageTypeResolver.Resolve("DuplicateMessage", [typeof(ReviewTypeScopeOne.DuplicateMessage), typeof(ReviewTypeScopeTwo.DuplicateMessage)]));
        LiteDbMessageTypeResolver.Resolve(typeof(DurableLedgerMessage).FullName, [typeof(DurableLedgerMessage)]).ShouldBe(typeof(DurableLedgerMessage).AssemblyQualifiedNameShort());
    }

    [Fact]
    public async Task RegisteredWorker_UsesRealNotificationServiceAndSelectedOperationBehavior()
    {
        var behavior = Substitute.For<INotificationOperationBehavior>();
        behavior.ExecuteAsync(Arg.Any<NotificationOperationContext>(), Arg.Any<Func<Task<Result>>>()).Returns(call => call.ArgAt<Func<Task<Result>>>(1)());
        await using var host = new LiteDbDurableTestHost(services =>
        {
            services.AddSingleton(behavior);
            services.AddNotificationService<EmailMessage>(configure: builder => builder
                .WithFakeSmtpClient(new FakeSmtpClientOptions { LogMessageBody = false })
                .WithSmtpSettings(settings => settings.Host = "smtp.example.invalid")
                .WithLiteDbStorageProvider(outbox: new OutboxNotificationEmailOptions { AutoArchiveAfter = null }));
        });
        var email = Email(host.Clock.UtcNow);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<INotificationService<EmailMessage>>();
            (await service.SendAsync(email, new NotificationSendOptions(), default)).IsSuccess.ShouldBeTrue();
        }

        await host.Services.GetRequiredService<IOutboxNotificationEmailWorker>().ProcessAsync();
        var row = await host.Services.GetRequiredService<INotificationEmailOutboxService>().GetMessageAsync(email.Id);
        row.Status.ShouldBe(EmailMessageStatus.Sent);
        row.RetryCount.ShouldBe(1);
        await behavior.Received(1).ExecuteAsync(Arg.Is<NotificationOperationContext>(context => context.Operation == "OutboxExecution" && context.IsWorker), Arg.Any<Func<Task<Result>>>());
    }

    [Fact]
    public async Task RetryLimit_ReportsFailedOperationWithoutInvokingTransport()
    {
        var behavior = Substitute.For<INotificationOperationBehavior>();
        var observed = default(Result?);
        behavior.ExecuteAsync(Arg.Any<NotificationOperationContext>(), Arg.Any<Func<Task<Result>>>()).Returns(async call =>
        {
            var result = await call.ArgAt<Func<Task<Result>>>(1)();
            observed = result;
            return result;
        });
        await using var host = new LiteDbDurableTestHost(services =>
        {
            services.AddSingleton(behavior);
            services.AddSingleton(Substitute.For<INotificationService<EmailMessage>>());
            new NotificationServiceBuilder(services).WithLiteDbStorageProvider(outbox: new OutboxNotificationEmailOptions { RetryCount = 3 });
        });
        var email = Email(host.Clock.UtcNow);
        email.RetryCount = 3;
        await host.Services.GetRequiredService<INotificationStorageProvider>().SaveAsync(email, default);
        await host.Services.GetRequiredService<IOutboxNotificationEmailWorker>().ProcessAsync();
        observed.HasValue.ShouldBeTrue();
        observed.Value.IsFailure.ShouldBeTrue();
        (await host.Services.GetRequiredService<INotificationEmailOutboxService>().GetMessageAsync(email.Id)).Status.ShouldBe(EmailMessageStatus.Failed);
        await host.Services.GetRequiredService<INotificationService<EmailMessage>>().DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<NotificationSendOptions>(), Arg.Any<CancellationToken>());
    }

    private static LiteDbDurableTestHost NotificationsHost() => new(services =>
    {
        services.AddSingleton(Substitute.For<INotificationService<EmailMessage>>());
        new NotificationServiceBuilder(services).WithLiteDbStorageProvider(outbox: new OutboxNotificationEmailOptions
        {
            ProcessingCount = 2, LeaseDuration = TimeSpan.FromSeconds(5), AutoArchiveAfter = null
        });
    });

    private static EmailMessage Email(DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), To = ["reader@example.invalid"], From = new() { Address = "sender@example.invalid" },
        Subject = "Fictional ledger statement", Body = "Fictional ledger statement", CreatedAt = now
    };
}

public static class ReviewTypeScopeOne
{
    public class DuplicateMessage;
}

public static class ReviewTypeScopeTwo
{
    public class DuplicateMessage;
}
