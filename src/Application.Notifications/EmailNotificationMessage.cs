namespace BridgingIT.DevKit.Application.Notifications;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents an email notification that can be queued in the outbox or delivered immediately.
/// </summary>
/// <example>
/// <code>
/// var message = new EmailMessage
/// {
///     Id = Guid.NewGuid(),
///     To = ["alice@example.com"],
///     Subject = "Todo created",
///     Body = "A new todo item was assigned to you.",
///     From = new EmailAddress { Name = "DoFiesta", Address = "noreply@example.com" }
/// };
/// </code>
/// </example>
public class EmailMessage : INotificationMessage
{
    /// <summary>
    /// Gets or sets the message identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the primary recipients.
    /// </summary>
    public List<string> To { get; set; } = [];

    /// <summary>
    /// Gets or sets the carbon-copy recipients.
    /// </summary>
    public List<string> CC { get; set; } = [];

    /// <summary>
    /// Gets or sets the blind-carbon-copy recipients.
    /// </summary>
    public List<string> BCC { get; set; } = [];

    /// <summary>
    /// Gets or sets the explicit sender identity. Falls back to <see cref="SmtpSettings" /> defaults when omitted.
    /// </summary>
    public EmailAddress From { get; set; }

    /// <summary>
    /// Gets or sets the reply-to identity.
    /// </summary>
    public EmailAddress ReplyTo { get; set; }

    /// <summary>
    /// Gets or sets the email subject line.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the plain text or HTML body content.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="Body" /> contains HTML markup.
    /// </summary>
    public bool IsHtml { get; set; }

    /// <summary>
    /// Gets or sets additional transport headers.
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = [];

    /// <summary>
    /// Gets or sets additional outbox properties persisted alongside the email.
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = [];

    /// <summary>
    /// Gets or sets the message delivery priority.
    /// </summary>
    public EmailMessagePriority Priority { get; set; } = EmailMessagePriority.Normal;

    /// <summary>
    /// Gets or sets the current outbox processing status.
    /// </summary>
    public EmailMessageStatus Status { get; set; } = EmailMessageStatus.Pending;

    /// <summary>
    /// Gets or sets the number of outbox processing attempts performed so far.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the message creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the successful delivery timestamp.
    /// </summary>
    public DateTimeOffset? SentAt { get; set; }

    /// <summary>
    /// Gets or sets the attachments stored with the email.
    /// </summary>
    public List<EmailAttachment> Attachments { get; set; } = [];
}

/// <summary>
/// Represents an email address with an optional display name.
/// </summary>
public class EmailAddress
{
    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the email address.
    /// </summary>
    public string Address { get; set; }
}

/// <summary>
/// Represents an email attachment or embedded resource.
/// </summary>
public class EmailAttachment
{
    /// <summary>
    /// Gets or sets the attachment identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the parent email identifier.
    /// </summary>
    public Guid EmailMessageId { get; set; }

    /// <summary>
    /// Gets or sets the attachment file name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the attachment media type.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the attachment content bytes.
    /// </summary>
    public byte[] Content { get; set; }

    /// <summary>
    /// Gets or sets the content identifier for inline attachments.
    /// </summary>
    public string ContentId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the attachment should be rendered inline.
    /// </summary>
    public bool IsEmbedded { get; set; }
}

/// <summary>
/// Defines the delivery priority for an email notification.
/// </summary>
public enum EmailMessagePriority
{
    /// <summary>
    /// Marks the message as high priority.
    /// </summary>
    High = 1,

    /// <summary>
    /// Marks the message as normal priority.
    /// </summary>
    Normal = 2,

    /// <summary>
    /// Marks the message as low priority.
    /// </summary>
    Low = 3
}

/// <summary>
/// Represents the persisted processing state of a notification email.
/// </summary>
public enum EmailMessageStatus
{
    /// <summary>
    /// The message is waiting to be claimed by an outbox worker.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The message has been delivered successfully.
    /// </summary>
    Sent = 1,

    /// <summary>
    /// The message is currently leased by an outbox worker.
    /// </summary>
    Locked = 2,

    /// <summary>
    /// The message failed delivery and may require intervention or retry.
    /// </summary>
    Failed = 3
}
