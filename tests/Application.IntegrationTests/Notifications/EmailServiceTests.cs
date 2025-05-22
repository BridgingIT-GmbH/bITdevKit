namespace BridgingIT.DevKit.Application.Notifications.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;
using MimeKit;
using BridgingIT.DevKit.Application.IntegrationTests;

public class EmailServiceTests : IAsyncLifetime
{
    private readonly TestEnvironmentFixture fixture;
    private readonly IServiceProvider serviceProvider;
    private readonly INotificationStorageProvider storageProvider;
    private readonly IOutboxNotificationEmailQueue outboxQueue;
    private readonly NotificationServiceOptions options;

    public EmailServiceTests(ITestOutputHelper output)
    {
        this.fixture = new TestEnvironmentFixture().WithOutput(output);
        var services = this.fixture.Services;
        services.AddLogging(builder => builder.AddConsole());
        services.AddDbContext<StubDbContext>(options =>
            options.UseSqlServer(this.fixture.SqlConnectionString));

        // Use fluent builder to set up services
        var builder = services.AddNotificationService<EmailMessage>(null, b =>
            new NotificationServiceInfrastructureBuilder(services)
                .WithSmtpSettings(s =>
                {
                    s.Host = new Uri(this.fixture.MailHogSmtpConnectionString).Host;
                    s.Port = new Uri(this.fixture.MailHogSmtpConnectionString).Port;
                    s.UseSsl = false;
                    s.SenderName = "Test App";
                    s.SenderAddress = "test@app.com";
                })
                .WithSmtpClient());
                // .WithEntityFrameworkProvider<StubDbContext>()
                // .WithOutbox<StubDbContext>(o => o
                //     .ProcessingMode(OutboxNotificationEmailProcessingMode.Interval)
                //     .ProcessingCount(100)
                //     .RetryCount(3))
                // .WithRetryer(r => r.MaxRetries(3).Delay(TimeSpan.FromSeconds(1)).UseExponentialBackoff())
                // .WithTimeout(TimeSpan.FromSeconds(30)));

        this.serviceProvider = this.fixture.ServiceProvider;
        this.storageProvider = this.serviceProvider.GetRequiredService<INotificationStorageProvider>();
        this.outboxQueue = this.serviceProvider.GetRequiredService<IOutboxNotificationEmailQueue>();
        this.options = this.serviceProvider.GetRequiredService<NotificationServiceOptions>();
    }

    public async Task InitializeAsync()
    {
        await this.fixture.InitializeAsync();
        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StubDbContext>();
        await dbContext.Database.MigrateAsync();
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
            To = new List<string> { "recipient@example.com" },
            From = new EmailAddress { Address = "sender@example.com", Name = "Sender" },
            Subject = "Test Email",
            Body = "<p>This is a test email</p>",
            IsHtml = true,
            Priority = EmailPriority.Normal,
            Attachments = new List<EmailAttachment>
            {
                new EmailAttachment
                {
                    Id = Guid.NewGuid(),
                    FileName = "test.txt",
                    ContentType = "text/plain",
                    Content = System.Text.Encoding.UTF8.GetBytes("Test content"),
                    IsEmbedded = false
                }
            }
        };

        // Act
        var result = await emailService.SendAsync(message, new NotificationSendOptions { SendImmediately = true }, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StubDbContext>();
        var storedMessage = await dbContext.OutboxNotificationEmails
            .FirstOrDefaultAsync(m => m.Id == message.Id);
        storedMessage.ShouldNotBeNull();
        storedMessage.Status.ShouldBe(EmailStatus.Sent);
        storedMessage.SentAt.ShouldNotBeNull();
        storedMessage.Subject.ShouldBe("Test Email");
        storedMessage.Body.ShouldBe("<p>This is a test email</p>");
        storedMessage.IsHtml.ShouldBeTrue();
        storedMessage.Attachments.ShouldNotBeEmpty();
        storedMessage.Attachments.First().FileName.ShouldBe("test.txt");

        // Validate email content via MailHog API
        using var client = this.fixture.GetMailHogApiClient();
        var response = await client.GetAsync("/api/v2/messages");
        var json = await response.Content.ReadAsStringAsync();
        var messages = JsonSerializer.Deserialize<MailHogMessages>(json);
        messages.Items.ShouldNotBeEmpty();
        var sentMessage = messages.Items.First();
        sentMessage.Raw.To.ShouldContain("recipient@example.com");
        sentMessage.Raw.Subject.ShouldBe("Test Email");
        sentMessage.Content.Body.ShouldContain("<p>This is a test email</p>");
        sentMessage.Content.MimeParts.ShouldContain(part => part.MimeType == "text/plain" && part.FileName == "test.txt");
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
            To = new List<string> { "recipient@example.com" },
            From = new EmailAddress { Address = "sender@example.com", Name = "Sender" },
            Subject = "Test Email",
            Body = "This is a test email",
            IsHtml = false,
            Priority = EmailPriority.Normal
        };

