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
            Priority = EmailPriority.Normal,
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
        storedMessage.Status.ShouldBe(EmailStatus.Sent);
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
            Priority = EmailPriority.Normal
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
        storedMessage.Status.ShouldBe(EmailStatus.Pending);
        storedMessage.SentAt.ShouldBeNull();
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
            Priority = EmailPriority.Normal
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
            Priority = EmailPriority.Normal,
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
            Priority = EmailPriority.Normal,
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
        storedMessage.Status.ShouldBe(EmailStatus.Failed);
        storedMessage.SentAt.ShouldNotBeNull();
        storedMessage.PropertiesJson.ShouldContain("Max retries reached");
    }
}