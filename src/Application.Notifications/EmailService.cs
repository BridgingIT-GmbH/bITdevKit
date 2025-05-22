namespace BridgingIT.DevKit.Application.Notifications;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Common.Utilities;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using MimeKit;

public class EmailService : INotificationService<EmailNotificationMessage>
{
    private readonly INotificationStorageProvider storageProvider;
    private readonly NotificationServiceOptions options;
    private readonly ILogger<EmailService> logger;
    private readonly ISmtpClient smtpClient;
    private readonly IOutboxNotificationEmailQueue outboxQueue;

    public EmailService(
        INotificationStorageProvider storageProvider,
        NotificationServiceOptions options,
        ILogger<EmailService> logger,
        ISmtpClient smtpClient,
        IOutboxNotificationEmailQueue outboxQueue = null)
    {
        this.storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.smtpClient = smtpClient ?? throw new ArgumentNullException(nameof(smtpClient));
        this.outboxQueue = outboxQueue;
    }

    public async Task<Result> SendAsync(
        EmailNotificationMessage message,
        NotificationSendOptions options,
        CancellationToken cancellationToken)
    {
        if (message == null)
        {
            return await Task.FromResult(Result.Failure().WithError(new Error("Message cannot be null")));
        }

        try
        {
            if (!this.options.IsOutboxConfigured)
            {
                return await this.SendImmediatelyAsync(message, cancellationToken);
            }

            message.Status = EmailStatus.Pending;
            var saveResult = await this.storageProvider.SaveAsync(message, cancellationToken);
            if (!saveResult.IsSuccess)
            {
                return saveResult;
            }

            if (options?.SendImmediately == true)
            {
                var result = await this.SendImmediatelyAsync(message, cancellationToken);
                message.Status = result.IsSuccess ? EmailStatus.Sent : EmailStatus.Failed;
                message.SentAt = DateTimeOffset.UtcNow;
                message.Properties["ProcessMessage"] = result.Errors?.FirstOrDefault()?.Message;
                var updateResult = await this.storageProvider.UpdateAsync(message, cancellationToken);
                if (!updateResult.IsSuccess)
                {
                    return updateResult;
                }
                return result;
            }

            if (this.options.OutboxOptions?.ProcessingMode == OutboxNotificationEmailProcessingMode.Immediate)
            {
                this.outboxQueue?.Enqueue(message.Id.ToString());
            }

            return await Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to process message with ID {MessageId}", message.Id);
            return await Task.FromResult(Result.Failure().WithError(new Error($"Failed to process message: {ex.Message}")));
        }
    }

    public async Task<Result> QueueAsync(EmailNotificationMessage message, CancellationToken cancellationToken)
    {
        if (message == null)
        {
            return await Task.FromResult(Result.Failure().WithError(new Error("Message cannot be null")));
        }

        try
        {
            if (!this.options.IsOutboxConfigured)
            {
                this.logger.LogWarning("Outbox not configured, queuing message with ID {MessageId} ignored", message.Id);
                return await Task.FromResult(Result.Success());
            }

            message.Status = EmailStatus.Pending;
            var saveResult = await this.storageProvider.SaveAsync(message, cancellationToken);
            if (!saveResult.IsSuccess)
            {
                return saveResult;
            }

            if (this.options.OutboxOptions?.ProcessingMode == OutboxNotificationEmailProcessingMode.Immediate)
            {
                this.outboxQueue?.Enqueue(message.Id.ToString());
            }

            return await Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to queue message with ID {MessageId}", message.Id);
            return await Task.FromResult(Result.Failure().WithError(new Error($"Failed to queue message: {ex.Message}")));
        }
    }

    private async Task<Result> SendImmediatelyAsync(EmailNotificationMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var mimeMessage = this.MapToMimeMessage(message);
            var retryer = new Retryer(3, TimeSpan.FromSeconds(1));
            var timeoutHandler = new TimeoutHandlerBuilder(TimeSpan.FromSeconds(30)).Build();

            await retryer.ExecuteAsync(
                async ct => await timeoutHandler.ExecuteAsync(
                    async ct => await this.smtpClient.SendAsync(mimeMessage, ct),
                    cancellationToken),
                cancellationToken);

            this.logger.LogInformation("Successfully sent email with ID {MessageId}", message.Id);
            return await Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to send email with ID {MessageId}", message.Id);
            return await Task.FromResult(Result.Failure().WithError(new Error($"Failed to send email: {ex.Message}")));
        }
    }

    private MimeMessage MapToMimeMessage(EmailNotificationMessage message)
    {
        var mimeMessage = new MimeMessage();

        // From
        mimeMessage.From.Add(new MailboxAddress(
            this.options.SmtpSettings.SenderName ?? message.From?.Name ?? string.Empty,
            message.From?.Address ?? this.options.SmtpSettings.SenderAddress));

        // To
        foreach (var to in message.To)
        {
            mimeMessage.To.Add(MailboxAddress.Parse(to));
        }

        // CC
        foreach (var cc in message.CC)
        {
            mimeMessage.Cc.Add(MailboxAddress.Parse(cc));
        }

        // BCC
        foreach (var bcc in message.BCC)
        {
            mimeMessage.Bcc.Add(MailboxAddress.Parse(bcc));
        }

        // ReplyTo
        if (message.ReplyTo != null && !string.IsNullOrEmpty(message.ReplyTo.Address))
        {
            mimeMessage.ReplyTo.Add(new MailboxAddress(message.ReplyTo.Name ?? string.Empty, message.ReplyTo.Address));
        }

        // Subject
        mimeMessage.Subject = message.Subject;

        // Headers
        foreach (var header in message.Headers)
        {
            mimeMessage.Headers.Add(header.Key, header.Value);
        }

        // Body
        var builder = new BodyBuilder();
        if (message.IsHtml)
        {
            builder.HtmlBody = message.Body;
        }
        else
        {
            builder.TextBody = message.Body;
        }

        // Attachments
        foreach (var attachment in message.Attachments)
        {
            var mimePart = new MimePart(attachment.ContentType)
            {
                Content = new MimeContent(new System.IO.MemoryStream(attachment.Content)),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
                FileName = attachment.FileName
            };
            if (attachment.IsEmbedded && !string.IsNullOrEmpty(attachment.ContentId))
            {
                mimePart.ContentId = attachment.ContentId;
                mimePart.ContentDisposition.Disposition = ContentDisposition.Inline;
            }
            builder.Attachments.Add(mimePart);
        }

        mimeMessage.Body = builder.ToMessageBody();

        return mimeMessage;
    }
}