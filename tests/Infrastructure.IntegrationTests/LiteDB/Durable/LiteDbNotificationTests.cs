// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.IntegrationTests.LiteDB.Durable;

using BridgingIT.DevKit.Application.Notifications;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection;

[IntegrationTest("Infrastructure")]
public class LiteDbNotificationTests
{
    [Fact]
    public async Task ClaimsAndAttachments_RoundTripAndFenceExpiredOwner()
    {
        await using var host = CreateHost();
        var sut = host.Services.GetRequiredService<INotificationStorageProvider>();
        var email = CreateEmail(host.Clock.UtcNow);
        email.Attachments.Add(new() { Id = Guid.NewGuid(), FileName = "ledger.bin", ContentType = "application/octet-stream", Content = [0, 2, 255] });
        (await sut.SaveAsync(email, default)).IsSuccess.ShouldBeTrue();
        var claimed = (await sut.GetPendingAsync<EmailMessage>(1, default)).Value.Single();
        claimed.Attachments.Single().Content.ShouldBe(new byte[] { 0, 2, 255 });
        (await sut.GetPendingAsync<EmailMessage>(1, default)).Value.ShouldBeEmpty();
        host.Clock.Advance(TimeSpan.FromMinutes(6));
        var replacement = (await sut.GetPendingAsync<EmailMessage>(1, default)).Value.Single();
        claimed.Status = EmailMessageStatus.Sent;
        (await sut.UpdateAsync(claimed, default)).IsFailure.ShouldBeTrue();
        replacement.Status = EmailMessageStatus.Sent;
        replacement.SentAt = host.Clock.UtcNow;
        (await sut.UpdateAsync(replacement, default)).IsSuccess.ShouldBeTrue();
        var operations = host.Services.GetRequiredService<INotificationEmailOutboxService>();
        (await operations.GetMessageAsync(email.Id)).AttachmentCount.ShouldBe(1);
        (await operations.GetMessageContentAsync(email.Id)).Body.ShouldBe("Fictional ledger content.");
    }

    [Fact]
    public async Task NativeOutboxWorker_UsesConfiguredTransportAndPersistsOutcome()
    {
        await using var host = CreateHost();
        var storage = host.Services.GetRequiredService<INotificationStorageProvider>();
        var email = CreateEmail(host.Clock.UtcNow);
        await storage.SaveAsync(email, default);
        var transport = host.Services.GetRequiredService<INotificationService<EmailMessage>>();
        transport.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<NotificationSendOptions>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var message = call.ArgAt<EmailMessage>(0);
            message.Status = EmailMessageStatus.Sent;
            message.SentAt = host.Clock.UtcNow;
            return await storage.UpdateAsync(message, call.ArgAt<CancellationToken>(2));
        });
        await host.Services.GetRequiredService<IOutboxNotificationEmailWorker>().ProcessAsync();
        var operations = host.Services.GetRequiredService<INotificationEmailOutboxService>();
        (await operations.GetMessageStatsAsync()).Sent.ShouldBe(1);
        (await operations.GetMessageAsync(email.Id)).RetryCount.ShouldBe(1);
        await operations.ArchiveMessageAsync(email.Id);
        (await operations.GetMessagesAsync()).ShouldBeEmpty();
        await operations.PurgeMessagesAsync(isArchived: true);
        (await operations.GetMessageAsync(email.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task ReopenAndAutoArchive_PreserveEmailThenApplyRetention()
    {
        var directory = Directory.CreateTempSubdirectory("bdk-notification-reopen-");
        try
        {
            var file = Path.Combine(directory.FullName, "email.db");
            Guid id;
            await using (var first = CreateHost(file))
            {
                var email = CreateEmail(first.Clock.UtcNow);
                id = email.Id;
                email.Status = EmailMessageStatus.Sent;
                await first.Services.GetRequiredService<INotificationStorageProvider>().SaveAsync(email, default);
            }

            await using var reopened = CreateHost(file);
            var sut = reopened.Services.GetRequiredService<INotificationEmailOutboxService>();
            (await sut.GetMessageAsync(id)).Subject.ShouldBe("Fictional statement");
            (await sut.AutoArchiveMessagesAsync(reopened.Clock.UtcNow.AddMinutes(1))).ShouldBe(1);
            (await sut.GetMessageAsync(id)).IsArchived.ShouldBeTrue();
        }
        finally { directory.Delete(true); }
    }

    private static LiteDbDurableTestHost CreateHost(string file = null) => new(s =>
    {
        s.AddSingleton(Substitute.For<INotificationService<EmailMessage>>());
        new NotificationServiceBuilder(s).WithLiteDbStorageProvider(outbox: new OutboxNotificationEmailOptions());
    }, file);

    private static EmailMessage CreateEmail(DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), To = ["reader@example.invalid"], From = new() { Address = "sender@example.invalid" }, Subject = "Fictional statement", Body = "Fictional ledger content.", CreatedAt = now
    };
}