        // Act
        var result = await emailService.SendAsync(message, new NotificationSendOptions { SendImmediately = false }, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        this.outboxQueue.Received(1).Enqueue(message.Id.ToString());
        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StubDbContext>();
        var storedMessage = await dbContext.OutboxNotificationEmails
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
            To = new List<string> { "recipient@example.com" },
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
        var storedMessage = await dbContext.OutboxNotificationEmails
            .FirstOrDefaultAsync(m => m.Id == message.Id);
        storedMessage.ShouldBeNull();

        // Validate email content via MailHog API
        using var client = this.fixture.GetMailHogApiClient();
        var response = await client.GetAsync("/api/v2/messages");
        var json = await response.Content.ReadAsStringAsync();
        var messages = JsonSerializer.Deserialize<MailHogMessages>(json);
        messages.Items.ShouldNotBeEmpty();
        var sentMessage = messages.Items.First();
        sentMessage.Raw.To.ShouldContain("recipient@example.com");
        sentMessage.Raw.Subject.ShouldBe("Test Email");
        sentMessage.Content.Body.ShouldContain("This is a test email");
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
            To = new List<string> { "recipient@example.com" },
            From = new EmailAddress { Address = "sender@example.com", Name = "Sender" },
            Subject = "Test Email with Embedded",
            Body = "<p>Embedded image: <img src='cid:test-image'></p>",
            IsHtml = true,
            Priority = EmailPriority.Normal,
            Attachments = new List<EmailAttachment>
            {
                new EmailAttachment
                {
                    Id = Guid.NewGuid(),
                    FileName = "image.jpg",
                    ContentType = "image/jpeg",
                    Content = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, // Mock JPEG header
                    IsEmbedded = true,
                    ContentId = "test-image"
                }
            }
        };

        // Act
        var result = await emailService.SendAsync(message, new NotificationSendOptions { SendImmediately = true }, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        using var client = this.fixture.GetMailHogApiClient();
        var response = await client.GetAsync("/api/v2/messages");
        var json = await response.Content.ReadAsStringAsync();
        var messages = JsonSerializer.Deserialize<MailHogMessages>(json);
        messages.Items.ShouldNotBeEmpty();
        var sentMessage = messages.Items.First();
        sentMessage.Raw.To.ShouldContain("recipient@example.com");
        sentMessage.Raw.Subject.ShouldBe("Test Email with Embedded");
        sentMessage.Content.Body.ShouldContain("<img src='cid:test-image'>");
        sentMessage.Content.MimeParts.ShouldContain(part => part.MimeType == "image/jpeg" && part.ContentId == "test-image");
    }

    [Fact]
    public async Task SendAsync_RetryFailure_MarksAsFailed()
    {
        // Arrange
        var emailService = this.serviceProvider.GetRequiredService<INotificationService<EmailMessage>>();
        var worker = new OutboxNotificationEmailWorker(
            Substitute.For<ILoggerFactory>(),
            this.serviceProvider);
        var message = new EmailMessage
        {
            Id = Guid.NewGuid(),
            To = new List<string> { "recipient@example.com" },
            From = new EmailAddress { Address = "sender@example.com", Name = "Sender" },
            Subject = "Test Email",
            Body = "This is a test email",
            IsHtml = false,
            Priority = EmailPriority.Normal
        };

        // Simulate max retries
        message.Properties["ProcessAttempts"] = 2; // RetryCount is 3, so this is the 3rd attempt
        await this.storageProvider.SaveAsync(message, CancellationToken.None);

        // Act
        await worker.ProcessAsync(message.Id.ToString(), CancellationToken.None);

        // Assert
        using var scope = this.serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StubDbContext>();
        var storedMessage = await dbContext.OutboxNotificationEmails
            .FirstOrDefaultAsync(m => m.Id == message.Id);
        storedMessage.ShouldNotBeNull();
        storedMessage.Status.ShouldBe(EmailStatus.Failed);
        storedMessage.SentAt.ShouldNotBeNull();
        storedMessage.PropertiesJson.ShouldContain("Max retries reached");
    }
}

// Helper classes for MailHog API response deserialization
public class MailHogMessages
{
    public List<MailHogMessage> Items { get; set; }
}

public class MailHogMessage
{
    public MailHogRaw Raw { get; set; }
    public MailHogContent Content { get; set; }
}

public class MailHogRaw
{
    public string From { get; set; }
    public string To { get; set; }
    public string Subject { get; set; }
}

public class MailHogContent
{
    public string Body { get; set; }
    public List<MailHogMimePart> MimeParts { get; set; }
}

public class MailHogMimePart
{
    public string MimeType { get; set; }
    public string FileName { get; set; }
    public string ContentId { get; set; }
}