namespace BridgingIT.DevKit.Application.Notifications;

using System;
using System.Collections.Generic;

public class EmailNotificationMessage : INotificationMessage
{
    public Guid Id { get; set; }

    public List<string> To { get; set; } = new();

    public List<string> CC { get; set; } = new();

    public List<string> BCC { get; set; } = new();

    public EmailAddress From { get; set; }

    public EmailAddress ReplyTo { get; set; }

    public string Subject { get; set; }

    public string Body { get; set; }

    public bool IsHtml { get; set; }

    public Dictionary<string, string> Headers { get; set; } = new();

    public Dictionary<string, object> Properties { get; set; } = new();

    public EmailPriority Priority { get; set; } = EmailPriority.Normal;

    public EmailStatus Status { get; set; } = EmailStatus.Pending;

    public int RetryCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? SentAt { get; set; }

    public List<EmailAttachment> Attachments { get; set; } = new();
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

public enum EmailPriority
{
    Low,
    Normal,
    High
}

public enum EmailStatus
{
    Pending,
    Sent,
    Failed
}
