// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Notifications.Tests;

using BridgingIT.DevKit.Application.IntegrationTests;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

[IntegrationTest("Application")]
[Collection(nameof(TestEnvironmentCollection))] // https://xunit.net/docs/shared-context#collection-fixture
public class EmailServiceFakeSmtpTests : IAsyncLifetime
{
    private readonly TestEnvironmentFixture fixture;
    private INotificationStorageProvider storageProvider;
    private readonly NotificationServiceOptions options;
    private readonly IServiceProvider serviceProvider;
    private IOutboxNotificationEmailQueue outboxQueue;

    public EmailServiceFakeSmtpTests(ITestOutputHelper output)
    {
        this.fixture = new TestEnvironmentFixture().WithOutput(output);
        var services = this.fixture.Services;
        services.AddLogging(builder => builder.AddConsole());
        services.AddDbContext<StubDbContext>(options =>
            options.UseSqlServer(this.fixture.SqlConnectionString));

        // Use fluent builder to set up services
        services.AddNotificationService<EmailMessage>(null, b => b
            .WithEntityFrameworkStorageProvider<StubDbContext>()
            .WithOutbox<StubDbContext>(o => o.Enabled(true))
            .WithFakeSmtpClient(new FakeSmtpClientOptions { LogMessageBodyLength = 256 })
            .WithSmtpSettings(s =>
            {
                s.Host = "127.0.0.1";
                s.Port = 25;
                s.UseSsl = false;
                s.SenderName = "Test App";
                s.SenderAddress = "test@app.com";
            }));

        this.serviceProvider = this.fixture.ServiceProvider;
        this.options = this.serviceProvider.GetRequiredService<NotificationServiceOptions>();
    }

    public async Task InitializeAsync()
    {
        await this.fixture.InitializeAsync();

        this.storageProvider = this.serviceProvider.GetRequiredService<INotificationStorageProvider>();
        this.outboxQueue = this.serviceProvider.GetRequiredService<IOutboxNotificationEmailQueue>();
        this.fixture.EnsureSqlServerDbContext();
    }

