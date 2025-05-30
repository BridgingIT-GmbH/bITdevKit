// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Notifications;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Common.Utilities;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using MimeKit;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class NotificationEmailService(
    ILogger<NotificationEmailService> logger,
    INotificationStorageProvider storageProvider,
    ISmtpClient smtpClient,
    NotificationServiceOptions options,
    IOutboxNotificationEmailQueue outboxQueue = null) : INotificationService<EmailMessage>
{
    private readonly INotificationStorageProvider storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
    private readonly NotificationServiceOptions options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<NotificationEmailService> logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ISmtpClient smtpClient = smtpClient ?? throw new ArgumentNullException(nameof(smtpClient));

    public async Task<Result> SendAsync(
        EmailMessage message,
        NotificationSendOptions options,
        CancellationToken cancellationToken)
    {
        if (message == null)
        {
            return await Task.FromResult(Result.Failure()
                .WithError(new Error("Message cannot be null")));
        }

        try
        {
            if (!this.options.IsOutboxConfigured)
            {
                return await this.SendAsync(message, cancellationToken);
            }

            if (!message.Properties.ContainsKey("Outbox")) // store pending in outbox
            {
                message.Status = EmailMessageStatus.Pending;
                var saveResult = await this.storageProvider.SaveAsync(message, cancellationToken);
                if (!saveResult.IsSuccess)
                {
                    return saveResult;
                }
            }

            if (options?.SendImmediately == true)
            {
                var result = await this.SendAsync(message, cancellationToken);
                message.Status = result.IsSuccess ? EmailMessageStatus.Sent : EmailMessageStatus.Failed;
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
                outboxQueue?.Enqueue(message.Id.ToString()); // worker uses this to process immediately
            }

            return await Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} failed to process message with ID {MessageId}", Constants.LogKey, message.Id);
            return await Task.FromResult(Result.Failure()
                .WithError(new Error($"Failed to process message: {ex.Message}")));
        }
    }

    public async Task<Result> QueueAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (message == null)
        {
            return await Task.FromResult(Result.Failure()
                .WithError(new Error("Message cannot be null")));
        }

        try
        {
            if (!this.options.IsOutboxConfigured)
            {
                this.logger.LogWarning("{LogKey} outbox not configured, queuing message with ID {MessageId} ignored", Constants.LogKey, message.Id);

                return await Task.FromResult(Result.Success());
            }

            message.Status = EmailMessageStatus.Pending;
            var saveResult = await this.storageProvider.SaveAsync(message, cancellationToken);
            if (!saveResult.IsSuccess)
            {
                return saveResult;
            }

            if (this.options.OutboxOptions?.ProcessingMode == OutboxNotificationEmailProcessingMode.Immediate)
            {
                outboxQueue?.Enqueue(message.Id.ToString());
            }

            return await Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} failed to queue message with ID {MessageId}", Constants.LogKey, message.Id);
            return await Task.FromResult(Result.Failure()
                .WithError(new Error($"Failed to queue message: {ex.Message}")));
        }
    }

    private async Task<Result> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var mimeMessage = this.MapToMimeMessage(message);
            var retryer = new Retryer(3, TimeSpan.FromSeconds(1));
            var timeoutHandler = new TimeoutHandlerBuilder(TimeSpan.FromSeconds(30)).Build();

            await retryer.ExecuteAsync(
                async ct => await timeoutHandler.ExecuteAsync(
                    async ct =>
                    {
                        await this.smtpClient.ConnectAsync(this.options.SmtpSettings.Host, this.options.SmtpSettings.Port, this.options.SmtpSettings.UseSsl, ct);

                        if (!string.IsNullOrEmpty(this.options.SmtpSettings.Username) &&
                            !string.IsNullOrEmpty(this.options.SmtpSettings.Password))
                        {
                            await this.smtpClient.AuthenticateAsync(
                                this.options.SmtpSettings.Username, this.options.SmtpSettings.Password, ct);
                        }

                        await this.smtpClient.SendAsync(mimeMessage, ct);
                        await this.smtpClient.DisconnectAsync(true, ct);
                    },
                    cancellationToken),
                cancellationToken);

            this.logger.LogInformation("{LogKey} mailservice - successfully sent email (id={MessageId})", Constants.LogKey, message.Id);
            return await Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} mailservice - failed to send email (id={MessageId})", Constants.LogKey, message.Id);
            return await Task.FromResult(Result.Failure()
                .WithError(new Error($"Failed to send email: {ex.Message}")));
        }
    }

    private MimeMessage MapToMimeMessage(EmailMessage message)
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
            mimeMessage.ReplyTo.Add(
                new MailboxAddress(message.ReplyTo.Name ?? string.Empty, message.ReplyTo.Address));
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
                Content = new MimeContent(new MemoryStream(attachment.Content)),
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