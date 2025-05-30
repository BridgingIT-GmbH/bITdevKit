// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Notifications;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

public class OutboxNotificationEmailWorker(
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider,
    OutboxNotificationEmailOptions options = null) : IOutboxNotificationEmailWorker
{
    private readonly ILogger<OutboxNotificationEmailWorker> logger = loggerFactory?.CreateLogger<OutboxNotificationEmailWorker>() ?? NullLoggerFactory.Instance.CreateLogger<OutboxNotificationEmailWorker>();
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly OutboxNotificationEmailOptions options = options ?? new OutboxNotificationEmailOptions();

    public async Task ProcessAsync(string messageId = null, CancellationToken cancellationToken = default)
    {
        using var scope = this.serviceProvider.CreateScope();
        var storageProvider = scope.ServiceProvider.GetRequiredService<INotificationStorageProvider>();
        var emailService = scope.ServiceProvider.GetRequiredService<INotificationService<EmailMessage>>();
        var count = 0;

        this.logger.LogDebug("{LogKey} outbox notification emails processing (messageId={MessageId})", Constants.LogKey, messageId);

        try
        {
            var messagesResult = await storageProvider.GetPendingAsync<EmailMessage>(this.options.ProcessingCount/*, this.options.RetryCount*/, cancellationToken);
            if (!messagesResult.IsSuccess)
            {
                this.logger.LogError("{LogKey} failed to retrieve pending messages: {ErrorMessage}", Constants.LogKey, messagesResult.Errors?.FirstOrDefault()?.Message);
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

            if (count == 0)
            {
                this.logger.LogDebug("{LogKey} outbox notification emails processed (count={Count})", Constants.LogKey, count);
            }
            else
            {
                this.logger.LogInformation("{LogKey} outbox notification emails processed (count={Count})", Constants.LogKey, count);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} outbox notification email processing failed: {ErrorMessage}", Constants.LogKey, ex.Message);
        }
    }

    public async Task PurgeAsync(bool processedOnly = false, CancellationToken cancellationToken = default)
    {
        using var scope = this.serviceProvider.CreateScope();
        var storageProvider = scope.ServiceProvider.GetRequiredService<INotificationStorageProvider>();

        this.logger.LogInformation("{LogKey} outbox notification emails purging (processedOnly={ProcessedOnly})", Constants.LogKey, processedOnly);

        try
        {
            var messagesResult = await storageProvider.GetPendingAsync<EmailMessage>(int.MaxValue/*, int.MaxValue*/, cancellationToken);
            if (!messagesResult.IsSuccess)
            {
                this.logger.LogError("{LogKey} failed to retrieve messages for purge: {ErrorMessage}", Constants.LogKey, messagesResult.Errors?.FirstOrDefault()?.Message);
                return;
            }

            var messages = processedOnly
                ? messagesResult.Value.Where(m => m.SentAt != null || m.Status != EmailMessageStatus.Pending)
                : messagesResult.Value;

            foreach (var message in messages)
            {
                var deleteResult = await storageProvider.DeleteAsync(message, cancellationToken);
                if (!deleteResult.IsSuccess)
                {
                    this.logger.LogWarning("{LogKey} failed to delete message with ID {MessageId}: {ErrorMessage}",
                        Constants.LogKey,
                        message.Id,
                        deleteResult.Errors?.FirstOrDefault()?.Message);
                }
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} outbox notification email purge failed: {ErrorMessage}", Constants.LogKey, ex.Message);
        }
    }

    private async Task ProcessEmail(
        EmailMessage message,
        INotificationStorageProvider storageProvider,
        INotificationService<EmailMessage> emailService,
        CancellationToken cancellationToken)
    {
        if (message.RetryCount >= this.options.RetryCount)
        {
            this.logger.LogWarning("{LogKey} outbox notification email processing skipped: max retries reached (messageId={MessageId}, attempts={Attempts})", Constants.LogKey, message.Id, message.RetryCount);

            message.Status = EmailMessageStatus.Failed;
            message.SentAt = DateTimeOffset.UtcNow;
            message.Properties["ProcessMessage"] = $"Max retries reached (attempts={message.RetryCount})";

            var updateResult = await storageProvider.UpdateAsync(message, cancellationToken);
            if (!updateResult.IsSuccess)
            {
                this.logger.LogWarning("{LogKey} failed to update message with ID {MessageId} after max retries: {ErrorMessage}", Constants.LogKey, message.Id, updateResult.Errors?.FirstOrDefault()?.Message);
            }
            return;
        }

        try
        {
            message.Properties["Outbox"] = true; // prevents readding (Save) in email service (SendAsync)
            message.RetryCount++;
            var sendResult = await emailService.SendAsync(
                message, new NotificationSendOptions { SendImmediately = true }, cancellationToken);

            //message.Status = sendResult.IsSuccess ? EmailMessageStatus.Sent : EmailMessageStatus.Failed;
            //message.SentAt = DateTimeOffset.UtcNow;
            //message.Properties["ProcessMessage"] = sendResult.Errors?.FirstOrDefault()?.Message;

            //var updateResult = await storageProvider.UpdateAsync(message, cancellationToken);
            //if (!updateResult.IsSuccess)
            //{
            //    this.logger.LogWarning("{LogKey} failed to update message with ID {MessageId} after processing: {ErrorMessage}", Constants.LogKey, message.Id, updateResult.Errors?.FirstOrDefault()?.Message);
            //}
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} outbox notification email processing failed: {ErrorMessage} (messageId={MessageId}, attempts={Attempts})", Constants.LogKey, ex.Message, message.Id, message.RetryCount);

            message.Status = EmailMessageStatus.Failed;
            message.Properties["ProcessMessage"] = $"[{ex.GetType().Name}] {ex.Message}";
            var updateResult = await storageProvider.UpdateAsync(message, cancellationToken);
            if (!updateResult.IsSuccess)
            {
                this.logger.LogWarning("{LogKey} failed to update message with ID {MessageId} after failure: {ErrorMessage}", Constants.LogKey, message.Id, updateResult.Errors?.FirstOrDefault()?.Message);
            }
        }
    }
}