    public async Task DisposeAsync()
    {
        await this.fixture.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_ImmediateWithOutbox_SavesAndSendsEmail()
    {
        // Arrange
        var emailService = this.serviceProvider.GetRequiredService<INotificationService<EmailMessage>>();
        var message = new EmailMessage
        {
            Id = Guid.NewGuid(),
            To = ["recipient@example.com"],
            From = new EmailAddress { Address = "sender@example.com", Name = "Sender" },
            Subject = "Test Email",
            Body = "<p>This is a test email</p>",
            IsHtml = true,
            Priority = EmailMessagePriority.Normal,
            Attachments =
            [
                new EmailAttachment
                {
                    Id = Guid.NewGuid(),
                    FileName = "test.txt",
                    ContentType = "text/plain",
                    Content = System.Text.Encoding.UTF8.GetBytes("Test content"),
                    IsEmbedded = false
                }
            ]
        };

        // Act
        var result = await emailService.SendAsync(
            message, new NotificationSendOptions { SendImmediately = true }, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StubDbContext>();
        var storedMessage = await dbContext.NotificationsEmails
            .Include(e => e.Attachments)
            .FirstOrDefaultAsync(m => m.Id == message.Id);
        storedMessage.ShouldNotBeNull();
        storedMessage.Status.ShouldBe(EmailMessageStatus.Sent);
        storedMessage.SentAt.ShouldNotBeNull();
        storedMessage.Subject.ShouldBe("Test Email");
        storedMessage.Body.ShouldBe("<p>This is a test email</p>");
        storedMessage.IsHtml.ShouldBeTrue();
        storedMessage.Attachments.ShouldNotBeEmpty();
        storedMessage.Attachments.First().FileName.ShouldBe("test.txt");
    }

    [Fact]
    public async Task SendAsync_QueuedWithOutbox_SavesAndEnqueues()
    {
        // Arrange
        var emailService = this.serviceProvider.GetRequiredService<INotificationService<EmailMessage>>();
        this.options.OutboxOptions.ProcessingMode = OutboxNotificationEmailProcessingMode.Immediate;
        var message = new EmailMessage
        {
            Id = Guid.NewGuid(),
            To = ["recipient@example.com"],
            From = new EmailAddress { Address = "sender@example.com", Name = "Sender" },
            Subject = "Test Email",
            Body = "This is a test email",
            IsHtml = false,
            Priority = EmailMessagePriority.Normal
        };

        // Act
        var result = await emailService.SendAsync(
            message, new NotificationSendOptions { SendImmediately = false }, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        //this.outboxQueue.Received(1).Enqueue(message.Id.ToString());
        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StubDbContext>();
        var storedMessage = await dbContext.NotificationsEmails
            .Include(e => e.Attachments)
            .FirstOrDefaultAsync(m => m.Id == message.Id);
        storedMessage.ShouldNotBeNull();
        storedMessage.Status.ShouldBe(EmailMessageStatus.Pending);
        storedMessage.SentAt.ShouldBeNull();
    }

    [Fact]
    public async Task QueueAsync_WithoutExplicitFrom_UsesConfiguredSenderAndPersists()
    {
        // Arrange
        var emailService = this.serviceProvider.GetRequiredService<INotificationService<EmailMessage>>();
        var message = new EmailMessage
        {
            Id = Guid.NewGuid(),
            To = ["recipient@example.com"],
            Subject = "Queued without sender",
            Body = "This is a test email",
            IsHtml = false,
            Priority = EmailMessagePriority.Normal
        };

        // Act
        var result = await emailService.QueueAsync(message, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StubDbContext>();
        var storedMessage = await dbContext.NotificationsEmails
            .FirstOrDefaultAsync(m => m.Id == message.Id);
        storedMessage.ShouldNotBeNull();
        storedMessage.From.ShouldContain("test@app.com");
        storedMessage.From.ShouldContain("Test App");
        storedMessage.Status.ShouldBe(EmailMessageStatus.Pending);
    }

    [Fact]
    public async Task SendAsync_NoOutbox_SendsDirectly()
    {
        // Arrange
        this.options.IsOutboxConfigured = false;
        var emailService = this.serviceProvider.GetRequiredService<INotificationService<EmailMessage>>();
        var message = new EmailMessage
        {
            Id = Guid.NewGuid(),
            To = ["recipient@example.com"],
            From = new EmailAddress { Address = "sender@example.com", Name = "Sender" },
            Subject = "Test Email",
            Body = "This is a test email",
            IsHtml = false,
            Priority = EmailMessagePriority.Normal
        };

        // Act
        var result = await emailService.SendAsync(message, new NotificationSendOptions { SendImmediately = true }, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StubDbContext>();
        var storedMessage = await dbContext.NotificationsEmails
            .Include(e => e.Attachments)
            .FirstOrDefaultAsync(m => m.Id == message.Id);
        storedMessage.ShouldBeNull();
    }

    [Fact]
    public async Task SendAsync_MimeMessageMapping_HandlesEmbeddedAttachments()
    {
        // Arrange
        this.options.IsOutboxConfigured = false;
        var emailService = this.serviceProvider.GetRequiredService<INotificationService<EmailMessage>>();
        var message = new EmailMessage
        {
            Id = Guid.NewGuid(),
            To = ["recipient@example.com"],
            From = new EmailAddress { Address = "sender@example.com", Name = "Sender" },
            Subject = "Test Email with Embedded",
            Body = "<p>Embedded image: <img src='cid:test-image'></p>",
            IsHtml = true,
            Priority = EmailMessagePriority.Normal,
            Attachments =
            [
                new EmailAttachment
                {
                    Id = Guid.NewGuid(),
                    FileName = "image.jpg",
                    ContentType = "image/jpeg",
                    Content = [0xFF, 0xD8, 0xFF, 0xE0], // Mock JPEG header
                    IsEmbedded = true,
                    ContentId = "test-image"
                }
            ]
        };

        // Act
        var result = await emailService.SendAsync(message, new NotificationSendOptions { SendImmediately = true }, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task SendAsync_RetryFailure_MarksAsFailed()
    {
        // Arrange
        var emailService = this.serviceProvider.GetRequiredService<INotificationService<EmailMessage>>();
        var worker = new OutboxNotificationEmailWorker(Substitute.For<ILoggerFactory>(), this.serviceProvider);
        var message = new EmailMessage
        {
            Id = Guid.NewGuid(),
            To = ["recipient@example.com"],
            From = new EmailAddress { Address = "sender@example.com", Name = "Sender" },
            Subject = "Test Email",
            Body = "This is a test email",
            IsHtml = false,
            Priority = EmailMessagePriority.Normal,
            // Simulate max retries
            RetryCount = 3 // RetryCount is 3, so this is the 3rd attempt
        };
        await this.storageProvider.SaveAsync(message, CancellationToken.None);

        // Act
        await worker.ProcessAsync(message.Id.ToString(), CancellationToken.None);

        // Assert
        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StubDbContext>();
        var storedMessage = await dbContext.NotificationsEmails
            .Include(e => e.Attachments)
            .FirstOrDefaultAsync(m => m.Id == message.Id);
        storedMessage.ShouldNotBeNull();
        storedMessage.Status.ShouldBe(EmailMessageStatus.Failed);
        storedMessage.SentAt.ShouldBeNull();
        storedMessage.PropertiesJson.ShouldContain("Max retries reached");
    }

    [Fact]
    public async Task GetPendingAsync_WithConcurrentReaders_ShouldClaimMessageOnlyOnce()
    {
        // Arrange
        var message = new EmailMessage
        {
            Id = Guid.NewGuid(),
            To = ["recipient@example.com"],
            From = new EmailAddress { Address = "sender@example.com", Name = "Sender" },
            Subject = "Claim once",
            Body = "This is a test email",
            IsHtml = false,
            Priority = EmailMessagePriority.Normal
        };

        await this.storageProvider.SaveAsync(message, CancellationToken.None);

        // Act
        async Task<IReadOnlyList<EmailMessage>> claimAsync()
        {
            using var scope = this.serviceProvider.CreateScope();
            var provider = scope.ServiceProvider.GetRequiredService<INotificationStorageProvider>();
            var result = await provider.GetPendingAsync<EmailMessage>(1, CancellationToken.None);
            result.IsSuccess.ShouldBeTrue();
            return result.Value.ToList();
        }

        var claimed = await Task.WhenAll(claimAsync(), claimAsync());

        // Assert
        claimed.Sum(batch => batch.Count).ShouldBe(1);
        using var verifyScope = this.serviceProvider.CreateScope();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<StubDbContext>();
        var storedMessage = await dbContext.NotificationsEmails.FirstOrDefaultAsync(m => m.Id == message.Id);
        storedMessage.ShouldNotBeNull();
        storedMessage.Status.ShouldBe(EmailMessageStatus.Locked);
        storedMessage.LockedBy.ShouldNotBeNullOrWhiteSpace();
        storedMessage.LockedUntil.ShouldNotBeNull();
        storedMessage.LockedUntil.Value.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task GetPendingAsync_WithExpiredLease_ShouldAllowTakeover()
    {
        // Arrange
        var message = new EmailMessage
        {
            Id = Guid.NewGuid(),
            To = ["recipient@example.com"],
            From = new EmailAddress { Address = "sender@example.com", Name = "Sender" },
            Subject = "Expired lease",
            Body = "This is a test email",
            IsHtml = false,
            Priority = EmailMessagePriority.Normal
        };

        await this.storageProvider.SaveAsync(message, CancellationToken.None);

        using (var firstScope = this.serviceProvider.CreateScope())
        {
            var firstProvider = firstScope.ServiceProvider.GetRequiredService<INotificationStorageProvider>();
            var firstClaim = await firstProvider.GetPendingAsync<EmailMessage>(1, CancellationToken.None);
            firstClaim.IsSuccess.ShouldBeTrue();
            firstClaim.Value.Count().ShouldBe(1);
        }

        string firstLockedBy;
        using (var expireScope = this.serviceProvider.CreateScope())
        {
            var dbContext = expireScope.ServiceProvider.GetRequiredService<StubDbContext>();
            var entity = await dbContext.NotificationsEmails.FirstAsync(m => m.Id == message.Id);
            firstLockedBy = entity.LockedBy;
            entity.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(-1);
            entity.AdvanceConcurrencyVersion();
            await dbContext.SaveChangesAsync();
        }

        // Act
        using var secondScope = this.serviceProvider.CreateScope();
        var secondProvider = secondScope.ServiceProvider.GetRequiredService<INotificationStorageProvider>();
        var secondClaim = await secondProvider.GetPendingAsync<EmailMessage>(1, CancellationToken.None);

        // Assert
        secondClaim.IsSuccess.ShouldBeTrue();
        secondClaim.Value.Count().ShouldBe(1);

        using var verifyScope = this.serviceProvider.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<StubDbContext>();
        var storedMessage = await verifyContext.NotificationsEmails.FirstAsync(m => m.Id == message.Id);
        storedMessage.LockedBy.ShouldNotBe(firstLockedBy);
        storedMessage.LockedUntil.ShouldNotBeNull();
        storedMessage.LockedUntil.Value.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ArchiveMessageAsync_WhenMessageExists_ArchivesMessageAndRemovesItFromPendingClaims()
    {
        // Arrange
        var message = new EmailMessage
        {
            Id = Guid.NewGuid(),
            To = ["recipient@example.com"],
            From = new EmailAddress { Address = "sender@example.com", Name = "Sender" },
            Subject = "Archive me",
            Body = "This message should be archived",
            IsHtml = false,
            Priority = EmailMessagePriority.Normal
        };

        await this.storageProvider.SaveAsync(message, CancellationToken.None);

        using var scope = this.serviceProvider.CreateScope();
        var outboxService = scope.ServiceProvider.GetRequiredService<INotificationEmailOutboxService>();

        // Act
        await outboxService.ArchiveMessageAsync(message.Id, CancellationToken.None);

        // Assert
        using var verifyScope = this.serviceProvider.CreateScope();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<StubDbContext>();
        var storedMessage = await dbContext.NotificationsEmails.FirstOrDefaultAsync(m => m.Id == message.Id);
        storedMessage.ShouldNotBeNull();
        storedMessage.IsArchived.ShouldBeTrue();
        storedMessage.ArchivedDate.ShouldNotBeNull();

        var pendingResult = await this.storageProvider.GetPendingAsync<EmailMessage>(10, CancellationToken.None);
        pendingResult.IsSuccess.ShouldBeTrue();
        pendingResult.Value.ShouldNotContain(item => item.Id == message.Id);
    }

    [Fact]
    public async Task RetryMessageAsync_WhenMessageWasArchived_ReactivatesMessage()
    {
        // Arrange
        var message = new EmailMessage
        {
            Id = Guid.NewGuid(),
            To = ["recipient@example.com"],
            From = new EmailAddress { Address = "sender@example.com", Name = "Sender" },
            Subject = "Retry archived",
            Body = "This message should reactivate",
            IsHtml = false,
            Priority = EmailMessagePriority.Normal
        };

        await this.storageProvider.SaveAsync(message, CancellationToken.None);

        using (var arrangeScope = this.serviceProvider.CreateScope())
        {
            var dbContext = arrangeScope.ServiceProvider.GetRequiredService<StubDbContext>();
            var entity = await dbContext.NotificationsEmails.FirstAsync(m => m.Id == message.Id);
            entity.Status = EmailMessageStatus.Failed;
            entity.RetryCount = 1;
            entity.IsArchived = true;
            entity.ArchivedDate = DateTimeOffset.UtcNow.AddMinutes(-5);
            entity.AdvanceConcurrencyVersion();
            await dbContext.SaveChangesAsync();
        }

        using var scope = this.serviceProvider.CreateScope();
        var outboxService = scope.ServiceProvider.GetRequiredService<INotificationEmailOutboxService>();

        // Act
        await outboxService.RetryMessageAsync(message.Id, CancellationToken.None);

        // Assert
        using var verifyScope = this.serviceProvider.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<StubDbContext>();
        var storedMessage = await verifyContext.NotificationsEmails.FirstAsync(m => m.Id == message.Id);
        storedMessage.Status.ShouldBe(EmailMessageStatus.Pending);
        storedMessage.RetryCount.ShouldBe(0);
        storedMessage.IsArchived.ShouldBeFalse();
        storedMessage.ArchivedDate.ShouldBeNull();
    }

    [Fact]
    public async Task ProcessAsync_WhenAutoArchiveAfterIsReached_ArchivesEligibleTerminalMessages()
    {
        // Arrange
        var sentMessage = new EmailMessage
        {
            Id = Guid.NewGuid(),
            To = ["recipient@example.com"],
            From = new EmailAddress { Address = "sender@example.com", Name = "Sender" },
            Subject = "Sent archive candidate",
            Body = "This message should auto-archive after it is sent",
            IsHtml = false,
            Priority = EmailMessagePriority.Normal
        };

        var failedMessage = new EmailMessage
        {
            Id = Guid.NewGuid(),
            To = ["recipient@example.com"],
            From = new EmailAddress { Address = "sender@example.com", Name = "Sender" },
            Subject = "Failed archive candidate",
            Body = "This message should auto-archive after it fails permanently",
            IsHtml = false,
            Priority = EmailMessagePriority.Normal
        };

        await this.storageProvider.SaveAsync(sentMessage, CancellationToken.None);
        await this.storageProvider.SaveAsync(failedMessage, CancellationToken.None);

        using (var arrangeScope = this.serviceProvider.CreateScope())
        {
            var dbContext = arrangeScope.ServiceProvider.GetRequiredService<StubDbContext>();

            var sentEntity = await dbContext.NotificationsEmails.FirstAsync(m => m.Id == sentMessage.Id);
            sentEntity.Status = EmailMessageStatus.Sent;
            sentEntity.SentAt = DateTimeOffset.UtcNow.AddMinutes(-5);
            sentEntity.AdvanceConcurrencyVersion();

            var failedEntity = await dbContext.NotificationsEmails.FirstAsync(m => m.Id == failedMessage.Id);
            failedEntity.Status = EmailMessageStatus.Failed;
            failedEntity.RetryCount = this.options.OutboxOptions.RetryCount;
            failedEntity.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
            failedEntity.AdvanceConcurrencyVersion();

            await dbContext.SaveChangesAsync();
        }

        var workerOptions = new OutboxNotificationEmailOptions
        {
            ProcessingCount = this.options.OutboxOptions.ProcessingCount,
            RetryCount = this.options.OutboxOptions.RetryCount,
            AutoArchiveAfter = TimeSpan.Zero,
            AutoArchiveStatuses = [EmailMessageStatus.Sent, EmailMessageStatus.Failed]
        };

        var worker = new OutboxNotificationEmailWorker(
            Substitute.For<ILoggerFactory>(),
            this.serviceProvider,
            workerOptions);

        // Act
        await worker.ProcessAsync(cancellationToken: CancellationToken.None);

        // Assert
        using var verifyScope = this.serviceProvider.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<StubDbContext>();
        var storedSentMessage = await verifyContext.NotificationsEmails.FirstAsync(m => m.Id == sentMessage.Id);
        var storedFailedMessage = await verifyContext.NotificationsEmails.FirstAsync(m => m.Id == failedMessage.Id);

        storedSentMessage.IsArchived.ShouldBeTrue();
        storedSentMessage.ArchivedDate.ShouldNotBeNull();
        storedFailedMessage.IsArchived.ShouldBeTrue();
        storedFailedMessage.ArchivedDate.ShouldNotBeNull();
    }

    [Fact]
    public async Task PurgeMessagesAsync_WhenArchiveFilterMatches_DeletesArchivedRows()
    {
        // Arrange
        var message = new EmailMessage
        {
            Id = Guid.NewGuid(),
            To = ["recipient@example.com"],
            From = new EmailAddress { Address = "sender@example.com", Name = "Sender" },
            Subject = "Purge archived",
            Body = "This message should be deleted",
            IsHtml = false,
            Priority = EmailMessagePriority.Normal
        };

        await this.storageProvider.SaveAsync(message, CancellationToken.None);

        using (var arrangeScope = this.serviceProvider.CreateScope())
        {
            var dbContext = arrangeScope.ServiceProvider.GetRequiredService<StubDbContext>();
            var entity = await dbContext.NotificationsEmails.FirstAsync(m => m.Id == message.Id);
            entity.Status = EmailMessageStatus.Sent;
            entity.IsArchived = true;
            entity.ArchivedDate = DateTimeOffset.UtcNow.AddMinutes(-10);
            entity.AdvanceConcurrencyVersion();
            await dbContext.SaveChangesAsync();
        }

        using var scope = this.serviceProvider.CreateScope();
        var outboxService = scope.ServiceProvider.GetRequiredService<INotificationEmailOutboxService>();

        // Act
        await outboxService.PurgeMessagesAsync(
            olderThan: DateTimeOffset.UtcNow.AddMinutes(-1),
            statuses: [EmailMessageStatus.Sent],
            isArchived: true,
            cancellationToken: CancellationToken.None);

        // Assert
        using var verifyScope = this.serviceProvider.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<StubDbContext>();
        var storedMessage = await verifyContext.NotificationsEmails.FirstOrDefaultAsync(m => m.Id == message.Id);
        storedMessage.ShouldBeNull();
    }
}
