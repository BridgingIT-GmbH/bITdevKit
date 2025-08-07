namespace BridgingIT.DevKit.Application.Notifications;

using System;
using System.Collections.Generic;

public class EmailMessage : INotificationMessage
{
    public Guid Id { get; set; }

    public List<string> To { get; set; } = [];

    public List<string> CC { get; set; } = [];

    public List<string> BCC { get; set; } = [];

    public EmailAddress From { get; set; }

    public EmailAddress ReplyTo { get; set; }

    public string Subject { get; set; }

    public string Body { get; set; }

    public bool IsHtml { get; set; }

    public Dictionary<string, string> Headers { get; set; } = [];

    public Dictionary<string, object> Properties { get; set; } = [];

    public EmailMessagePriority Priority { get; set; } = EmailMessagePriority.Normal;

    public EmailMessageStatus Status { get; set; } = EmailMessageStatus.Pending;

    public int RetryCount { get; set; } // for outbox processing retries

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? SentAt { get; set; }

    public List<EmailAttachment> Attachments { get; set; } = [];
}

public class EmailAddress
{
    public string Name { get; set; }

    public string Address { get; set; }
}

public class EmailAttachment
{
    public Guid Id { get; set; }

    public Guid EmailMessageId { get; set; }

    public string FileName { get; set; }

    public string ContentType { get; set; }

    public byte[] Content { get; set; }

    public string ContentId { get; set; }

    public bool IsEmbedded { get; set; }
}

public enum EmailMessagePriority
{
    High = 1,
    Normal = 2,
    Low = 3
}

public enum EmailMessageStatus
{
    Pending = 0,
    Sent = 1,
    Locked = 2,
    Failed = 3
}
