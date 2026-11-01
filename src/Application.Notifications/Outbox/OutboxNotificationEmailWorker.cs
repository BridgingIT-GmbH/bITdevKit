// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Notifications;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Claims and processes persisted notification emails from the configured storage provider.
/// </summary>
public class OutboxNotificationEmailWorker(
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider,
    OutboxNotificationEmailOptions options = null) : IOutboxNotificationEmailWorker
{
    private readonly ILogger<OutboxNotificationEmailWorker> logger = loggerFactory?.CreateLogger<OutboxNotificationEmailWorker>() ?? NullLoggerFactory.Instance.CreateLogger<OutboxNotificationEmailWorker>();
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly OutboxNotificationEmailOptions options = options ?? new OutboxNotificationEmailOptions();

    /// <summary>
    /// Processes a specific notification email or the next claimed batch.
    /// </summary>
    public Task ProcessAsync(string messageId = null, CancellationToken cancellationToken = default)
        => AuditSourceExecutor.RunIsolatedAsync(this.serviceProvider.GetRequiredService<IServiceScopeFactory>(), "Email", messageId ?? "batch", null, null, async () =>
        {
            await this.ProcessCoreAsync(messageId, cancellationToken).ConfigureAwait(false);
            return true;
        });

    private async Task ProcessCoreAsync(string messageId, CancellationToken cancellationToken)
    {
        await using var scope = this.serviceProvider.CreateAsyncScope();
        var storageProvider = scope.ServiceProvider.GetRequiredService<INotificationStorageProvider>();
        var outboxService = scope.ServiceProvider.GetService<INotificationEmailOutboxService>();
        var count = 0;

        this.logger.LogDebug("[{LogKey}] outbox notification emails processing (messageId={MessageId})", Constants.LogKey, messageId);

        try
        {
            var messagesResult = await storageProvider.GetPendingAsync<EmailMessage>(this.options.ProcessingCount/*, this.options.RetryCount*/, cancellationToken);
            if (!messagesResult.IsSuccess)
            {
                this.logger.LogError("[{LogKey}] failed to retrieve pending messages: {ErrorMessage}", Constants.LogKey, messagesResult.Errors?.FirstOrDefault()?.Message);
                return;
            }

            var messages = string.IsNullOrEmpty(messageId)
                ? messagesResult.Value
                : messagesResult.Value.OrderByDescending(m => m.Id.ToString() == messageId);

            foreach (var message in messages)
            {
                count++;
                var operation = new NotificationOperationContext
                {
                    Operation = "OutboxExecution", MessageId = message.Id.ToString("D"), Properties = message.Properties, IsWorker = true,
                    Attempt = message.RetryCount < this.options.RetryCount ? message.RetryCount + 1 : null,
                    SuccessStatus = "SmtpAccepted", FailureStatus = message.RetryCount >= this.options.RetryCount ? "RetryLimit" : "Failed", CancellationToken = cancellationToken
                };
                async Task<Result> Execute()
                {
                    await using var messageScope = this.serviceProvider.CreateAsyncScope();
                    return await this.ProcessEmail(message, storageProvider, messageScope.ServiceProvider.GetRequiredService<INotificationService<EmailMessage>>(), cancellationToken).ConfigureAwait(false);
                }

                Func<Task<Result>> next = Execute;
                foreach (var behavior in AuditSourceExecutor.DistinctAuditing<INotificationOperationBehavior, AuditingNotificationOperationBehavior>(scope.ServiceProvider.GetServices<INotificationOperationBehavior>()).OrderBy(behavior => behavior is AuditingNotificationOperationBehavior ? 0 : 1).Reverse())
                {
                    var inner = next;
                    next = () => behavior.ExecuteAsync(operation, inner);
                }

                _ = await AuditSourceExecutor.RunIsolatedAsync(this.serviceProvider.GetRequiredService<IServiceScopeFactory>(), "Email", operation.MessageId,
                    AuditHandoffMetadata.Read(message.Properties), null, next).ConfigureAwait(false);
            }

            var archivedCount = await this.AutoArchiveMessagesAsync(outboxService, cancellationToken);

            if (count == 0)
            {
                this.logger.LogDebug("[{LogKey}] outbox notification emails processed (count={Count}, archivedCount={ArchivedCount})", Constants.LogKey, count, archivedCount);
            }
            else
            {
                this.logger.LogInformation("[{LogKey}] outbox notification emails processed (count={Count}, archivedCount={ArchivedCount})", Constants.LogKey, count, archivedCount);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "[{LogKey}] outbox notification email processing failed: {ErrorMessage}", Constants.LogKey, ex.Message);
        }
    }

    /// <summary>
    /// Purges notification emails from the configured storage provider.
    /// </summary>
    public async Task PurgeAsync(bool processedOnly = false, CancellationToken cancellationToken = default)
    {
        using var scope = this.serviceProvider.CreateScope();
        var storageProvider = scope.ServiceProvider.GetRequiredService<INotificationStorageProvider>();

        this.logger.LogInformation("[{LogKey}] outbox notification emails purging (processedOnly={ProcessedOnly})", Constants.LogKey, processedOnly);

        try
        {
            var messagesResult = await storageProvider.GetPendingAsync<EmailMessage>(int.MaxValue/*, int.MaxValue*/, cancellationToken);
            if (!messagesResult.IsSuccess)
            {
                this.logger.LogError("[{LogKey}] failed to retrieve messages for purge: {ErrorMessage}", Constants.LogKey, messagesResult.Errors?.FirstOrDefault()?.Message);
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
                    this.logger.LogWarning("[{LogKey}] failed to delete message with ID {MessageId}: {ErrorMessage}",
                        Constants.LogKey,
                        message.Id,
                        deleteResult.Errors?.FirstOrDefault()?.Message);
                }
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "[{LogKey}] outbox notification email purge failed: {ErrorMessage}", Constants.LogKey, ex.Message);
        }
    }

    private async Task<Result> ProcessEmail(
        EmailMessage message,
        INotificationStorageProvider storageProvider,
        INotificationService<EmailMessage> emailService,
        CancellationToken cancellationToken)
    {
        if (message.RetryCount >= this.options.RetryCount)
        {
            this.logger.LogWarning("[{LogKey}] outbox notification email processing skipped: max retries reached (messageId={MessageId}, attempts={Attempts})", Constants.LogKey, message.Id, message.RetryCount);

            message.Status = EmailMessageStatus.Failed;
            message.SentAt = null;
            message.Properties["ProcessMessage"] = $"Max retries reached (attempts={message.RetryCount})";

            var updateResult = await storageProvider.UpdateAsync(message, cancellationToken);
            if (!updateResult.IsSuccess)
            {
                this.logger.LogWarning("[{LogKey}] failed to update message with ID {MessageId} after max retries: {ErrorMessage}", Constants.LogKey, message.Id, updateResult.Errors?.FirstOrDefault()?.Message);
            }

            return updateResult.IsFailure ? updateResult : Result.Failure(new Error("Retry limit reached."));
        }

        try
        {
            message.Properties["Outbox"] = true; // prevents readding (Save) in email service (SendAsync)
            message.RetryCount++;
            return await emailService.SendAsync(
                message, new NotificationSendOptions { SendImmediately = true }, cancellationToken);

        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "[{LogKey}] outbox notification email processing failed: {ErrorMessage} (messageId={MessageId}, attempts={Attempts})", Constants.LogKey, ex.Message, message.Id, message.RetryCount);

            message.Status = EmailMessageStatus.Failed;
            message.Properties["ProcessMessage"] = $"[{ex.GetType().Name}] {ex.Message}";
            var updateResult = await storageProvider.UpdateAsync(message, cancellationToken);
            if (!updateResult.IsSuccess)
            {
                this.logger.LogWarning("[{LogKey}] failed to update message with ID {MessageId} after failure: {ErrorMessage}", Constants.LogKey, message.Id, updateResult.Errors?.FirstOrDefault()?.Message);
            }

            return updateResult.IsFailure ? updateResult : Result.Failure(new Error("Notification processing failed."));
        }
    }

    private async Task<int> AutoArchiveMessagesAsync(
        INotificationEmailOutboxService outboxService,
        CancellationToken cancellationToken)
    {
        if (outboxService is null || !this.options.AutoArchiveAfter.HasValue)
        {
            return 0;
        }

        var statuses = this.options.AutoArchiveStatuses?.Distinct().ToArray() ?? [];
        if (statuses.Length == 0)
        {
            return 0;
        }

        var archiveThreshold = DateTimeOffset.UtcNow.Subtract(this.options.AutoArchiveAfter.Value);
        var archivedCount = await outboxService.AutoArchiveMessagesAsync(archiveThreshold, statuses, cancellationToken);

        if (archivedCount > 0)
        {
            this.logger.LogInformation(
                "[{LogKey}] outbox notification emails auto-archived (count={Count}, olderThan={OlderThan})",
                Constants.LogKey,
                archivedCount,
                archiveThreshold);
        }

        return archivedCount;
    }
}
