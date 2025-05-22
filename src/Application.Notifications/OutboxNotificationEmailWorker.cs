namespace BridgingIT.DevKit.Infrastructure.Notifications;

using System;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Notifications;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class OutboxNotificationEmailWorker : IOutboxNotificationEmailWorker
{
    private readonly ILogger<OutboxNotificationEmailWorker> logger;
    private readonly IServiceProvider serviceProvider;
    private readonly OutboxNotificationEmailOptions options;

    public OutboxNotificationEmailWorker(
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        OutboxNotificationEmailOptions options)
    {
        this.logger = loggerFactory?.CreateLogger<OutboxNotificationEmailWorker>() ?? NullLoggerFactory.Instance.CreateLogger<OutboxNotificationEmailWorker>();
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        this.options = options ?? new OutboxNotificationEmailOptions();
    }

    public async Task ProcessAsync(string messageId = null, CancellationToken cancellationToken = default)
    {
        using var scope = this.serviceProvider.CreateScope();
        var storageProvider = scope.ServiceProvider.GetRequiredService<INotificationStorageProvider>();
        var emailService = scope.ServiceProvider.GetRequiredService<INotificationService<EmailNotificationMessage>>();
        var count = 0;

        this.logger.LogInformation("{LogKey} Outbox notification emails processing (messageId={MessageId})", "NOT", messageId);

        try
        {
            var messagesResult = await storageProvider.GetPendingAsync<EmailNotificationMessage>(
                this.options.ProcessingCount,
                this.options.RetryCount,
                cancellationToken);
            if (!messagesResult.IsSuccess)
            {
                this.logger.LogError("{LogKey} Failed to retrieve pending messages: {ErrorMessage}", "NOT", messagesResult.Errors?.FirstOrDefault()?.Message);
                return;
            }

            var messages = messagesResult.Value;
            if (!string.IsNullOrEmpty(messageId))
            {
                messages = messages.Where(m => m.Id.ToString() == messageId);
            }

            foreach (var message in messages)
            {
                count++;
                await this.ProcessEmail(message, storageProvider, emailService, cancellationToken);
            }

            this.logger.LogInformation("{LogKey} Outbox notification emails processed (count={Count})", "NOT", count);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} Outbox notification email processing failed: {ErrorMessage}", "NOT", ex.Message);
        }
    }

    public async Task PurgeAsync(bool processedOnly = false, CancellationToken cancellationToken = default)
    {
        using var scope = this.serviceProvider.CreateScope();
        var storageProvider = scope.ServiceProvider.GetRequiredService<INotificationStorageProvider>();

        this.logger.LogInformation("{LogKey} Outbox notification emails purging (processedOnly={ProcessedOnly})", "NOT", processedOnly);

        try
        {
            var messagesResult = await storageProvider.GetPendingAsync<EmailNotificationMessage>(
                int.MaxValue,
                int.MaxValue,
                cancellationToken);
            if (!messagesResult.IsSuccess)
            {
                this.logger.LogError("{LogKey} Failed to retrieve messages for purge: {ErrorMessage}", "NOT", messagesResult.Errors?.FirstOrDefault()?.Message);
                return;
            }

            var messages = processedOnly
                ? messagesResult.Value.Where(m => m.SentAt != null || m.Status != EmailStatus.Pending)
                : messagesResult.Value;

            foreach (var message in messages)
            {
                var deleteResult = await storageProvider.DeleteAsync(message, cancellationToken);
                if (!deleteResult.IsSuccess)
                {
                    this.logger.LogWarning("{LogKey} Failed to delete message with ID {MessageId}: {ErrorMessage}",
                        "NOT",
                        message.Id,
                        deleteResult.Errors?.FirstOrDefault()?.Message);
                }
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} Outbox notification email purge failed: {ErrorMessage}", "NOT", ex.Message);
        }
    }

    private async Task ProcessEmail(
        EmailNotificationMessage message,
        INotificationStorageProvider storageProvider,
        INotificationService<EmailNotificationMessage> emailService,
        CancellationToken cancellationToken)
    {
        if (message.RetryCount >= this.options.RetryCount)
        {
            this.logger.LogWarning(
                "{LogKey} Outbox notification email processing skipped: max retries reached (messageId={MessageId}, attempts={Attempts})",
                "NOT",
                message.Id,
                message.RetryCount);

            message.Status = EmailStatus.Failed;
            message.SentAt = DateTimeOffset.UtcNow;
            message.Properties["ProcessMessage"] = $"Max retries reached (attempts={message.RetryCount})";
            var updateResult = await storageProvider.UpdateAsync(message, cancellationToken);
            if (!updateResult.IsSuccess)
            {
                this.logger.LogWarning(
                    "{LogKey} Failed to update message with ID {MessageId} after max retries: {ErrorMessage}",
                    "NOT",
                    message.Id,
                    updateResult.Errors?.FirstOrDefault()?.Message);
            }
            return;
        }

        try
        {
            message.RetryCount++;
            var sendResult = await emailService.SendAsync(
                message,
                new NotificationSendOptions { SendImmediately = true },
                cancellationToken);

            message.Status = sendResult.IsSuccess ? EmailStatus.Sent : EmailStatus.Failed;
            message.SentAt = DateTimeOffset.UtcNow;
            message.Properties["ProcessMessage"] = sendResult.Errors?.FirstOrDefault()?.Message;

            var updateResult = await storageProvider.UpdateAsync(message, cancellationToken);
            if (!updateResult.IsSuccess)
            {
                this.logger.LogWarning(
                    "{LogKey} Failed to update message with ID {MessageId} after processing: {ErrorMessage}",
                    "NOT",
                    message.Id,
                    updateResult.Errors?.FirstOrDefault()?.Message);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(
                ex,
                "{LogKey} Outbox notification email processing failed: {ErrorMessage} (messageId={MessageId}, attempts={Attempts})",
                "NOT",
                ex.Message,
                message.Id,
                message.RetryCount);

            message.Status = EmailStatus.Failed;
            message.Properties["ProcessMessage"] = $"[{ex.GetType().Name}] {ex.Message}";
            var updateResult = await storageProvider.UpdateAsync(message, cancellationToken);
            if (!updateResult.IsSuccess)
            {
                this.logger.LogWarning(
                    "{LogKey} Failed to update message with ID {MessageId} after failure: {ErrorMessage}",
                    "NOT",
                    message.Id,
                    updateResult.Errors?.FirstOrDefault()?.Message);
            }
        }
    }
}